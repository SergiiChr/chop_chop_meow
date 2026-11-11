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

SPAN = re.compile(r"`([^`\n]+)`")
TYPE = re.compile(
    r"\b(?:class|struct|record struct|record|enum|interface)\s+([A-Z]\w*)"
)
NAME = r"[A-Za-z_]\w*"
DOTTED = re.compile(rf"^{NAME}(?:\.{NAME})+$")
PASCAL = re.compile(r"^[A-Z][a-z0-9]+(?:[A-Z][a-z0-9]*)+$")
FLAG = re.compile(r"^--[a-z][a-z0-9-]*$")
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
# Endings of files the game or a tool writes while running, which a dotted name must not be taken for.
DATA_ENDINGS = (
    "json",
    "cfg",
    "txt",
    "log",
    "tsv",
    "png",
    "svg",
    "mp3",
    "ogv",
    "mp4",
    "gif",
    "zip",
    "gz",
    "pck",
    "tscn",
    "godot",
    "toml",
    "yml",
    "sh",
    "import",
    "uid",
    "x86_64",
    "done",
    "tmp",
)
# Commands of other programs, whose flags are theirs and not the game's.
FOREIGN = (
    "git",
    "gh",
    "dotnet",
    "mypy",
    "ruff",
    "pip",
    "ln",
    "cp",
    "rm",
    "ffmpeg",
    "pgrep",
    "pkill",
)
# A skill's commands, in its fenced blocks, may add a flag of their own to the game, such as the playtest driver's.
SKILLS = ".claude/skills/*/SKILL.md"
# Names that are right but are not in this repo: tools kept outside it.
OUTSIDE = {"loudness.py"}


def files(patterns: list[str]) -> list[Path]:
    """Every file under the repo root that one of the patterns matches."""
    return sorted({path for pattern in patterns for path in ROOT.glob(pattern)})


def spans(path: Path) -> Iterator[tuple[int, str]]:
    """Each backticked span in a doc with its line number, leaving out fenced code blocks."""
    fenced = False
    for number, line in enumerate(path.read_text().splitlines(), 1):
        if line.lstrip().startswith("```"):
            fenced = not fenced
            continue
        if not fenced:
            for match in SPAN.finditer(line):
                yield number, match.group(1)


def fenced(path: Path) -> Iterator[str]:
    """The lines of a doc's fenced code blocks."""
    inside = False
    for line in path.read_text().splitlines():
        if line.lstrip().startswith("```"):
            inside = not inside
        elif inside:
            yield line


class Code:
    """The repo's code, as the docs' names are looked up in it."""

    def __init__(self) -> None:
        self.csharp = {path: path.read_text() for path in files(CSHARP)}
        self.other = "\n".join(
            [path.read_text() for path in files(OTHER_CODE)]
            + [block for path in files([SKILLS]) for block in fenced(path)]
        )
        self.stems = {path.stem for path in self.csharp}
        self.all = "\n".join(self.csharp.values()) + "\n" + self.other
        # A partial class is declared in several files, and its members are in any of them.
        self.types: dict[str, str] = {}
        for text in self.csharp.values():
            for name in set(TYPE.findall(text)):
                self.types[name] = self.types.get(name, "") + "\n" + text

    def has_word(self, word: str, text: str | None = None) -> bool:
        """Whether a name appears as a whole word."""
        return (
            re.search(rf"(?<![\w]){re.escape(word)}(?![\w])", text or self.all)
            is not None
        )

    def has_dotted(self, name: str) -> bool:
        """Whether `Type.Member` names a member of a type declared here, or is written out somewhere as it stands."""
        parts = name.split(".")
        owner = self.types.get(parts[0])
        if owner is None:
            return name in self.all
        for part in parts[1:]:
            if not self.has_word(part, owner):
                return False
            owner = self.types.get(part, owner)
        return True

    def has_flag(self, flag: str) -> bool:
        return re.search(rf"(?<![\w-]){re.escape(flag)}(?![\w-])", self.all) is not None


def path_exists(text: str) -> bool | None:
    """Whether a path the docs name exists, or None when the text is not a path into the repo."""
    text = text.split("#")[0].removeprefix("./").removeprefix("res://")
    text = re.sub(r":\d+(-\d+)?$", "", text)
    if any(mark in text for mark in "<>{}…$") or " " in text:
        return None
    if text.startswith(FOLDERS):
        return any(ROOT.glob(text.rstrip("/")))
    # A lone file name is checked when it is source or a doc, which live in known places.
    if "/" not in text and text.endswith(SOURCE_ENDINGS) and "*" not in text:
        return any(ROOT.glob(text)) or any(
            path.name == text for path in files([*CSHARP, *OTHER_CODE, *DOCS])
        )
    return None


def missing(span: str, code: Code) -> Iterator[str]:
    """The names in one backticked span that are meant to be in the repo and are not."""
    words = span.split()
    foreign = words[0] in FOREIGN
    for word in words:
        word = word.strip(",;:()[]\"'")
        if word in OUTSIDE:
            continue
        exists = path_exists(word)
        if exists is not None:
            if not exists:
                yield word
            continue
        if FLAG.match(word):
            if not foreign and not code.has_flag(word):
                yield word
            continue
        if len(words) > 1:
            continue
        # `World.As(...)`, `Checks.Test<T>` and `GameData.LowNeed` are looked up as `World.As`, `Checks.Test` and so on.
        name = re.sub(r"<[^<>]*>|\(.*\)$", "", word)
        if DOTTED.match(name) and name.rsplit(".", 1)[1] not in DATA_ENDINGS:
            if not code.has_dotted(name):
                yield word
        elif PASCAL.match(name) and name not in code.stems and not code.has_word(name):
            yield word


def main() -> int:
    code = Code()
    found = [
        f"{doc.relative_to(ROOT)}:{number}: `{name}`"
        for doc in files(DOCS)
        for number, span in spans(doc)
        for name in missing(span, code)
    ]
    if found:
        print("Names in the docs that are not in the code:\n" + "\n".join(found))
        return 1
    print(f"Docs name nothing missing ({len(files(DOCS))} docs).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
