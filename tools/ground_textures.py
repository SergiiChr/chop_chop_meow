#!/usr/bin/env python3
"""Pack the pinned CC0 ground texture sets from ambientCG the way Terrain3D reads them."""

from __future__ import annotations

import shutil
import sys
import tempfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.

# Texture name in the game, the ambientCG asset it comes from and the SHA-256 of that asset's 1K PNG zip.
SETS = {
    "grass": (
        "Grass004",
        "9bdb8a28536f437e910824096dd8a3d8276d5186a27ad067dea4e2e762a6981b",
    ),
    "dirt": (
        "Ground037",
        "cbd75f0660870b3299a68c4fe7fd54efb3951cf992d4295961209619eb284c47",
    ),
    "rock": (
        "Rock030",
        "d3e0dc55fc46b093631f4d0009c934003c601e69df9aa4ba41a43db3807056ee",
    ),
}
MAPS = ("Color", "Displacement", "NormalGL", "Roughness")
# Half the source size: a tile spans a few world units and about a hundred screen pixels each.
SIZE = 512
CACHE = ROOT / ".tools" / "ambientcg"
OUTPUT = ROOT / "assets" / "environment" / "terrain3d" / "textures"
PACK_SCRIPT = ROOT / "tools" / "pack_ground.gd"

PROJECT_SOURCE = """; Isolated project for packing ground textures.
config_version=5

[application]
config/name="Ground texture packer"

[rendering]
renderer/rendering_method="gl_compatibility"
"""


def main() -> None:
    build.fetch_godot()
    build.BUILD.mkdir(exist_ok=True)
    CACHE.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    # Godot ignores projects nested under the repository's .tools/.gdignore, so the packer runs from a temporary
    # project.
    with tempfile.TemporaryDirectory(prefix="chop-chop-ground-") as temporary:
        source = Path(temporary) / "source"
        source.mkdir()
        for asset, sha256 in SETS.values():
            archive = CACHE / f"{asset}_1K-PNG.zip"
            build.fetch_pinned(
                f"https://ambientcg.com/get?file={archive.name}", archive, sha256
            )
            with zipfile.ZipFile(archive) as bundle:
                for name in MAPS:
                    bundle.extract(f"{asset}_1K-PNG_{name}.png", source)
        project = Path(temporary) / "project"
        project.mkdir()
        (project / "project.godot").write_text(PROJECT_SOURCE, encoding="utf-8")
        shutil.copy(PACK_SCRIPT, project / PACK_SCRIPT.name)
        env = build.isolated_env(Path(temporary))
        build.run_godot(
            "ground-pack",
            [
                build.GODOT,
                "--headless",
                "--path",
                project,
                "--script",
                f"res://{PACK_SCRIPT.name}",
                "--",
                source,
                OUTPUT,
                str(SIZE),
                *(f"{name}={asset}" for name, (asset, _) in SETS.items()),
            ],
            env,
            timeout=600,
            success_marker="GROUND-PACK PASS",
        )
    print(f"Packed {len(SETS)} ground texture sets into {OUTPUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
