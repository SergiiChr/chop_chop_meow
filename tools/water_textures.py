#!/usr/bin/env python3
"""Download the pinned MIT-licensed textures of the Realistic Water Shader, as its demo project serves them."""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.

# The demo project's commit the files come from.
COMMIT = "940eaecc5a8cbcc2ca0117c16412695a6a389dcc"
SOURCE = (
    "https://raw.githubusercontent.com/godot-extended-libraries/godot-realistic-water/"
    f"{COMMIT}/realistic_water_shader/art/water"
)
# Name in the game, name upstream and SHA-256.
FILES = {
    "water_flow.png": (
        "Water_UV.png",
        "79ac9c7f813745c6297e99505a3edee233dc6a2c1e7bc1671b62a1cde6f47634",
    ),
    "water_normal_a.png": (
        "Water_N_A.png",
        "585ec979b3a446371d32655bd8c861c92f5111e9f7d8dc7f96a7e5537dd4b62e",
    ),
    "water_normal_b.png": (
        "Water_N_B.png",
        "8de93f4bfcc9857cb433b65fe5d41d0fd232e7759c73632df084e0836a84cc4b",
    ),
    "water_foam.png": (
        "Foam.png",
        "b341d1ecb580c6465daf04507124d89ce145e4a62449eea3c29766306c5cce7b",
    ),
    "water_caustics.png": (
        "Caustic.png",
        "613988d597c7dc892a0e47d0241bc461cc9480f0c1cc6e389cd8a7f33562c39a",
    ),
}
LAKE = ROOT / "assets" / "environment" / "lake"


def main() -> None:
    for name, (upstream, sha256) in FILES.items():
        build.fetch_pinned(f"{SOURCE}/{upstream}", LAKE / name, sha256)


if __name__ == "__main__":
    main()
