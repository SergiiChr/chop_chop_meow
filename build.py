#!/usr/bin/env python3
"""Small Linux launcher and build helper for Chop Chop Meow.

  ./build.py play    Set up the local tools, then run the game. Forward flags after --.
  ./build.py setup   Download tools if needed, compile C#, and import the project.
  ./build.py editor  Set up the project and open the Godot editor.
  ./build.py test    Run the simulation, UI and two-process network checks headlessly.
  ./build.py build   Test, export, and smoke-test build/ChopChopMeow.x86_64.
  ./build.py lint    Compile the C# project and verify its formatting.

Godot, .NET, and export templates are kept under .tools/. Set GODOT_BIN to use an
existing Godot 4.7.2 .NET executable. On a hybrid-graphics laptop, play and editor
render on the NVIDIA GPU; set CHOP_CHOP_MEOW_GPU=integrated to keep the default one.
"""

from __future__ import annotations

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


def dotnet_env() -> dict[str, str]:
    env = os.environ.copy()
    env["XDG_DATA_HOME"] = str(XDG_DATA)
    env["XDG_CONFIG_HOME"] = str(TOOLS / "xdg-config")
    env["XDG_CACHE_HOME"] = str(TOOLS / "xdg-cache")
    return env


def nvidia_offload_available(env: dict[str, str]) -> bool:
    """True on a hybrid machine where another GPU drives the display and the NVIDIA driver can take render offload."""
    try:
        match = re.search(r"\s(\d+)\.\d+", Path("/proc/driver/nvidia/version").read_text())
    except OSError:
        return False
    # PRIME render offload arrived in driver 435.
    if not match or int(match.group(1)) < 435:
        return False
    nvidia_display = other_display = False
    for card in Path("/sys/class/drm").glob("card[0-9]*"):
        try:
            vendor = (card / "device" / "vendor").read_text().strip()
            if not (card / "device" / "class").read_text().startswith("0x03"):
                continue
            boot = (card / "device" / "boot_vga").read_text().strip() == "1"
        except OSError:
            continue
        if vendor == "0x10de":
            nvidia_display |= boot
        elif boot:
            other_display = True
    if nvidia_display or not other_display:
        return False
    # An X server lists the offload provider; without it the NVIDIA GLX library cannot make a context.
    if env.get("XDG_SESSION_TYPE") == "x11" and shutil.which("xrandr"):
        providers = subprocess.run(
            ["xrandr", "--listproviders"], env=env, capture_output=True, text=True, check=False
        )
        return "NVIDIA-G0" in providers.stdout
    return True


def discrete_gpu(env: dict[str, str]) -> dict[str, str]:
    """Render on the NVIDIA GPU through PRIME offload, since OpenGL otherwise picks the integrated one."""
    offload = ("__NV_PRIME_RENDER_OFFLOAD", "__GLX_VENDOR_LIBRARY_NAME", "DRI_PRIME")
    if env.get("CHOP_CHOP_MEOW_GPU") == "integrated" or any(name in env for name in offload):
        return env
    if not nvidia_offload_available(env):
        return env
    return {**env, "__NV_PRIME_RENDER_OFFLOAD": "1", "__GLX_VENDOR_LIBRARY_NAME": "nvidia"}


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


def build_project(env: dict[str, str]) -> None:
    command(
        ["dotnet", "build", "--nologo", "-v", "quiet", ROOT / "ChopChopMeow.csproj"],
        env=env,
    )
    run_godot("import", [GODOT, "--headless", "--path", ROOT, "--import"], env)


def setup() -> dict[str, str]:
    TOOLS.mkdir(exist_ok=True)
    (TOOLS / ".gdignore").touch()
    BUILD.mkdir(exist_ok=True)
    (BUILD / ".gdignore").touch()
    fetch_godot()
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


def main() -> int:
    action = sys.argv[1] if len(sys.argv) > 1 else ""
    if action in {"-h", "--help", "help"}:
        print(__doc__)
        return 0
    if action in {"play", "run", "editor", "setup", "test", "build", "lint"}:
        env = setup()
        if action in {"play", "run"}:
            forwarded = sys.argv[2:]
            if forwarded[:1] == ["--"]:
                forwarded = forwarded[1:]
            game_args = ["--", *forwarded] if forwarded else []
            os.execve(GODOT, [str(GODOT), "--path", str(ROOT), *game_args], discrete_gpu(env))
        if action == "editor":
            os.execve(GODOT, [str(GODOT), "--editor", "--path", str(ROOT)], discrete_gpu(env))
        if action == "test":
            test(env)
        elif action == "build":
            test(env)
            fetch_templates(env)
            run_godot(
                "export",
                [
                    GODOT,
                    "--headless",
                    "--path",
                    ROOT,
                    "--export-release",
                    "Linux/X11",
                    OUTPUT,
                ],
                env,
            )
            run_godot(
                "export-smoke",
                [OUTPUT, "--headless", "--", "--smoke-test"],
                env,
                timeout=120,
                success_marker="SMOKE-TEST PASS",
            )
            archive = BUILD / "ChopChopMeow-linux-x86_64.tar.gz"
            with tarfile.open(archive, "w:gz") as bundle:
                for path in (
                    OUTPUT,
                    OUTPUT.with_suffix(".pck"),
                    BUILD / "data_ChopChopMeow_linuxbsd_x86_64",
                    ROOT / "README.md",
                    ROOT / "LICENSE",
                ):
                    bundle.add(path, arcname=f"ChopChopMeow/{path.name}")
            print(
                f"Built {OUTPUT.relative_to(ROOT)}\nPortable bundle: {archive.relative_to(ROOT)}"
            )
        elif action == "lint":
            command(
                [
                    "dotnet",
                    "format",
                    ROOT / "ChopChopMeow.csproj",
                    "--verify-no-changes",
                ],
                env=env,
            )
        elif action == "setup":
            print("Ready. Run ./build.py play to launch the game.")
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        sys.exit(130)
