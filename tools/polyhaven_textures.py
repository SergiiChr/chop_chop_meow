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
        "aerial_rocks_01",
        "4k",
        "df324eaacd84a7cd14d20a375d0c5902b2a07a0e3668979d6044d591fb88aa66",
        "298493b652ff4305d089262fdaf436ce970bceab9da33874c850a9535717df04",
        "d1452b4245574bae2005b8367458f540d74991d295c832eebc1a1c79d60ed148",
    ),
    ("environment/terrain3d", "mountain"): (
        "rocky_terrain_02",
        "8k",
        "ca2881ae0ad377d56129820aaf8e748617395baab7c9fae222802560f3e1b0e7",
        "17fbde9f1a18298a3ce1498dbf18f710889b31d4c05df2ac0cd3bf4b27b0e6ef",
        "b8ebba9e2761f630c22df1d0b91807e62a339e950bd7df3cff8f0afc984e7b55",
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
