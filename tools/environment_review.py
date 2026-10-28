#!/usr/bin/env python3
"""Capture matched before/after images for the three environment trials."""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.
from tools import terrain3d  # noqa: E402


OUTPUT = ROOT / "docs" / "reviews" / "environment"

CAPTURES = (
    (
        "placement-before",
        "forest",
        (),
        "01-placement-before.png",
    ),
    (
        "placement-after",
        "forest",
        ("--proton-scatter",),
        "01-placement-after.png",
    ),
    (
        "lake-before",
        "lake",
        ("--proton-scatter",),
        "02-lake-before.png",
    ),
    (
        "lake-after",
        "lake",
        ("--proton-scatter", "--lake-water"),
        "02-lake-after.png",
    ),
    (
        "terrain-before",
        "terrain",
        ("--proton-scatter", "--lake-water"),
        "03-terrain-before.png",
    ),
    (
        "terrain-after",
        "terrain",
        ("--proton-scatter", "--lake-water", "--terrain3d"),
        "03-terrain-after.png",
    ),
)

SHORELINE_CAPTURES = (
    ("coast-before", "coast", ("--proton-scatter", "--lake-water", "--terrain3d", "--shoreline-baseline"), "shoreline/coast-before.png"),
    ("coast-after", "coast", ("--proton-scatter", "--lake-water", "--terrain3d"), "shoreline/coast-after.png"),
    ("lake-edge-before", "lake", ("--proton-scatter", "--lake-water", "--terrain3d", "--shoreline-baseline"), "shoreline/lake-before.png"),
    ("lake-edge-after", "lake", ("--proton-scatter", "--lake-water", "--terrain3d"), "shoreline/lake-after.png"),
    *((f"mine-{ore}-after", "mine", ("--proton-scatter", "--lake-water", "--terrain3d", "--mine-ore", ore), f"shoreline/{ore}-after.png") for ore in ("coal", "iron", "tin")),
)


def capture(
    env: dict[str, str],
    name: str,
    location: str,
    flags: tuple[str, ...],
    filename: str,
) -> None:
    image = (OUTPUT / filename).resolve()
    image.parent.mkdir(parents=True, exist_ok=True)
    args = [
        "--showcase",
        "--environment-review",
        location,
        "--tree3d",
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
    OUTPUT.mkdir(parents=True, exist_ok=True)
    # Install the optional native runtime before build.setup imports the game.
    terrain3d.install_runtime()
    env = build.setup()

    captures = SHORELINE_CAPTURES if "--shoreline" in sys.argv[1:] else CAPTURES
    for name, location, flags, filename in captures:
        capture(env, name, location, flags, filename)

    print(f"Environment review capture pass: {len(captures)} screenshots")


if __name__ == "__main__":
    main()
