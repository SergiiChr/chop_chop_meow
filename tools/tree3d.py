#!/usr/bin/env python3
"""Bake reproducible Tree3D meshes into the project assets.

The native addon is downloaded from a pinned upstream release and kept under
the ignored .tools directory. The bake script itself lives in tools/ and is
copied into an isolated Godot project before execution.
"""

from __future__ import annotations

import shutil
import sys
import zipfile
from pathlib import Path

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


def install_addon() -> None:
    build.fetch_pinned(RELEASE_URL, ARCHIVE, RELEASE_SHA256)
    # Clear stale extraction products so the cache always reflects this exact release.
    if ADDON.exists():
        shutil.rmtree(ADDON)
    with zipfile.ZipFile(ARCHIVE) as archive:
        archive.extractall(ADDON.parent, FILES)

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
