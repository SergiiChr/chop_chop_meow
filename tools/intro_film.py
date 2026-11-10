#!/usr/bin/env python3
"""Draw the intro film's frames in the game, with the cue sheet of its sounds, for a movie to be made from."""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.

OUTPUT = build.BUILD / "intro"
MARKER = "INTRO RENDERED"


def render(folder: Path, stills: str | None) -> None:
    """Plays the film in the game and saves its frames, or only the frames at the given seconds."""
    shutil.rmtree(folder, ignore_errors=True)
    folder.mkdir(parents=True)
    flags = ["--render-intro", str(folder)]
    if stills:
        flags += ["--intro-stills", stills]
    log = folder.with_suffix(".log")
    # A native Wayland window draws about one frame a second; XWayland draws at full speed.
    env = {key: value for key, value in os.environ.items() if key != "WAYLAND_DISPLAY"}
    with log.open("w") as output:
        result = subprocess.run(
            [sys.executable, ROOT / "build.py", "play", "--", *flags],
            cwd=ROOT,
            env=env,
            stdout=output,
            stderr=subprocess.STDOUT,
            timeout=1800,
            check=False,
        )
    text = log.read_text(errors="replace")
    errors = list(
        dict.fromkeys(
            line.strip() for line in text.splitlines() if build.ERRORS.search(line)
        )
    )
    if errors:
        print("\n".join(errors[:20]), file=sys.stderr)
    if result.returncode or MARKER not in text:
        raise SystemExit(
            f"The film did not render (exit {result.returncode}); see {log.relative_to(ROOT)}"
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
    render(folder, args.stills)
    print(
        f"{'Stills' if args.stills else 'Frames'} and cue sheet: {folder.relative_to(ROOT)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
