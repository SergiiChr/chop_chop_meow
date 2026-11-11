#!/usr/bin/env python3
"""Check that the code names the docs put in backticks still exist.

Looks at files and folders, types and their members, lone code names such as a check's, and command-line flags.
Words in backticks that are none of these, such as item ids, keys and shell commands, are left alone.
"""

from __future__ import annotations

import re
import sys
from collections.abc import Iterator
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DOCS = ["*.md", "docs/*.md", "assets/*.md", ".claude/skills/*/SKILL.md"]
# What the names are looked up in: the game and its checks, then the launcher, the tools and the skills' own scripts.
CSHARP = ["src/**/*.cs", "tests/*.cs"]
OTHER_CODE = [
    "build.py",
    "tools/*.py",
    "tools/*.gd",
    ".claude/skills/*/*.py",
    ".claude/skills/*/*.sh",
    ".claude/skills/*/*.txt",
    ".github/workflows/*.yml",
]
# A skill's commands, in its fenced blocks, may add a flag of their own to the game, such as the playtest driver's.
SKILLS = ".claude/skills/*/SKILL.md"

SPAN = re.compile(r"`([^`\n]+)`")
TYPE = re.compile(
    r"\b(?:class|struct|record struct|record|enum|interface)\s+([A-Z]\w*)"
)
WORD = re.compile(r"\w+")
FLAG = re.compile(r"--[a-z][a-z0-9-]*")
NAME = r"[A-Za-z_]\w*"
DOTTED = re.compile(rf"^{NAME}(?:\.{NAME})+$")
PASCAL = re.compile(r"^[A-Z][a-z0-9]+(?:[A-Z][a-z0-9]*)+$")
# Folders of the repo a path in the docs starts with; anything else, such as build output, is not checked.
FOLDERS = (
    "src/",
    "tests/",
    "tools/",
    "docs/",
    "assets/",
    "scenes/",
    "addons/",
    ".claude/",
    ".github/",
)
SOURCE_ENDINGS = (".cs", ".py", ".gd", ".md")


def files(patterns: list[str]) -> list[Path]:
    """Every file under the repo root that one of the patterns matches."""
    return sorted({path for pattern in patterns for path in ROOT.glob(pattern)})


def lines(path: Path) -> Iterator[tuple[int, str, bool]]:
    """Each line of a doc with its number and whether a fenced code block holds it; the fences themselves are left out."""
    fenced = False
    for number, line in enumerate(path.read_text().splitlines(), 1):
        if line.lstrip().startswith("```"):
            fenced = not fenced
        else:
            yield number, line, fenced


class Code:
    """The repo's code, as the docs' names are looked up in it."""

    def __init__(self) -> None:
        csharp = {path: path.read_text() for path in files(CSHARP)}
        other = files(OTHER_CODE)
        skills = [
            line for doc in files([SKILLS]) for _, line, fenced in lines(doc) if fenced
        ]
        self.text = "\n".join(
            [*csharp.values(), *(path.read_text() for path in other), *skills]
        )
        self.words = set(WORD.findall(self.text))
        self.flags = set(FLAG.findall(self.text))
        self.file_names = {path.name for path in [*csharp, *other, *files(DOCS)]}
        self.stems = {path.stem for path in [*csharp, *other]}
        # A partial class is declared in several files, and its members are in any of them.
        self.types: dict[str, set[str]] = {}
        for text in csharp.values():
            words = set(WORD.findall(text))
            for name in set(TYPE.findall(text)):
                self.types.setdefault(name, set()).update(words)

    def has_dotted(self, name: str) -> bool:
        """Whether `Type.Member` names a member of a type declared here, or is written out somewhere as it stands."""
        first, *rest = name.split(".")
        owner = self.types.get(first)
        if owner is None:
            return name in self.text
        for part in rest:
            if part not in owner:
                return False
            owner = self.types.get(part, owner)
        return True


def path_exists(text: str, code: Code) -> bool | None:
    """Whether a path the docs name exists, or None when the text is not a path into the repo."""
    text = text.split("#")[0].removeprefix("./").removeprefix("res://")
    text = re.sub(r":\d+(-\d+)?$", "", text)
    if any(mark in text for mark in "<>{}…$") or " " in text:
        return None
    if text.startswith(FOLDERS):
        return any(ROOT.glob(text.rstrip("/")))
    # A lone file name is checked when it is source or a doc, which live in known places.
    if "/" not in text and text.endswith(SOURCE_ENDINGS) and "*" not in text:
        return text in code.file_names
    return None


def missing(span: str, code: Code) -> Iterator[str]:
    """The names in one backticked span that are meant to be in the repo and are not."""
    words = [word.strip(",;:()[]\"'") for word in span.split()]
    if not words:
        return
    # A flag is the game's or a tool's own when the span starts with a flag or runs one of the repo's scripts.
    # Any other program's flags are its own.
    ours = FLAG.fullmatch(words[0]) is not None or any(
        path_exists(word, code) for word in words
    )
    for word in words:
        exists = path_exists(word, code)
        if exists is not None:
            if not exists:
                yield word
        elif FLAG.fullmatch(word):
            if ours and word not in code.flags:
                yield word
        elif len(words) == 1:
            # `World.As(...)` and `Checks.Test<T>` are looked up as `World.As` and `Checks.Test`.
            name = re.sub(r"<[^<>]*>|\(.*\)$", "", word)
            first = name.split(".")[0]
            # A dotted name is the code's when it starts with a type or a module of the repo.
            # `settings.cfg` and the like are files the game writes.
            if DOTTED.match(name) and (first[0].isupper() or first in code.stems):
                if not code.has_dotted(name):
                    yield word
            elif (
                PASCAL.match(name) and name not in code.stems and name not in code.words
            ):
                yield word


def main() -> int:
    code = Code()
    docs = files(DOCS)
    found = [
        f"{doc.relative_to(ROOT)}:{number}: `{name}`"
        for doc in docs
        for number, line, fenced in lines(doc)
        if not fenced
        for span in SPAN.findall(line)
        for name in missing(span, code)
    ]
    if found:
        print("Names in the docs that are not in the code:\n" + "\n".join(found))
        return 1
    print(f"Docs name nothing missing ({len(docs)} docs).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
