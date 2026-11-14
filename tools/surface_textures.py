#!/usr/bin/env python3
"""Pack the pinned CC0 photoscans of the model kit's surfaces and of the rock, coal and soil it draws as they are."""

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
        "Rock026",
        "be5e6dc252ff1c17352db9c19e354b57f0414e79146c8431648cb6e640dc0da5",
    ),
    "coal": (
        "Rock035",
        "e745b558d754962ac44162ccee8805d7dba84ecdc428a543c3e552bcb28f8b85",
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
# Surface name in the game, the Poly Haven asset it comes from and the SHA-256 of its 1K PNG colour, normal and height
# maps.
POLYHAVEN = {
    "soil": (
        "red_laterite_soil_stones",
        "96f3e55c2cf202e609faea9234847805563375d8edf466fb30f06413dda1f2a1",
        "7cc7f835a3242574e085bfd948234d025fe81714cfda110d57456a914bae66a9",
        "525a0b0d8de227188ff9a83a5581c7744d342373c29d54d5b8e6b30c02c6c738",
    ),
}
MAPS = ("Color", "NormalGL", "Displacement")
# Cloth scans are nearly even at the game's distance, so the weave keeps all of its contrast to show at all.
# Rock, coal and soil are drawn as the photoscans are, with parallax.
DETAIL: dict[str, float | Literal["scan"]] = {
    "cloth": 1.0,
    "rock": "scan",
    "coal": "scan",
    "soil": "scan",
}
# A quarter of the source size: a tile spans a model or two and a model is well under a hundred screen pixels.
# Scans drawn as they are ignore it and keep the source size.
SIZE = 256
OUTPUT = ROOT / "assets" / "models" / "surfaces"


def main() -> None:
    build.pack_photoscans(SETS, MAPS, SIZE, OUTPUT, "surface", DETAIL, POLYHAVEN)
    # The shrub shader lays on the foliage grain alone, so its bump stays out of the game.
    (OUTPUT / "foliage_bump.png").unlink()


if __name__ == "__main__":
    main()
