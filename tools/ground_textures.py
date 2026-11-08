#!/usr/bin/env python3
"""Pack the pinned CC0 ground texture sets from ambientCG the way Terrain3D reads them."""

from __future__ import annotations

import sys
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
OUTPUT = ROOT / "assets" / "environment" / "terrain3d" / "textures"


def main() -> None:
    build.pack_photoscans(SETS, MAPS, SIZE, OUTPUT, "ground")


if __name__ == "__main__":
    main()
