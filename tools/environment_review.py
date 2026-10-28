#!/usr/bin/env python3
"""Capture current default environment views for visual review."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.


OUTPUT = ROOT / "docs" / "reviews" / "environment"

CURRENT_CAPTURES = (
    ("forest", "forest", (), "01-placement-after.png"),
    ("lake", "lake", (), "02-lake-after.png"),
    ("terrain", "terrain", (), "03-terrain-after.png"),
)

SHORELINE_CAPTURES = (
    ("coast", "coast", (), "shoreline/coast-after.png"),
    ("lake-edge", "lake", (), "shoreline/lake-after.png"),
    *((f"mine-{ore}", "mine", ("--mine-ore", ore), f"shoreline/{ore}-after.png") for ore in ("coal", "iron", "tin")),
)


def capture(
    env: dict[str, str], name: str, location: str, flags: tuple[str, ...], filename: str
) -> None:
    image = (OUTPUT / filename).resolve()
    image.parent.mkdir(parents=True, exist_ok=True)
    args = [
        "--showcase",
        "--environment-review",
        location,
        *flags,
        "--shot-delay",
        "2",
        "--screenshot",
        str(image),
    ]
    build.run_godot(
        f"environment-{name}",
        [build.GODOT, "--path", ROOT, "--", *args],
        env,
        timeout=120,
        success_marker="Screenshot:",
    )
    print(f"Captured {image.relative_to(ROOT)} (build/environment-{name}.log)")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--shoreline",
        action="store_true",
        help="capture current coast, lake-edge, and mine views instead of landscape views",
    )
    options = parser.parse_args()

    OUTPUT.mkdir(parents=True, exist_ok=True)
    env = build.setup()
    captures = SHORELINE_CAPTURES if options.shoreline else CURRENT_CAPTURES
    for name, location, flags, filename in captures:
        capture(env, name, location, flags, filename)

    print(f"Environment review capture pass: {len(captures)} current screenshots")


if __name__ == "__main__":
    main()
