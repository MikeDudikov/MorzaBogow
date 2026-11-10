"""Project paths and Windows/WSL path conversion.

The tools run under WSL Python (Windows has only the Store stub), while Unity and a portable
Blender are Windows programs started through WSL interop, so they need Windows paths.
"""
import os
import pathlib
import re
import subprocess

TOOL_ROOT = pathlib.Path(__file__).resolve().parent.parent
PROJECT_ROOT = TOOL_ROOT.parent.parent

_MNT = re.compile(r"^/mnt/([a-zA-Z])(/.*)?$")
_DRIVE = re.compile(r"^([a-zA-Z]):[\\/](.*)$")


def on_windows() -> bool:
    return os.name == "nt"


def to_windows(path) -> str:
    """A path a Windows program understands."""
    text = str(path)
    if on_windows() or _DRIVE.match(text):
        return text.replace("/", "\\")
    match = _MNT.match(text)
    if match:
        rest = (match.group(2) or "/").replace("/", "\\")
        return f"{match.group(1).upper()}:{rest}"
    try:
        return subprocess.run(["wslpath", "-w", text], check=True, capture_output=True, text=True, timeout=10).stdout.strip()
    except (OSError, subprocess.SubprocessError):
        return text


def from_windows(path) -> pathlib.Path:
    """A local path for a Windows-style path (C:\\x -> /mnt/c/x under WSL)."""
    text = str(path).strip().strip('"')
    if on_windows():
        return pathlib.Path(text)
    match = _DRIVE.match(text)
    if match:
        return pathlib.Path(f"/mnt/{match.group(1).lower()}/" + match.group(2).replace("\\", "/"))
    return pathlib.Path(text)


def project_relative(path, root=None) -> str:
    """Forward-slash path relative to the project root, for manifests and Unity asset paths."""
    root = pathlib.Path(root or PROJECT_ROOT).resolve()
    return pathlib.Path(path).resolve().relative_to(root).as_posix()
