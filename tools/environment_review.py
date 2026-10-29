#!/usr/bin/env python3
"""Capture current default environment views for visual review."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.

OUTPUT = build.BUILD / "environment-review"

CURRENT_CAPTURES = (
    ("forest", "forest", ()),
    ("lake", "lake", ()),
    ("terrain", "terrain", ()),
)

SHORELINE_CAPTURES = (
    ("coast", "coast", ()),
    ("lake-edge", "lake", ()),
    *((f"mine-{ore}", "mine", ("--mine-ore", ore)) for ore in ("coal", "iron", "tin")),
)


def capture(
    env: dict[str, str], name: str, location: str, flags: tuple[str, ...]
) -> None:
    image = (OUTPUT / f"{name}.png").resolve()
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
    for name, location, flags in captures:
        capture(env, name, location, flags)

    print(
        f"Environment review capture pass: {len(captures)} screenshots in {OUTPUT.relative_to(ROOT)}"
    )


if __name__ == "__main__":
    main()
