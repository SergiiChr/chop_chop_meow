#!/usr/bin/env python3
"""Download the pinned CC0 Poly Haven photoscans the shore shaders draw, used as Poly Haven serves them."""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.

# Shore name in the game, the Poly Haven asset, its JPEG size and the SHA-256 of its colour and OpenGL normal maps.
SETS = {
    "sand": (
        "aerial_beach_01",
        "4k",
        "231f7dfdbc79ec5ccb128616ed601aacc2b323204148480114bf662b09f79ea8",
        "0eeff8b0899968fce619b2d0d39c656df13149e319add95041243652596242fd",
    ),
    "rocks": (
        "aerial_rocks_01",
        "2k",
        "25edb78d0c3daf52d44bbd076c74ef53865fc4cd386a922736cb167313420a7b",
        "570650b9c9bc26b541cbd09754ab6a6b3d198bb44ae964e3877f262d3c96567d",
    ),
}
OUTPUT = ROOT / "assets" / "environment" / "lake"
SOURCE = "https://dl.polyhaven.org/file/ph-assets/Textures/jpg"


def main() -> None:
    for name, (asset, size, albedo, normal) in SETS.items():
        for map_name, upstream, sha256 in (
            ("albedo", "diff", albedo),
            ("normal", "nor_gl", normal),
        ):
            build.fetch_pinned(
                f"{SOURCE}/{size}/{asset}/{asset}_{upstream}_{size}.jpg",
                OUTPUT / f"{name}_{map_name}.jpg",
                sha256,
            )


if __name__ == "__main__":
    main()
