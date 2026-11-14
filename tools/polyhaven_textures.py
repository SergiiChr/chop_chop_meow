#!/usr/bin/env python3
"""Download the pinned CC0 Poly Haven photoscans the game draws with their own colours, as Poly Haven serves them."""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.

# Folder and name in the game, the Poly Haven asset, its JPEG size and the SHA-256 of its colour, OpenGL normal and
# height maps.
SETS = {
    ("environment/lake", "sand"): (
        "aerial_beach_01",
        "2k",
        "1803d7e2861b304bea6d2e124dcd4c657fcb93843b84c17bb2db98ce48a87e4e",
        "0f35185b7225fe2027897bb7e4586c7deb048817524f1ed104fdae7ce80bdc06",
        "73c4795adc4232511b1810995eb6740295ff6d713df5b57573f9413565231f20",
    ),
    ("environment/lake", "rocks"): (
        "coast_sand_rocks_02",
        "2k",
        "98a8850f24cba18e4c99e46c56f5675f910809ee7f75df3e8587ec9e27088ed6",
        "e75cd105d218aae5bf669630635e6753a78f6325bc799be3d0ee8b974853a84d",
        "109dfa81b1085e1b1d32ba740c0efb04b06e0b497925ef8e58e9c3c0e6753617",
    ),
    ("environment/terrain3d", "path"): (
        "brown_mud_leaves_01",
        "2k",
        "29c997d77f56e4ea16d3e998a306be0c17fddb28ab4822657c7cd8249040ffda",
        "b6ec386e00d9d2a1c3a1da6a98a67066b0dfaa88ddb779341df29700cf081bdf",
        "3688c1acc705f604ffdaf956064c898658bcd2d8b0258e437e671390c896abb8",
    ),
    ("environment/terrain3d", "forest"): (
        "forrest_ground_01",
        "2k",
        "e4d557ff61a0b42712a58e8a2e91c6010738977d4e172b7af47abd7d406ae735",
        "aa23fa930c6303490f842f7c016c19d838263e8e4538d42508a9332e3fd76b69",
        "1d5c63d39e67af44e96cc9d5752273982bc5c82c9855695dada4abc2fc20575d",
    ),
    ("environment/terrain3d", "moss"): (
        "aerial_grass_rock",
        "4k",
        "84bf24b80d097c1a38d0fbe5c84584b1ede3e2ab06b4af2721fd184a9f94d5a3",
        "3a9fca5d7886ab56a8d73d4ad10185a28d40b9eea6200e9c122e7af52dc5ba9f",
        "eebf74c37905a41938a236767d1a3e3f3e0090f6ef7e0c30ed9c8943ceaa46a9",
    ),
}
ASSETS = ROOT / "assets"
SOURCE = "https://dl.polyhaven.org/file/ph-assets/Textures/jpg"


def main() -> None:
    for (folder, name), (asset, size, albedo, normal, height) in SETS.items():
        for map_name, upstream, sha256 in (
            ("albedo", "diff", albedo),
            ("normal", "nor_gl", normal),
            ("height", "disp", height),
        ):
            build.fetch_pinned(
                f"{SOURCE}/{size}/{asset}/{asset}_{upstream}_{size}.jpg",
                ASSETS / folder / f"{name}_{map_name}.jpg",
                sha256,
            )


if __name__ == "__main__":
    main()
