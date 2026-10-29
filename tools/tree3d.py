#!/usr/bin/env python3
"""Bake reproducible Tree3D meshes into the project assets.

The native addon is downloaded from a pinned upstream release and kept under
the ignored .tools directory. The bake script itself lives in tools/ and is
copied into an isolated Godot project before execution.
"""

from __future__ import annotations

import hashlib
import shutil
import stat
import sys
import zipfile
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))
import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.

RELEASE_URL = (
    "https://github.com/JekSun97/gdTree3D/releases/download/v1.1.0/"
    "Tree3D-addon-godot4.5.zip"
)
RELEASE_SHA256 = "fb12f381ec19d1e9eb6339f8df85da474e9255ba0f86e5251df5d4599cc1b42d"
CACHE = ROOT / ".tools" / "tree3d"
ARCHIVE = CACHE / "Tree3D-addon-godot4.5-v1.1.0.zip"
ADDON = CACHE / "addons" / "Tree3D"
BAKE_SCRIPT = ROOT / "tools" / "bake_trees.gd"
OUTPUT = ROOT / "assets" / "models" / "tree3d"

PROJECT_SOURCE = """; Isolated temporary project used to bake Tree3D models.
config_version=5

[application]
config/name="Tree3D Offline Baker"

[rendering]
renderer/rendering_method="gl_compatibility"
renderer/rendering_method.mobile="gl_compatibility"
"""

FILES = (
    "Tree3D/Tree3D.gdextension",
    "Tree3D/ico/Tree3D.png",
    "Tree3D/libTree3D.linux.template_debug.x86_64.so",
    "Tree3D/libTree3D.linux.template_release.x86_64.so",
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def validate_zip_member(name: str) -> PurePosixPath:
    """Reject unsafe archive paths, including entries we do not extract."""
    if "\\" in name or "\x00" in name:
        raise SystemExit(f"Unsafe path in Tree3D archive: {name!r}")
    path = PurePosixPath(name)
    if path.is_absolute() or any(part in ("", ".", "..") for part in path.parts):
        raise SystemExit(f"Unsafe path in Tree3D archive: {name!r}")
    return path


def install_addon() -> None:
    CACHE.mkdir(parents=True, exist_ok=True)
    if not ARCHIVE.is_file():
        print("Downloading pinned gdTree3D v1.1.0 addon...")
        build.download(RELEASE_URL, ARCHIVE)

    actual = sha256(ARCHIVE)
    if actual != RELEASE_SHA256:
        ARCHIVE.unlink(missing_ok=True)
        raise SystemExit(
            "gdTree3D v1.1.0 archive SHA-256 mismatch: "
            f"expected {RELEASE_SHA256}, got {actual}"
        )

    with zipfile.ZipFile(ARCHIVE) as archive:
        members: dict[str, zipfile.ZipInfo] = {}
        for info in archive.infolist():
            validate_zip_member(info.filename.rstrip("/"))
            mode = info.external_attr >> 16
            if stat.S_ISLNK(mode):
                raise SystemExit(
                    f"Symlink in Tree3D archive is not allowed: {info.filename}"
                )
            if not info.is_dir():
                members[info.filename] = info
        missing = [name for name in FILES if name not in members]
        if missing:
            raise SystemExit(
                "Tree3D archive is missing required files: " + ", ".join(missing)
            )

        # Clear stale extraction products so the cache always reflects this exact release.
        if ADDON.exists():
            shutil.rmtree(ADDON)
        for member in FILES:
            relative = validate_zip_member(member).relative_to("Tree3D")
            destination = ADDON.joinpath(*relative.parts)
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(archive.read(members[member]))

    # Godot resolves the icon declared in the extension metadata during import.
    project = CACHE / "project.godot"
    project.write_text(PROJECT_SOURCE, encoding="utf-8")
    (CACHE / "bake_trees.gd").write_bytes(BAKE_SCRIPT.read_bytes())


def main() -> None:
    if not BAKE_SCRIPT.is_file():
        raise SystemExit(f"Missing bake script: {BAKE_SCRIPT}")

    install_addon()
    env = build.setup()
    build.run_godot(
        "tree3d-import",
        [build.GODOT, "--headless", "--path", CACHE, "--import"],
        env,
    )
    build.run_godot(
        "tree3d-bake",
        [
            build.GODOT,
            "--headless",
            "--path",
            CACHE,
            "--script",
            "res://bake_trees.gd",
            "--",
            OUTPUT,
        ],
        env,
        timeout=300,
        success_marker="TREE3D-BAKE PASS",
    )
    print(f"Baked Tree3D assets into {OUTPUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
