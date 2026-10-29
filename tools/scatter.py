#!/usr/bin/env python3
"""Bake deterministic environment placement transforms with pinned ProtonScatter.

The addon is extracted into ignored .tools/scatter and run in an isolated
Godot project. Only its transform data is committed; the game does not load
the editor addon or recreate the scatter at runtime.
"""

from __future__ import annotations

import shutil
import sys
import tempfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))
import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.

COMMIT = "7198cc00078ebde61307578137c3f35223c6e438"
ARCHIVE_URL = f"https://codeload.github.com/HungryProton/scatter/zip/{COMMIT}"
ARCHIVE_SHA256 = "485295fa4a97cb54bcd20d1b1304ed8a2fc0b28fe3215a07be62d26df9208d5f"
CACHE = ROOT / ".tools" / "scatter"
ARCHIVE = CACHE / "scatter-7198cc0.zip"
ADDON = CACHE / "addons" / "proton_scatter"
BAKE_SCRIPT = ROOT / "tools" / "bake_scatter.gd"
OUTPUT = ROOT / "assets" / "environment" / "proton_scatter" / "placements.json"

PROJECT_SOURCE = """; Isolated project for the ProtonScatter offline placement bake.
config_version=5

[application]
config/name="ProtonScatter Offline Baker"

[rendering]
renderer/rendering_method="gl_compatibility"
renderer/rendering_method.mobile="gl_compatibility"

[filesystem]
import/blender/enabled=false
"""


def install_addon() -> None:
    build.fetch_pinned(ARCHIVE_URL, ARCHIVE, ARCHIVE_SHA256)
    top = f"scatter-{COMMIT}/"
    with zipfile.ZipFile(ARCHIVE) as archive:
        members = [
            info
            for info in archive.infolist()
            if info.filename.startswith(top + "addons/proton_scatter/")
            and not info.is_dir()
        ]
        for info in members:
            info.filename = info.filename.removeprefix(top)
        if ADDON.exists():
            shutil.rmtree(ADDON)
        archive.extractall(ADDON.parent.parent, members)


def main() -> None:
    if not BAKE_SCRIPT.is_file():
        raise SystemExit(f"Missing bake script: {BAKE_SCRIPT}")

    install_addon()
    build.fetch_godot()
    build.BUILD.mkdir(exist_ok=True)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    # Godot ignores projects nested under the repository's .tools/.gdignore.
    # Stage the isolated project in a temporary directory, keeping the pinned
    # addon cache in .tools and the generated JSON in the committed asset path.
    with tempfile.TemporaryDirectory(prefix="chop-chop-scatter-") as temporary:
        project = Path(temporary) / "project"
        project.mkdir()
        shutil.copytree(ADDON, project / "addons" / "proton_scatter")
        (project / "project.godot").write_text(PROJECT_SOURCE, encoding="utf-8")
        (project / "bake_scatter.gd").write_bytes(BAKE_SCRIPT.read_bytes())

        env = build.dotnet_env()
        env["XDG_DATA_HOME"] = str(Path(temporary) / "xdg-data")
        env["XDG_CONFIG_HOME"] = str(Path(temporary) / "xdg-config")
        env["XDG_CACHE_HOME"] = str(Path(temporary) / "xdg-cache")
        build.run_godot(
            "scatter-import",
            [build.GODOT, "--headless", "--path", project, "--import"],
            env,
            timeout=180,
        )
        build.run_godot(
            "scatter-bake",
            [
                build.GODOT,
                "--headless",
                "--path",
                project,
                "--script",
                "res://bake_scatter.gd",
                "--",
                OUTPUT,
            ],
            env,
            timeout=300,
            success_marker="SCATTER-BAKE PASS",
        )
    print(f"Baked ProtonScatter placements into {OUTPUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
