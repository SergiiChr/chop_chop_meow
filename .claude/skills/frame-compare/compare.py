#!/usr/bin/env python3
"""Put folders of numbered frames side by side, each under a label, as an MP4, a GIF or a contact sheet."""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path

BACKGROUND = "0x141e18"
TEXT = "0xf2e8cf"
LABEL_HEIGHT = 40


@dataclass(frozen=True)
class Panel:
    folder: Path
    label: str


def parse_panel(text: str) -> Panel:
    """Reads `folder:label`; the label defaults to the folder's name."""
    folder, _, label = text.partition(":")
    path = Path(folder).expanduser()
    if not path.is_dir():
        raise argparse.ArgumentTypeError(f"not a folder: {folder}")
    return Panel(path, label or path.name)


def parse_size(text: str) -> tuple[int, int]:
    width, _, height = text.partition("x")
    try:
        return int(width), int(height)
    except ValueError as error:
        raise argparse.ArgumentTypeError(f"size must look like 460x700: {text}") from error


def panel_filter(index: int, panel: Panel, options: argparse.Namespace, labels: Path) -> str:
    """Crops one input around its centre, shifted by --shift, and writes its label over a band at the top."""
    label_file = labels / f"{index}.txt"
    label_file.write_text(panel.label)
    width, height = options.crop
    crop = f"crop={width}:{height}:(iw-{width})/2:(ih-{height})/2+{options.shift}" if width else "null"
    number = (
        f",drawtext=text='%{{eif\\:n+{options.first}\\:d}}':x=w-tw-12:y=10:fontsize=18:fontcolor={TEXT}"
        if options.numbers
        else ""
    )
    return (
        f"[{index}:v]{crop},drawbox=x=0:y=0:w=iw:h={LABEL_HEIGHT}:color={BACKGROUND}:t=fill,"
        f"drawtext=textfile='{label_file}':x=12:y=8:fontsize=22:fontcolor={TEXT}{number}[p{index}]"
    )


def build_filter(panels: list[Panel], options: argparse.Namespace, labels: Path) -> str:
    parts = [panel_filter(i, panel, options, labels) for i, panel in enumerate(panels)]
    joined = "".join(f"[p{i}]" for i in range(len(panels)))
    stack = f"{joined}hstack=inputs={len(panels)}" if len(panels) > 1 else "[p0]null"
    # H.264 needs even sizes, so the scaled height rounds to one.
    stack += f",scale=trunc(iw*{options.scale}/2)*2:-2"
    output = Path(options.output)
    if output.suffix == ".gif":
        stack += ",split[a][b];[a]palettegen=max_colors=128[pal];[b][pal]paletteuse=dither=bayer:bayer_scale=4"
    elif output.suffix == ".png":
        frames = -(-options.count // options.step)
        columns = min(options.columns, frames)
        rows = -(-frames // columns)
        stack += f",trim=end_frame={options.count},select='not(mod(n\\,{options.step}))',tile={columns}x{rows}:color={BACKGROUND}"
    return ";".join([*parts, stack])


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", help="an .mp4 video, a .gif or a .png contact sheet")
    parser.add_argument("panels", nargs="+", type=parse_panel, help="folder:label, left to right")
    parser.add_argument("--pattern", default="%03d.png", help="frame file names, printf style (default %(default)s)")
    parser.add_argument("--first", type=int, default=0, help="first frame number (default %(default)s)")
    parser.add_argument("--count", type=int, required=True, help="frames to use from each folder")
    parser.add_argument("--fps", type=float, default=30, help="playback rate (default %(default)s)")
    parser.add_argument("--crop", type=parse_size, default=(0, 0), help="crop each panel around its centre, as WxH")
    parser.add_argument("--shift", type=int, default=0, help="move the crop down this many pixels")
    parser.add_argument("--scale", type=float, default=1, help="scale the whole picture (default %(default)s)")
    parser.add_argument("--numbers", action="store_true", help="print each frame's number in its corner")
    parser.add_argument("--step", type=int, default=1, help="contact sheet: every Nth frame (default %(default)s)")
    parser.add_argument("--columns", type=int, default=8, help="contact sheet: frames a row (default %(default)s)")
    options = parser.parse_args()
    if Path(options.output).suffix not in {".mp4", ".gif", ".png"}:
        parser.error("output must end in .mp4, .gif or .png")
    if shutil.which("ffmpeg") is None:
        sys.exit("ffmpeg is not installed")
    panels: list[Panel] = options.panels
    inputs: list[str] = []
    for panel in panels:
        inputs += ["-framerate", str(options.fps), "-start_number", str(options.first)]
        inputs += ["-i", str(panel.folder / options.pattern)]
    encode = ["-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", "-movflags", "+faststart"]
    with tempfile.TemporaryDirectory() as labels:
        command = [
            "ffmpeg", "-y", "-loglevel", "error", *inputs,
            "-filter_complex", build_filter(panels, options, Path(labels)), "-frames:v",
            "1" if options.output.endswith(".png") else str(options.count),
            *(encode if options.output.endswith(".mp4") else []),
            *(["-loop", "0"] if options.output.endswith(".gif") else []),
            options.output,
        ]  # fmt: skip
        subprocess.run(command, check=True)
    print(f"Wrote {Path(options.output).resolve()}")


if __name__ == "__main__":
    main()
