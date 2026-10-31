#!/usr/bin/env python3
"""List string literals in src/ that differ from a base commit, to catch a rename reaching into text."""

from __future__ import annotations

import argparse
import re
import subprocess
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LITERAL = re.compile(r'"(?:[^"\\\n]|\\.)*"')


def literals(text: str) -> Counter[str]:
    """Count every string literal in C# source."""
    return Counter(LITERAL.findall(text))


def at_commit(base: str) -> Counter[str]:
    """Count the literals in src/ at a commit."""
    names = subprocess.run(
        ["git", "ls-tree", "-r", "--name-only", base, "src/"],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=True,
    ).stdout.split()
    found: Counter[str] = Counter()
    for name in names:
        if name.endswith(".cs"):
            source = subprocess.run(
                ["git", "show", f"{base}:{name}"],
                cwd=ROOT,
                capture_output=True,
                text=True,
                check=True,
            ).stdout
            found += literals(source)
    return found


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("base", help="commit to compare against")
    base = at_commit(parser.parse_args().base)
    now: Counter[str] = Counter()
    for path in (ROOT / "src").glob("*.cs"):
        now += literals(path.read_text())
    for literal in sorted(base.keys() - now.keys()):
        print(f"- {literal}")
    for literal in sorted(now.keys() - base.keys()):
        print(f"+ {literal}")


if __name__ == "__main__":
    main()
