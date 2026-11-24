#!/usr/bin/env python3
"""Bake the pinned CC0 nettle plant and rope bundle the game shows as nettles and nettle rope."""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.

# Patch's asset library serves each model under the SHA-256 of its file.
SOURCE = "https://assets.patchusercontent.com/sha256"
# Poly Haven's nettle plant at its lightest level of detail: six sprigs that share one texture.
NETTLE = (
    "nettle_plant_lod2.glb",
    "7e44facbc9a1117940e2da61e6da41d48cf8dbeafb9df33257e5649c4a15de01",
)
# KayKit's rope bundle A, coloured from the kit's palette texture.
ROPE = (
    "rope_bundle_A.glb",
    "0436c349d94bd66ea633ec6f7228200cb256410ab8b93f42c821990876a46731",
)
# The sprigs' shared texture, and its size on each side in the game.
TEXTURE = "nettle_plant.png"
TEXTURE_SIZE = 512
CACHE = build.TOOLS / "nettle-models"
OUTPUT = ROOT / "assets" / "models" / "nettle"


def main() -> None:
    build.fetch_godot()
    build.BUILD.mkdir(exist_ok=True)
    CACHE.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, sha256 in (NETTLE, ROPE):
        build.fetch_pinned(f"{SOURCE}/{sha256}/{name}", CACHE / name, sha256)
    build.run_tool_script(
        ROOT / "tools" / "bake_cards.gd",
        [OUTPUT, CACHE / NETTLE[0], TEXTURE, str(TEXTURE_SIZE)],
        "CARD-BAKE PASS",
        timeout=300,
    )
    build.run_tool_script(
        ROOT / "tools" / "bake_models.gd",
        [OUTPUT, CACHE / ROPE[0]],
        "MODEL-BAKE PASS",
        timeout=300,
    )
    print(f"Baked the nettle sprigs and the rope bundle into {OUTPUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
