#!/usr/bin/env python3
"""Install the pinned Linux Terrain3D runtime; --probe also checks its API."""

from __future__ import annotations

import hashlib
import shutil
import stat
import sys
import zipfile
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))
import build  # noqa: E402

VERSION = "v1.1.0-rc1"
URL = f"https://github.com/TokisanGames/Terrain3D/releases/download/{VERSION}/Terrain3D_{VERSION}.zip"
SHA256 = "549d0c1d45982ae2e30d90275f754a9c7174b8f57eb600bc93a8108b98d44636"
CACHE = ROOT / ".tools" / "terrain3d"
ARCHIVE = CACHE / f"Terrain3D_{VERSION}.zip"
PROJECT = CACHE / "probe_project"
ADDON = PROJECT / "addons" / "terrain_3d"
RUNTIME = ROOT / "addons" / "terrain_3d"

PROJECT_GODOT = """; Isolated Terrain3D runtime probe.
config_version=5

[application]
config/name="Terrain3D Runtime Probe"
run/main_scene="res://Probe.tscn"
config/features=PackedStringArray("4.7", "C#", "Forward Plus")

[dotnet]
project/assembly_name="Terrain3DProbe"
"""

CSPROJ = """<Project Sdk="Godot.NET.Sdk/4.7.2">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="addons/terrain_3d/csharp/**/*.cs" />
  </ItemGroup>
</Project>
"""

PROBE_CS = """using Godot;

public partial class Probe : Node
{
    public override void _Ready()
    {
        try
        {
            var extensionPath = ProjectSettings.GlobalizePath("res://addons/terrain_3d/terrain.gdextension");
            var loadStatus = GDExtensionManager.LoadExtension(extensionPath);
            Require(loadStatus == GDExtensionManager.LoadStatus.Ok || loadStatus == GDExtensionManager.LoadStatus.AlreadyLoaded,
                $"native extension loaded ({loadStatus})");
            Require(ClassDB.ClassExists("Terrain3D"), "Terrain3D GDExtension class was registered");
            var terrain = ClassDB.Instantiate("Terrain3D").AsGodotObject();
            Require(terrain is Node, "Terrain3D can be instantiated as a Node");
            var camera = new Camera3D();
            AddChild(camera);
            terrain!.Set("free_editor_textures", false);
            terrain.Call("set_camera", camera);
            AddChild((Node)terrain!);

            var data = terrain!.Get("data").AsGodotObject();
            var region = data!.Call("add_region_blank", Vector2I.Zero, false).AsGodotObject();
            Require(region is not null, "blank region created at region (0,0)");

            var heightMap = Image.CreateEmpty(256, 256, false, Image.Format.Rf);
            for (int z = 0; z < 256; z++)
                for (int x = 0; x < 256; x++)
                    heightMap.SetPixel(x, z, new Color(x * 0.1f + z * 0.02f, 0, 0, 1));
            region!.Call("set_height_map", heightMap);
            region.Call("calc_height_range");
            data.Call("update_maps");

            var sample = (float)data.Call("get_height", new Vector3(10, 0, 20));
            Require(Mathf.IsEqualApprox(sample, 1.4f), $"height map reads back expected sample (got {sample})");
            Require((bool)data.Call("has_region", Vector2I.Zero), "region is active after map update");

            terrain.Set("collision_mode", 3); // Terrain3D CollisionMode.FULL_GAME
            var collision = terrain.Get("collision").AsGodotObject();
            collision!.Call("build");
            Require((bool)collision.Call("is_enabled"), "full-game collision builds for the region");

            GD.Print("TERRAIN3D-PROBE PASS");
        }
        catch (System.Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
            return;
        }
        GetTree().Quit();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new System.InvalidOperationException(message);
        GD.Print("TERRAIN3D-PROBE: ", message);
    }
}
"""

TSCN = """[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://Probe.cs" id="1"]

[node name="Probe" type="Node"]
script = ExtResource("1")
"""


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def safe_member(name: str) -> PurePosixPath:
    if "\\" in name or "\x00" in name:
        raise SystemExit(f"Unsafe Terrain3D archive path: {name!r}")
    path = PurePosixPath(name.rstrip("/"))
    if (
        path.is_absolute()
        or not path.parts
        or any(part in ("", ".", "..") for part in path.parts)
    ):
        raise SystemExit(f"Unsafe Terrain3D archive path: {name!r}")
    return path


