#!/usr/bin/env python3
"""Small Linux launcher and build helper for Chop Chop Meow.

Godot, .NET, the Python check tools and export templates are kept under .tools/. Set GODOT_BIN to use an
existing Godot 4.7.2 .NET executable.
"""

from __future__ import annotations

import argparse
import hashlib
import heapq
import itertools
import os
import random
import re
import select
import shutil
import socket
import subprocess
import sys
import tarfile
import threading
import time
import urllib.request
import zipfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

GODOT_VERSION = "4.7.2"
DOTNET_CHANNEL = "10.0"
ROOT = Path(__file__).resolve().parent
TOOLS = ROOT / ".tools"
BUILD = ROOT / "build"
GODOT_DIR_NAME = f"Godot_v{GODOT_VERSION}-stable_mono_linux_x86_64"
GODOT_RELATIVE = (
    Path(GODOT_DIR_NAME) / f"Godot_v{GODOT_VERSION}-stable_mono_linux.x86_64"
)
GODOT = (
    Path(os.environ["GODOT_BIN"]).expanduser()
    if os.environ.get("GODOT_BIN")
    else TOOLS / GODOT_RELATIVE
)
DOTNET_LOCAL = TOOLS / "dotnet"
XDG_DATA = TOOLS / "xdg-data"
TEMPLATES = XDG_DATA / "godot" / "export_templates" / f"{GODOT_VERSION}.stable.mono"
BASE_URL = (
    f"https://github.com/godotengine/godot/releases/download/{GODOT_VERSION}-stable"
)
OUTPUT = BUILD / "ChopChopMeow.x86_64"
PYTHON_TOOLS = TOOLS / "python"
REQUIREMENTS = ROOT / "requirements-dev.txt"
ERRORS = re.compile(r"SCRIPT ERROR|Parse Error|ERROR:|Unhandled exception")


def command(args: list[str | Path], *, env: dict[str, str] | None = None) -> None:
    result = subprocess.run([str(arg) for arg in args], cwd=ROOT, env=env, check=False)
    if result.returncode:
        sys.exit(result.returncode)


