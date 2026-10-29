#!/usr/bin/env python3
"""Summarize raw frames from --performance-run without third-party dependencies."""

from __future__ import annotations

import argparse
import csv
import json
import math
from pathlib import Path
from statistics import mean

# Columns summarize reads; allocation, GC and pipeline counters are totals since the game started.
COLUMNS = {
    "frame_ms",
    "process_ms",
    "render_cpu_ms",
    "gpu_ms",
    "allocated_total",
    "gen0_total",
    "gen1_total",
    "gen2_total",
    "static_bytes",
    "video_bytes",
    "draw_calls",
    "primitives",
    "workers_active",
    "machines_active",
    "site",
    "night",
    "settlement",
    "pipelines_total",
}


def summarize(rows: list[dict[str, str]]) -> dict[str, float | int]:
    frames = sorted(float(row["frame_ms"]) for row in rows)

    def percentile(fraction: float) -> float:
        return frames[min(len(frames) - 1, math.ceil(len(frames) * fraction) - 1)]

    return {
        "frames": len(rows),
        "fps": 1000 / mean(frames),
        "mean_ms": mean(frames),
        "p95_ms": percentile(0.95),
        "p99_ms": percentile(0.99),
        "max_ms": max(frames),
        "over_16_7ms": sum(value > 1000 / 60 for value in frames),
        "over_33_3ms": sum(value > 1000 / 30 for value in frames),
        "over_50ms": sum(value > 50 for value in frames),
        "over_100ms": sum(value > 100 for value in frames),
        "process_mean_ms": mean(float(row["process_ms"]) for row in rows),
        "render_cpu_mean_ms": mean(float(row["render_cpu_ms"]) for row in rows),
        "gpu_mean_ms": mean(float(row["gpu_ms"]) for row in rows),
        "allocated_bytes_per_frame": (
            int(rows[-1]["allocated_total"]) - int(rows[0]["allocated_total"])
        )
        / max(1, len(rows) - 1),
        "gen0": int(rows[-1]["gen0_total"]) - int(rows[0]["gen0_total"]),
        "gen1": int(rows[-1]["gen1_total"]) - int(rows[0]["gen1_total"]),
        "gen2": int(rows[-1]["gen2_total"]) - int(rows[0]["gen2_total"]),
        "pipeline_compilations": int(rows[-1]["pipelines_total"])
        - int(rows[0]["pipelines_total"]),
        "static_start_mb": float(rows[0]["static_bytes"]) / 1024**2,
        "static_end_mb": float(rows[-1]["static_bytes"]) / 1024**2,
        "static_peak_mb": max(float(row["static_bytes"]) for row in rows) / 1024**2,
        "video_peak_mb": max(float(row["video_bytes"]) for row in rows) / 1024**2,
        "draw_calls_max": max(int(row["draw_calls"]) for row in rows),
        "primitives_max": max(int(row["primitives"]) for row in rows),
        "workers_active_max": max(int(row["workers_active"]) for row in rows),
        "machines_active_max": max(int(row["machines_active"]) for row in rows),
        "frames_at_least_3_machines": sum(
            int(row["machines_active"]) >= 3 for row in rows
        ),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path)
    options = parser.parse_args()
    frames = options.folder / "frames.csv"
    if not frames.is_file():
        raise SystemExit(f"No {frames}: record one with --performance-run first.")
    with frames.open() as source:
        reader = csv.DictReader(source)
        missing = sorted(COLUMNS - set(reader.fieldnames or ()))
        if missing:
            raise SystemExit(f"{frames} lacks columns: {', '.join(missing)}")
        rows = list(reader)
    groups = {
        "all": rows,
        "day-walking": [
            r for r in rows if r["settlement"] == "False" and r["night"] == "False"
        ],
        "night-walking": [
            r for r in rows if r["settlement"] == "False" and r["night"] == "True"
        ],
        "production": [r for r in rows if r["settlement"] == "True"],
    }
    for site in sorted({r["site"] for r in rows}):
        groups[site] = [
            r for r in rows if r["site"] == site and r["settlement"] == "False"
        ]
    result = {name: summarize(group) for name, group in groups.items() if group}
    slowest = sorted(rows, key=lambda r: float(r["frame_ms"]), reverse=True)[:20]
    summary = {**result, "slowest_frames": slowest}
    (options.folder / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    for name, stats in result.items():
        print(
            f"{name}: {stats['fps']:.1f} FPS, p95 {stats['p95_ms']:.2f} ms, p99 {stats['p99_ms']:.2f} ms, max {stats['max_ms']:.2f} ms; >50ms {stats['over_50ms']}, >100ms {stats['over_100ms']}; GPU {stats['gpu_mean_ms']:.2f} ms"
        )


if __name__ == "__main__":
    main()
