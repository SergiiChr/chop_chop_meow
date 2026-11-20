#!/usr/bin/env python3
"""Record one animation clip from several camera views, optionally from another branch too, for side-by-side review."""

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
DEFAULT_FRAMES = 96
DEFAULT_VIEWS = ("side", "front", "game", "facing")
# Time for the launcher to prepare the export before it starts recording.
RECORD_STARTUP_SECONDS = 600

# Camera pitch and rig turn in degrees.
VIEWS = {
    "side": (4, 0),  # Low from the side.
    "front": (4, -90),  # Low with the rig facing the camera.
    "game": (30, 0),  # The game camera angle.
    "facing": (30, -90),  # The game camera angle with the rig facing the camera.
    "top": (89, 0),  # Almost straight down.
}


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


def record(
    checkout: Path, clip: str, view: str, slow: float, frames: int, movie: Path
) -> None:
    """Records one view of a checkout's clip at 30 frames a second of slowed game time."""
    pitch, turn = VIEWS[view]
    movie.parent.mkdir(parents=True, exist_ok=True)
    flags = [
        "--animation-review",
        "--animation-clip",
        clip,
        "--animation-pitch",
        str(pitch),
        "--animation-turn",
        str(turn),
    ]
    log = movie.with_suffix(".log")
    capture_folder = movie.with_suffix("")
    records = '"record"' in (checkout / "build.py").read_text()
    if records:
        command = [
            sys.executable,
            checkout / "build.py",
            "record",
            movie,
            "--",
            *flags,
            "--slow",
            str(slow),
            "--frames",
            str(frames),
        ]
    else:
        # A ref from before recording captured numbered frames, which become a movie of the same rate here.
        shutil.rmtree(capture_folder, ignore_errors=True)
        command = [
            sys.executable, checkout / "build.py", "play", "--", *flags,
            "--animation-slow", str(slow), "--animation-frames", str(frames), "--animation-capture", capture_folder,
        ]  # fmt: skip
    # A native Wayland window draws about one frame a second; XWayland draws at full speed.
    env = {key: value for key, value in os.environ.items() if key != "WAYLAND_DISPLAY"}
    timeout = RECORD_STARTUP_SECONDS + build.record_timeout(frames)
    try:
        with log.open("w") as output:
            result = subprocess.run(
                [str(part) for part in command],
                cwd=checkout,
                env=env,
                stdout=output,
                stderr=subprocess.STDOUT,
                timeout=timeout,
                check=False,
            )
    except subprocess.TimeoutExpired:
        raise SystemExit(
            f"Recording {view} timed out after {timeout} seconds. See {log}"
        ) from None
    if result.returncode:
        text = log.read_text(errors="replace")
        errors = dict.fromkeys(
            line.strip() for line in text.splitlines() if build.ERRORS.search(line)
        )
        print("\n".join(list(errors)[:20]), file=sys.stderr)
        raise SystemExit(
            f"Recording {view} failed (exit {result.returncode}); see {log}"
        )
    if not records:
        subprocess.run(
            ["ffmpeg", "-y", "-loglevel", "error", "-framerate", "30", "-i", capture_folder / "%03d.png", "-c:v", "mjpeg", "-q:v", "2", movie],
            check=True,
        )  # fmt: skip
        shutil.rmtree(capture_folder)
    print(f"Recorded {movie}")


def slug(text: str) -> str:
    """Turns a clip name or ref into a folder name."""
    return re.sub(r"[^\w.-]", "-", text)


def record_all(
    checkout: Path, clip: str, name: str, options: argparse.Namespace
) -> None:
    """Records a checkout's clip into the folder of the clip named on the command line, a movie a view."""
    for view in options.views:
        record(
            checkout,
            clip,
            view,
            options.slow,
            options.frames,
            OUTPUT / slug(options.clip) / name / f"{view}.avi",
        )


def record_base(ref: str, options: argparse.Namespace) -> None:
    """Captures a ref from a temporary worktree that shares this checkout's tools."""
    with tempfile.TemporaryDirectory(prefix="animation-base-") as folder:
        checkout = Path(folder) / "checkout"
        subprocess.run(
            ["git", "worktree", "add", "--detach", checkout, ref], cwd=ROOT, check=True
        )
        try:
            (checkout / ".tools").symlink_to(build.TOOLS.resolve())
            record_all(checkout, options.base_clip, slug(ref), options)
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
        default=DEFAULT_VIEWS,
        help="camera views to record",
    )
    parser.add_argument(
        "--frames",
        type=int,
        default=DEFAULT_FRAMES,
        help="frames per view (default %(default)s)",
    )
    parser.add_argument(
        "--slow", type=float, default=4, help="slow motion factor (default %(default)s)"
    )
    parser.add_argument(
        "--base", metavar="REF", help="also record this branch or commit, such as main"
    )
    parser.add_argument(
        "--base-clip",
        metavar="CLIP",
        help="the clip's name in --base, when it was renamed since (default: the clip)",
    )
    options = parser.parse_args()
    if not options.slow > 0:
        parser.error("--slow must be positive")
    if options.frames < 1:
        parser.error("--frames must be positive")
    require_clip(None, options.clip)
    options.base_clip = options.base_clip or options.clip
    if options.base:
        require_clip(options.base, options.base_clip)
    elif options.base_clip != options.clip:
        parser.error("--base-clip needs --base")
    record_all(ROOT, options.clip, "current", options)
    if options.base:
        record_base(options.base, options)
    print(
        f"{options.frames} frames a view at 30 a second of {options.slow}x slow motion in {OUTPUT / slug(options.clip)}"
    )


if __name__ == "__main__":
    main()
