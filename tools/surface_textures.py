#!/usr/bin/env python3
"""Pack the pinned CC0 photoscans the model kit tints as its wood, metal, stone, rock, cloth, earth, soil and foliage surfaces."""

from __future__ import annotations

import sys
from pathlib import Path
from typing import Literal

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.

# Surface name in the game, the ambientCG asset it comes from and the SHA-256 of that asset's 1K PNG zip.
SETS = {
    "wood": (
        "Wood049",
        "5859189c2be4588287aa84e77adbd38ba4a7d219122a31ee65674de72cb3e74b",
    ),
    "metal": (
        "Metal038",
        "c3769d6af2ed27c47bd331c9d41d16acbe20bbc3fea926b44d3d2a7724fe96f9",
    ),
    "stone": (
        "Rock030",
        "d3e0dc55fc46b093631f4d0009c934003c601e69df9aa4ba41a43db3807056ee",
    ),
    "rock": (
        "Rock010",
        "542955172a445235c2ea95029338b9fc250d5ecb72202a628aa09a6a3aea1a88",
    ),
    "cloth": (
        "Fabric061",
        "fdab47618c56a6ecdffc3ed9131ecba8d21e3a35bcd3932f1c5625cc37514be4",
    ),
    "earth": (
        "Ground037",
        "cbd75f0660870b3299a68c4fe7fd54efb3951cf992d4295961209619eb284c47",
    ),
    "foliage": (
        "Moss002",
        "97010f72c7a32fa1e1242cfe872822c7c8b2ac445b47aa2e53cc877ccb2ec87a",
    ),
}
# Surface name in the game, the Poly Haven asset it comes from and the SHA-256 of its 1K PNG colour and normal maps.
POLYHAVEN = {
    "soil": (
        "red_laterite_soil_stones",
        "96f3e55c2cf202e609faea9234847805563375d8edf466fb30f06413dda1f2a1",
        "7cc7f835a3242574e085bfd948234d025fe81714cfda110d57456a914bae66a9",
    ),
}
MAPS = ("Color", "NormalGL")
# Cloth scans are nearly even at the game's distance, so the weave keeps all of its contrast to show at all.
# Soil and rock keep their colours: models take the scan's mean colour, so they show the photoscan as it is.
DETAIL: dict[str, float | Literal["colour"]] = {
    "cloth": 1.0,
    "rock": "colour",
    "soil": "colour",
}
# A quarter of the source size: a tile spans a model or two and a model is well under a hundred screen pixels.
SIZE = 256
OUTPUT = ROOT / "assets" / "models" / "surfaces"


def main() -> None:
    build.pack_photoscans(SETS, MAPS, SIZE, OUTPUT, "surface", DETAIL, POLYHAVEN)
    # The shrub shader lays on the foliage grain alone, so its bump stays out of the game.
    (OUTPUT / "foliage_bump.png").unlink()


if __name__ == "__main__":
    main()
