#!/usr/bin/env python3
"""Capture one animation clip from several camera views, optionally from another branch too, for side-by-side review."""

from __future__ import annotations

import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import build  # noqa: E402  Importing build.py is safe; its CLI is guarded.

OUTPUT = build.BUILD / "animation-review"
MARKER = "ANIMATION-CAPTURE PASS"

# Camera pitch and rig turn in degrees: low from the side, low facing the camera, and the game's own angle.
VIEWS = {"side": (4, 0), "front": (4, -90), "game": (30, 0)}


def review_source(ref: str | None) -> str:
    """Reads the review's source from the working tree, or from a ref when one is given."""
    if ref is None:
        return (ROOT / "src" / "WorldScene.cs").read_text()
    show = ["git", "show", f"{ref}:src/WorldScene.cs"]
    result = subprocess.run(show, cwd=ROOT, capture_output=True, text=True, check=False)
    if result.returncode:
        raise SystemExit(result.stderr.strip())
    return result.stdout


def require_clip(ref: str | None, clip: str) -> None:
    """Stops when a checkout has no close-up review or no clip of that name, which would capture every clip instead."""
    source, where = review_source(ref), ref or "this checkout"
    if "--animation-clip" not in source:
        raise SystemExit(f"{where} has no --animation-clip close-up review")
    names = re.findall(r'\("([^"]+)", ActorSkin\.', source)
    wolves = re.search(r"WolfClips = \{([^}]*)\}", source)
    names += re.findall(r'"([^"]+)"', wolves.group(1)) if wolves else []
    if clip not in names:
        raise SystemExit(f"No clip {clip!r} in {where}; clips: {', '.join(names)}")


def capture(checkout: Path, clip: str, view: str, slow: float, folder: Path) -> None:
    pitch, turn = VIEWS[view]
    shutil.rmtree(folder, ignore_errors=True)
    folder.mkdir(parents=True)
    log = folder.with_suffix(".log")
    flags = [
        "--animation-review",
        "--animation-clip",
        clip,
        "--animation-pitch",
        str(pitch),
        "--animation-turn",
        str(turn),
        "--animation-slow",
        str(slow),
        "--animation-capture",
        str(folder),
    ]
    # A native Wayland window draws about one frame a second; XWayland draws at full speed.
    env = {key: value for key, value in os.environ.items() if key != "WAYLAND_DISPLAY"}
    with log.open("w") as output:
        result = subprocess.run(
            [sys.executable, checkout / "build.py", "play", "--", *flags],
            cwd=checkout,
            env=env,
            stdout=output,
            stderr=subprocess.STDOUT,
            timeout=600,
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
            f"Capture of {view} failed (exit {result.returncode}); see {log}"
        )
    # A machine without a GPU or sound device logs engine errors yet saves every frame, so they only warn.
    print(
        f"Captured {folder}"
        + (f" with the engine errors above; see {log}" if errors else "")
    )


def capture_all(checkout: Path, name: str, options: argparse.Namespace) -> None:
    for view in options.views:
        capture(checkout, options.clip, view, options.slow, OUTPUT / name / view)


def capture_base(ref: str, options: argparse.Namespace) -> None:
    """Captures a ref from a temporary worktree that shares this checkout's tools."""
    with tempfile.TemporaryDirectory(prefix="animation-base-") as folder:
        checkout = Path(folder) / "checkout"
        subprocess.run(
            ["git", "worktree", "add", "--detach", checkout, ref], cwd=ROOT, check=True
        )
        try:
            (checkout / ".tools").symlink_to(build.TOOLS.resolve())
            capture_all(checkout, re.sub(r"[^\w.-]", "-", ref), options)
        finally:
            subprocess.run(
                ["git", "worktree", "remove", "--force", checkout], cwd=ROOT, check=True
            )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "clip", help='a clip name from the review, such as "player run"'
    )
    parser.add_argument(
        "--views",
        nargs="+",
        choices=VIEWS,
        default=list(VIEWS),
        help="camera views to capture",
    )
    parser.add_argument(
        "--slow", type=float, default=4, help="slow motion factor (default %(default)s)"
    )
    parser.add_argument(
        "--base", metavar="REF", help="also capture this branch or commit, such as main"
    )
    options = parser.parse_args()
    if not options.slow > 0:
        parser.error("--slow must be positive")
    require_clip(None, options.clip)
    if options.base:
        require_clip(options.base, options.clip)
    capture_all(ROOT, "current", options)
    if options.base:
        capture_base(options.base, options)
    print(f"96 frames a view at 30 a second of {options.slow}x slow motion in {OUTPUT}")


if __name__ == "__main__":
    main()
