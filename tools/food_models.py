#!/usr/bin/env python3
"""Bake the pinned CC0 Kenney Food Kit models the game shows as food into meshes that carry their own colours."""

from __future__ import annotations

import shutil
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.

# Patch's asset library serves each model under the SHA-256 of its file.
SOURCE = "https://assets.patchusercontent.com/sha256"
# Kenney's name for the model and the SHA-256 of Patch's copy of it.
MODELS = {
    "loaf-round": "702178314b09184381ffccebba4fa5c4684f63e882b58a078143e17b49bf8416",
    "wholer-ham": "b4f85d50bb8ab92d7b3fe799e0816235c2ec4edf005f36f43c089ca39f938dfc",
    "meat-cooked": "286eafd15f7f0e48f2f5a8d4e8b0cd52ef1e53e8ef1c784805c551b407391369",
    "bowl-soup": "6be05262d7ce4f8faf6e28680ea63b3ecf53aab67394493a04407d7e371372e6",
}
CACHE = build.TOOLS / "kenney-food"
BAKE_SCRIPT = ROOT / "tools" / "bake_models.gd"
OUTPUT = ROOT / "assets" / "models" / "food"


def main() -> None:
    build.fetch_godot()
    build.BUILD.mkdir(exist_ok=True)
    CACHE.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, sha256 in MODELS.items():
        build.fetch_pinned(
            f"{SOURCE}/{sha256}/{name}.glb", CACHE / f"{name}.glb", sha256
        )
    # Godot ignores projects nested under the repository's .tools/.gdignore, so the baker runs from a temporary
    # project.
    with tempfile.TemporaryDirectory(prefix="chop-chop-models-") as temporary:
        project = Path(temporary) / "project"
        project.mkdir()
        (project / "project.godot").write_text(build.PACKER_PROJECT, encoding="utf-8")
        shutil.copy(BAKE_SCRIPT, project / BAKE_SCRIPT.name)
        build.run_godot(
            BAKE_SCRIPT.stem,
            [
                build.GODOT,
                "--headless",
                "--path",
                project,
                "--script",
                f"res://{BAKE_SCRIPT.name}",
                "--",
                OUTPUT,
                *(CACHE / f"{name}.glb" for name in MODELS),
            ],
            build.isolated_env(Path(temporary)),
            timeout=300,
            success_marker="MODEL-BAKE PASS",
        )
    print(f"Baked {len(MODELS)} models into {OUTPUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
