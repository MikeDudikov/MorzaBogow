"""Image facts the pipeline needs without an imaging library (WSL Python has only the standard library)."""
import pathlib
import struct


def png_size(path) -> tuple:
    """(width, height) of a PNG from its IHDR chunk, or None when the file is not a PNG."""
    with open(path, "rb") as handle:
        head = handle.read(24)
    if len(head) < 24 or head[:8] != b"\x89PNG\r\n\x1a\n" or head[12:16] != b"IHDR":
        return None
    return struct.unpack(">II", head[16:24])


def oversized(maps: dict, size: int) -> dict:
    """The maps (role -> path) larger than `size` texels on their longer side; maps of unknown size are left alone."""
    result = {}
    for role, path in maps.items():
        dims = png_size(pathlib.Path(path)) if str(path).lower().endswith(".png") else None
        if dims and max(dims) > size:
            result[role] = path
    return result
