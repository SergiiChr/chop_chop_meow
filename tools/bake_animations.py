#!/usr/bin/env python3
"""Offline art packaging. Requires Pillow; never called by setup, play, or export.

Inputs are authored crop rectangles and foot pivots in art/animation-layouts.json.
All RGBA pixels inside each rectangle are retained, including disconnected tools.
Old source sheets can be recovered from the pinned Git revision; new sheets live
under art/animation-sources (excluded from Godot import and export).
"""

import argparse
import io
import json
import math
import subprocess
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]


def read_source(source):
    if source.startswith("git:"):
        return Image.open(
            io.BytesIO(subprocess.check_output(["git", "show", source[4:]], cwd=ROOT))
        ).convert("RGBA")
    return Image.open(ROOT / source).convert("RGBA")


def pack(source, spec):
    """Pack exactly the supplied rectangles; no segmentation or pivot inference."""
    if not math.isfinite(spec["body_height"]) or spec["body_height"] <= 0:
        raise ValueError("Expected a positive body height")
    scale = 150 / spec["body_height"]  # 2x normal display resolution.
    frames = []
    for frame in spec["frames"]:
        x, y, width, height = frame["rect"]
        if (
            min(x, y) < 0
            or min(width, height) <= 0
            or x + width > source.width
            or y + height > source.height
        ):
            raise ValueError("Crop rectangle outside source")
        foot = frame["foot"]
        if (
            len(foot) != 2
            or not all(math.isfinite(v) for v in foot)
            or not 0 <= foot[0] <= width
            or not 0 <= foot[1] <= height
        ):
            raise ValueError("Explicit foot pivot outside crop")
        image = source.crop((x, y, x + width, y + height))
        image = image.resize(
            (max(1, round(width * scale)), max(1, round(height * scale))),
            Image.Resampling.LANCZOS,
        )
        # Use actual rounded dimensions to keep the pivot coincident with the image.
        foot = [
            frame["foot"][0] * image.width / width,
            frame["foot"][1] * image.height / height,
        ]
        frames.append((image, foot))
    if len(frames) < 8 or len(frames) % 4 != 0:
        raise ValueError(
            "Expected four directions and at least two frames per direction"
        )
    cell_w = max(image.width for image, foot in frames) + 4
    cell_h = max(image.height for image, foot in frames) + 4
    atlas = Image.new("RGBA", (cell_w * 4, cell_h * (len(frames) // 4)))
    metadata = []
    for index, (image, foot) in enumerate(frames):
        x, y = (index % 4) * cell_w + 2, (index // 4) * cell_h + 2
        atlas.paste(image, (x, y))  # Paste RGBA as-is; do not premultiply alpha twice.
        metadata.append(
            {
                "region": [x, y, image.width, image.height],
                "foot": [round(v, 3) for v in foot],
            }
        )
    return atlas, metadata


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT)
    args = parser.parse_args()
    layouts = json.loads((ROOT / "art/animation-layouts.json").read_text())
    destination = args.output / "assets/animations"
    destination.mkdir(parents=True, exist_ok=True)
    manifest = {}
    for name, spec in layouts.items():
        atlas, frames = pack(read_source(spec["source"]), spec)
        temporary = destination / f"{name}.webp.tmp"
        atlas.save(
            temporary,
            format="WEBP",
            quality=92,
            alpha_quality=100,
            method=6,
            exact=True,
        )
        temporary.replace(destination / f"{name}.webp")
        manifest[name] = {
            "texture": f"res://assets/animations/{name}.webp",
            "body_height": 150,
            "frames": frames,
        }
    (args.output / "data").mkdir(parents=True, exist_ok=True)
    temporary = args.output / "data/animations.json.tmp"
    temporary.write_text(json.dumps(manifest, indent=2) + "\n")
    temporary.replace(args.output / "data/animations.json")
    print(
        f"Baked {len(manifest)} clips ({sum(len(clip['frames']) for clip in manifest.values())} frames)."
    )


if __name__ == "__main__":
    main()
