"""Shared provider pieces."""
import base64
import pathlib

from ..core.errors import Fatal

IMAGE_MIME = {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".webp": "image/webp"}


class Status:
    """A remote task's state as the worker understands it."""

    def __init__(self, state: str, progress=None, raw=None, error=None, consumed_credits=None):
        assert state in ("running", "succeeded", "failed", "canceled"), state
        self.state = state
        self.progress = progress
        self.raw = raw
        self.error = error
        self.consumed_credits = consumed_credits


def data_uri(path, mime=None, limit=100 * 1024 * 1024) -> str:
    path = pathlib.Path(path)
    size = path.stat().st_size
    if size > limit:
        raise Fatal(f"{path.name} ma {size} B; limit data URI to {limit} B.")
    mime = mime or IMAGE_MIME.get(path.suffix.lower(), "application/octet-stream")
    return f"data:{mime};base64," + base64.b64encode(path.read_bytes()).decode("ascii")


def capability(service, stages, status, verified, detail, env=(), config=None) -> dict:
    """One row of the capability matrix. Environment variables are reported as set or not, never by value."""
    return {
        "service": service,
        "stages": list(stages),
        "status": status,
        "verified": verified,
        "detail": detail,
        "env": {name: (config.is_set(name) if config else False) for name in env},
    }