def get_archive() -> None:
    CACHE.mkdir(parents=True, exist_ok=True)
    if not ARCHIVE.is_file():
        print(f"Downloading official Terrain3D {VERSION} release...")
        build.download(URL, ARCHIVE)
    actual = file_sha256(ARCHIVE)
    if actual != SHA256:
        ARCHIVE.unlink(missing_ok=True)
        raise SystemExit(
            f"Terrain3D ZIP SHA-256 mismatch: expected {SHA256}, got {actual}"
        )


def install_runtime() -> None:
    """Install only the Linux runtime, icons, manifest and license, without editor scripts or C# wrappers."""
    get_archive()
    members = [
        "terrain.gdextension",
        "LICENSE.txt",
        "icons/terrain3d.svg",
        "bin/libterrain.linux.debug.x86_64.so",
        "bin/libterrain.linux.release.x86_64.so",
    ]
    stamp = RUNTIME / ".release-sha256"
    if (
        stamp.is_file()
        and stamp.read_text().strip() == SHA256
        and all((RUNTIME / name).is_file() for name in members)
    ):
        return
    with zipfile.ZipFile(ARCHIVE) as archive:
        for name in members:
            info = archive.getinfo("addons/terrain_3d/" + name)
            safe_member(info.filename)
            if stat.S_ISLNK(info.external_attr >> 16):
                raise SystemExit(f"Symlink in Terrain3D runtime archive: {name}")
            destination = RUNTIME / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(archive.read(info))
    stamp.write_text(SHA256 + "\n")
    print(
        f"Installed verified Terrain3D {VERSION} Linux runtime into {RUNTIME.relative_to(ROOT)}"
    )


def install_addon() -> None:
    get_archive()
    with zipfile.ZipFile(ARCHIVE) as archive:
        chosen: dict[str, zipfile.ZipInfo] = {}
        prefix = "addons/terrain_3d/"
        for info in archive.infolist():
            safe_member(info.filename)
            if not info.is_dir() and info.filename.startswith(prefix):
                mode = info.external_attr >> 16
                if stat.S_ISLNK(mode):
                    raise SystemExit(
                        f"Symlink in Terrain3D archive is not allowed: {info.filename}"
                    )
                chosen[info.filename] = info
        expected = {
            prefix + "terrain.gdextension",
            prefix + "LICENSE.txt",
            prefix + "bin/libterrain.linux.release.x86_64.so",
            prefix + "csharp/Terrain3D.cs",
        }
        missing = sorted(expected - chosen.keys())
        if missing:
            raise SystemExit(
                "Terrain3D release is missing required files: " + ", ".join(missing)
            )

        if ADDON.exists():
            shutil.rmtree(ADDON)
        for name, info in chosen.items():
            relative = safe_member(name).relative_to("addons/terrain_3d")
            destination = ADDON.joinpath(*relative.parts)
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(archive.read(info))

    PROJECT.mkdir(parents=True, exist_ok=True)
    (PROJECT / "project.godot").write_text(PROJECT_GODOT, encoding="utf-8")
    (PROJECT / "Terrain3DProbe.csproj").write_text(CSPROJ, encoding="utf-8")
    (PROJECT / "Probe.cs").write_text(PROBE_CS, encoding="utf-8")
    (PROJECT / "Probe.tscn").write_text(TSCN, encoding="utf-8")
    print(f"Installed verified Terrain3D ZIP into {ADDON.relative_to(ROOT)}")


def run_probe() -> None:
    env = build.ensure_dotnet()
    godot = build.GODOT
    if not godot.is_file():
        raise SystemExit(
            f"Godot executable is missing: {godot}; run ./build.py setup first"
        )
    build.command(
        [
            "dotnet",
            "build",
            "--nologo",
            "-v",
            "quiet",
            PROJECT / "Terrain3DProbe.csproj",
        ],
        env=env,
    )
    build.run_godot(
        "terrain3d-probe",
        [godot, "--headless", "--path", PROJECT],
        env,
        timeout=120,
        success_marker="TERRAIN3D-PROBE PASS",
    )


def main() -> None:
    install_runtime()
    if "--probe" in sys.argv[1:]:
        install_addon()
        run_probe()


if __name__ == "__main__":
    main()
