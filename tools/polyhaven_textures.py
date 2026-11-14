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
        "8k",
        "492b89604cd79f733557b590f561ddc9ea51739705d96d9768c4ca1614b44b60",
        "0a59f38f2dd43985619d3574b9ec67845cce5e86baa9a4767333ab7f539e635f",
        "1ad0d03acc97a6b93b6582e0c78ab37f090f3e1031770eef0b4d134ab75c98d4",
    ),
    ("environment/lake", "rocks"): (
        "coast_sand_rocks_02",
        "8k",
        "d1973fe3971f3ace9ed514b19077f6fca393446fa5402108346af9b25f5d2b19",
        "e924fa9f0eaaab4ddc162954fd30164912694146a0af3c7d9f083559b54fa6cd",
        "de6b3edec35804fab6a80a13676ba464e62987fd35c70b3517b2eda313db2fe5",
    ),
    ("environment/terrain3d", "path"): (
        "brown_mud_leaves_01",
        "2k",
        "29c997d77f56e4ea16d3e998a306be0c17fddb28ab4822657c7cd8249040ffda",
        "b6ec386e00d9d2a1c3a1da6a98a67066b0dfaa88ddb779341df29700cf081bdf",
        "3688c1acc705f604ffdaf956064c898658bcd2d8b0258e437e671390c896abb8",
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