def download(url: str, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    for attempt in range(5):
        try:
            with (
                urllib.request.urlopen(url, timeout=60) as response,
                target.open("wb") as output,
            ):
                shutil.copyfileobj(response, output)
            return
        except OSError as error:
            target.unlink(missing_ok=True)
            if attempt == 4:
                raise SystemExit(f"Could not download {url}: {error}") from error
            print(f"Download failed ({error}); retrying...")
            time.sleep(2 * (attempt + 1))


def fetch_pinned(url: str, archive: Path, sha256: str) -> None:
    """Downloads url into archive unless it is already there, and deletes it and stops when its SHA-256 is not sha256."""
    if not archive.is_file():
        print(f"Downloading {url}...")
        download(url, archive)
    digest = hashlib.sha256()
    with archive.open("rb") as source:
        while block := source.read(1 << 20):
            digest.update(block)
    actual = digest.hexdigest()
    if actual != sha256:
        archive.unlink()
        raise SystemExit(
            f"{archive.name} SHA-256 mismatch: expected {sha256}, got {actual}"
        )


def dotnet_env() -> dict[str, str]:
    env = os.environ.copy()
    env["XDG_DATA_HOME"] = str(XDG_DATA)
    env["XDG_CONFIG_HOME"] = str(TOOLS / "xdg-config")
    env["XDG_CACHE_HOME"] = str(TOOLS / "xdg-cache")
    return env


def dotnet_supported(executable: str | Path, env: dict[str, str]) -> bool:
    sdk = subprocess.run(
        [str(executable), "--list-sdks"],
        capture_output=True,
        text=True,
        env=env,
        check=False,
    )
    runtime = subprocess.run(
        [str(executable), "--list-runtimes"],
        capture_output=True,
        text=True,
        env=env,
        check=False,
    )
    return any(line.startswith("10.") for line in sdk.stdout.splitlines()) and any(
        line.startswith("Microsoft.NETCore.App 10.")
        for line in runtime.stdout.splitlines()
    )


def ensure_dotnet() -> dict[str, str]:
    env = dotnet_env()
    system_dotnet = shutil.which("dotnet")
    if system_dotnet and dotnet_supported(system_dotnet, env):
        system_root = Path(system_dotnet).resolve().parent
        env["DOTNET_ROOT"] = str(system_root)
        env["PATH"] = f"{system_root}{os.pathsep}{env.get('PATH', '')}"
        return env
    local = DOTNET_LOCAL / "dotnet"
    if not local.exists() or not dotnet_supported(local, env):
        print(f"Installing the .NET {DOTNET_CHANNEL} SDK into .tools/dotnet...")
        installer = TOOLS / "dotnet-install.sh"
        download("https://dot.net/v1/dotnet-install.sh", installer)
        command(
            [
                "bash",
                installer,
                "--channel",
                DOTNET_CHANNEL,
                "--install-dir",
                DOTNET_LOCAL,
            ],
            env=env,
        )
    env["DOTNET_ROOT"] = str(DOTNET_LOCAL)
    env["PATH"] = f"{DOTNET_LOCAL}{os.pathsep}{env.get('PATH', '')}"
    return env


def fetch_godot() -> None:
    if not GODOT.is_file() and os.environ.get("GODOT_BIN"):
        raise SystemExit(f"GODOT_BIN does not point to a file: {GODOT}")
    if not GODOT.is_file():
        print(f"Downloading Godot {GODOT_VERSION} .NET into .tools/...")
        archive = TOOLS / "godot.zip"
        download(f"{BASE_URL}/{GODOT_DIR_NAME}.zip", archive)
        with zipfile.ZipFile(archive) as zipped:
            zipped.extractall(TOOLS)
        archive.unlink(missing_ok=True)
        GODOT.chmod(0o755)
    version = subprocess.run(
        [str(GODOT), "--version"], capture_output=True, text=True, check=False
    )
    if version.returncode or not version.stdout.startswith(
        f"{GODOT_VERSION}.stable.mono"
    ):
        raise SystemExit(
            f"Godot must be the {GODOT_VERSION} .NET build: {version.stdout.strip()}"
        )


def run_godot(
    name: str,
    args: list[str | Path],
    env: dict[str, str],
    *,
    timeout: int | None = None,
    success_marker: str | None = None,
) -> None:
    log = BUILD / f"{name}.log"
    with log.open("w") as output:
        try:
            result = subprocess.run(
                [str(arg) for arg in args],
                cwd=ROOT,
                env=env,
                stdout=output,
                stderr=subprocess.STDOUT,
                timeout=timeout,
                check=False,
            )
        except subprocess.TimeoutExpired as error:
            raise SystemExit(
                f"{name} exceeded its {timeout}s time limit; see {log.relative_to(ROOT)}"
            ) from error
    text = log.read_text(errors="replace")
    failures = list(
        dict.fromkeys(line.strip() for line in text.splitlines() if ERRORS.search(line))
    )
    missing_marker = success_marker is not None and success_marker not in text
    if result.returncode or failures or missing_marker:
        if failures:
            print("\n".join(failures[:20]), file=sys.stderr)
        detail = (
            f"; missing success marker {success_marker!r}" if missing_marker else ""
        )
        raise SystemExit(
            f"{name} failed (exit {result.returncode}{detail}); see {log.relative_to(ROOT)}"
        )
    if success_marker is not None:
        print(f"{success_marker} ({log.relative_to(ROOT)})")


def build_project(env: dict[str, str]) -> None:
    command(
        ["dotnet", "build", "--nologo", "-v", "quiet", ROOT / "ChopChopMeow.csproj"],
        env=env,
    )
    import_args: list[str | Path] = [GODOT, "--headless", "--path", ROOT, "--import"]
    try:
        run_godot("import", import_args, env)
    except SystemExit as error:
        # Godot 4.7.2 can crash at shutdown after the first scan of this native
        # extension. Preserve that failure and retry once, only for this exact
        # exit after editor initialization, with no reported engine errors.
        log = BUILD / "import.log"
        text = log.read_text(errors="replace") if log.is_file() else ""
        if (
            "import failed (exit -11)" not in str(error)
            or "loading_editor_layout" not in text
            or ERRORS.search(text)
        ):
            raise
        shutil.copyfile(log, BUILD / "terrain3d-first-import.log")
        print(
            "Terrain3D first import crashed during editor shutdown; retrying once. See build/terrain3d-first-import.log."
        )
        run_godot("import", import_args, env)


def setup() -> dict[str, str]:
    TOOLS.mkdir(exist_ok=True)
    (TOOLS / ".gdignore").touch()
    BUILD.mkdir(exist_ok=True)
    (BUILD / ".gdignore").touch()
    fetch_godot()
    # Terrain3D is part of the default renderer, so every setup path (play,
    # tests, and builds) must provision its pinned native runtime.
    from tools import terrain3d

    terrain3d.install_runtime()
    env = ensure_dotnet()
    build_project(env)
    return env


GAME_PORT = 24565
LAG_PORT = 24575


def lag_proxy(stop: threading.Event, delay: float = 0.08, loss: float = 0.05) -> None:
    """Relay UDP between guests on LAG_PORT and the host, delaying and dropping packets both ways as a long connection would."""
    queue: list[tuple[float, int, socket.socket, tuple[str, int] | None, bytes]] = []
    order = itertools.count()
    upstreams: dict[tuple[str, int], socket.socket] = {}
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as front:
        front.bind(("127.0.0.1", LAG_PORT))
        while not stop.is_set():
            wait = queue[0][0] - time.monotonic() if queue else 0.05
            ready, _, _ = select.select(
                [front, *upstreams.values()], [], [], max(0, min(wait, 0.05))
            )
            for sock in ready:
                # A port that is not listening yet or any more refuses; the game resends what matters.
                try:
                    data, sender = sock.recvfrom(65536)
                except ConnectionRefusedError:
                    continue
                if sock is front:
                    if sender not in upstreams:
                        upstreams[sender] = socket.socket(
                            socket.AF_INET, socket.SOCK_DGRAM
                        )
                        upstreams[sender].connect(("127.0.0.1", GAME_PORT))
                    target, client = upstreams[sender], None
                else:
                    target, client = (
                        front,
                        next(a for a, s in upstreams.items() if s is sock),
                    )
                if random.random() >= loss:
                    due = time.monotonic() + delay * random.uniform(0.75, 1.25)
                    heapq.heappush(queue, (due, next(order), target, client, data))
            while queue and queue[0][0] <= time.monotonic():
                _, _, target, client, data = heapq.heappop(queue)
                try:
                    if client:
                        target.sendto(data, client)
                    else:
                        target.send(data)
                except ConnectionRefusedError:
                    pass
    for upstream in upstreams.values():
        upstream.close()


def python_checks() -> None:
    """Install the pinned check tools into .tools/python when their list changes, then run them."""
    bin_dir = PYTHON_TOOLS / "bin"
    stamp = PYTHON_TOOLS / "requirements.txt"
    if not stamp.is_file() or stamp.read_text() != REQUIREMENTS.read_text():
        print("Installing pinned Python check tools into .tools/python...")
        shutil.rmtree(PYTHON_TOOLS, ignore_errors=True)
        command([sys.executable, "-m", "venv", PYTHON_TOOLS])
        command(
            [
                bin_dir / "pip",
                "install",
                "-q",
                "--disable-pip-version-check",
                "-r",
                REQUIREMENTS,
            ]
        )
        shutil.copyfile(REQUIREMENTS, stamp)
    command([bin_dir / "mypy"])
    command([bin_dir / "ruff", "check"])
    command([bin_dir / "ruff", "format", "--check"])


def test(env: dict[str, str]) -> None:
    run_godot(
        "test",
        [GODOT, "--headless", "--path", ROOT, "--", "--self-test"],
        env,
        timeout=120,
        success_marker="SELF-TEST PASS",
    )
    run_godot(
        "ui-test",
        [GODOT, "--headless", "--path", ROOT, "--", "--ui-test"],
        env,
        timeout=45,
        success_marker="UI-TEST PASS",
    )
    # The host and guest of the network check talk to each other, so both run at once.
    # The guest joins through a relay that adds lag and packet loss.
    stop = threading.Event()
    roles = {"host": ["host"], "guest": ["guest", "--port", str(LAG_PORT)]}
    with ThreadPoolExecutor() as pool:
        relay = pool.submit(lag_proxy, stop)
        checks = [
            pool.submit(
                run_godot,
                f"net-{role}",
                [GODOT, "--headless", "--path", ROOT, "--", "--net-check", *args],
                env,
                timeout=240,
                success_marker=f"NET-{role.upper()} PASS",
            )
            for role, args in roles.items()
        ]
        try:
            for check in checks:
                check.result()
        finally:
            stop.set()
            relay.result()


def fetch_templates(env: dict[str, str]) -> None:
    if (TEMPLATES / "linux_release.x86_64").is_file():
        return
    print("Downloading Godot Linux export templates into .tools/...")
    archive = TOOLS / "templates.tpz"
    download(
        f"{BASE_URL}/Godot_v{GODOT_VERSION}-stable_mono_export_templates.tpz", archive
    )
    TEMPLATES.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as zipped:
        for name in ("linux_release.x86_64", "linux_debug.x86_64", "version.txt"):
            (TEMPLATES / name).write_bytes(zipped.read(f"templates/{name}"))
    archive.unlink(missing_ok=True)


def package_linux() -> Path:
    """Keep exported native extensions beside the executable in the portable bundle."""
    paths = [
        OUTPUT,
        OUTPUT.with_suffix(".pck"),
        BUILD / "data_ChopChopMeow_linuxbsd_x86_64",
        ROOT / "README.md",
        ROOT / "LICENSE",
        BUILD / "libterrain.linux.release.x86_64.so",
    ]
    archive = BUILD / "ChopChopMeow-linux-x86_64.tar.gz"
    with tarfile.open(archive, "w:gz") as bundle:
        for path in paths:
            bundle.add(path, arcname=f"ChopChopMeow/{path.name}")
    return archive


def check_raw_save() -> None:
    """Fail when a check or step outside tests/SetupSteps.cs uses a world's raw save."""
    raw = re.compile(r"\b(?!Key\b)\w+\.S\b")
    files = [
        *ROOT.glob("tests/*.cs"),
        *ROOT.glob("src/*Checks.cs"),
        *ROOT.glob("src/*Steps.cs"),
    ]
    found = [
        f"{path.relative_to(ROOT)}:{number}: {line.strip()}"
        for path in sorted(files)
        if path.name != "SetupSteps.cs"
        for number, line in enumerate(path.read_text().splitlines(), 1)
        if raw.search(line)
    ]
    if found:
        raise SystemExit(
            "Raw save used outside tests/SetupSteps.cs:\n" + "\n".join(found)
        )


def run_check(env: dict[str, str]) -> None:
    """Verify C# format and raw save use, then run every test."""
    check_raw_save()
    command(
        ["dotnet", "format", ROOT / "ChopChopMeow.csproj", "--verify-no-changes"],
        env=env,
    )
    test(env)


def run_build(env: dict[str, str]) -> None:
    """Run every check, export the Linux build, smoke-test it and bundle it."""
    run_check(env)
    fetch_templates(env)
    run_godot(
        "export",
        [GODOT, "--headless", "--path", ROOT, "--export-release", "Linux/X11", OUTPUT],
        env,
    )
    run_godot(
        "export-smoke",
        [OUTPUT, "--headless", "--", "--smoke-test"],
        env,
        timeout=120,
        success_marker="SMOKE-TEST PASS",
    )
    archive = package_linux()
    print(
        f"Built {OUTPUT.relative_to(ROOT)}\nPortable bundle: {archive.relative_to(ROOT)}"
    )


def run_icons(env: dict[str, str]) -> None:
    """Render the icons from their models, then re-import the project."""
    # Rendering needs a real window; the headless renderer draws nothing.
    run_godot(
        "icons",
        [GODOT, "--path", ROOT, "--", "--render-icons", ROOT / "assets/icons"],
        env,
        timeout=300,
        success_marker="ICONS RENDERED",
    )
    run_godot("import", [GODOT, "--headless", "--path", ROOT, "--import"], env)


def build_parser() -> argparse.ArgumentParser:
    """Describe the subcommands and their help text."""
    parser = argparse.ArgumentParser(
        prog="build.py",
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    commands = parser.add_subparsers(dest="action", metavar="command")
    helps = {
        "play": "Set up the local tools, then run the game. Forward flags after --.",
        "editor": "Set up the project and open the Godot editor.",
        "setup": "Download tools if needed, compile C#, and import the project.",
        "check": "Run every offline check: Python types and style, C# compile and "
        "format, then the simulation, UI and two-process network tests.",
        "test": "Run only the simulation, UI and two-process network tests.",
        "build": "Check, export, and smoke-test build/ChopChopMeow.x86_64.",
        "icons": "Render item and building icons from their models into assets/icons.",
    }
    for name, text in helps.items():
        subparser = commands.add_parser(name, help=text, description=text)
        if name == "play":
            subparser.usage = "build.py play [-h] [-- GAME_ARGS ...]"
    return parser


def main() -> int:
    argv = sys.argv[1:]
    # Everything after the first `--` belongs to the game, not to this launcher.
    split = argv.index("--") if "--" in argv else len(argv)
    own_args, game_args = argv[:split], argv[split + 1 :]
    parser = build_parser()
    args = parser.parse_args(own_args)
    if args.action is None:
        parser.print_help()
        return 2
    if game_args and args.action != "play":
        parser.error(f"{args.action} does not take arguments after --")
    if args.action in {"check", "build"}:
        python_checks()
    env = setup()
    match args.action:
        case "play":
            forwarded = ["--", *game_args] if game_args else []
            os.execve(GODOT, [str(GODOT), "--path", str(ROOT), *forwarded], env)
        case "editor":
            os.execve(GODOT, [str(GODOT), "--editor", "--path", str(ROOT)], env)
        case "setup":
            print("Ready. Run ./build.py play to launch the game.")
        case "check":
            run_check(env)
        case "test":
            test(env)
        case "build":
            run_build(env)
        case "icons":
            run_icons(env)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        sys.exit(130)
