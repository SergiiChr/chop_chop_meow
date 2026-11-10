#!/usr/bin/env python3
"""Draw the intro film's frames in the game, with the cue sheet of its sounds, for a movie to be made from."""

from __future__ import annotations

import argparse
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.

OUTPUT = build.BUILD / "intro"
MARKER = "INTRO RENDERED"


def render(env: dict[str, str], folder: Path, stills: str | None) -> None:
    """Plays the film in the game and saves its frames, or only the frames at the given seconds."""
    shutil.rmtree(folder, ignore_errors=True)
    folder.mkdir(parents=True)
    flags: list[str | Path] = ["--render-intro", folder]
    if stills:
        flags += ["--intro-stills", stills]
    # A native Wayland window draws about one frame a second; XWayland draws at full speed.
    window = {key: value for key, value in env.items() if key != "WAYLAND_DISPLAY"}
    # The optimized release export draws the film several times faster than the debug build.
    build.run_godot(
        f"intro-{folder.name}",
        [build.export_play(env), "--", *flags],
        window,
        timeout=1800,
        success_marker=MARKER,
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--stills",
        help="save only the frames at these seconds, such as 1.5,9.2, in build/intro/stills",
    )
    args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    folder = OUTPUT / ("stills" if args.stills else "frames")
    render(build.setup(), folder, args.stills)
    print(
        f"{'Stills' if args.stills else 'Frames'} and cue sheet: {folder.relative_to(ROOT)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
