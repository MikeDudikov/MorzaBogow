import pathlib
import struct
import tempfile
import unittest
import zlib

from asset_pipeline.core.images import oversized, png_size


def write_png(path, width, height):
    raw = b"".join(b"\x00" + b"\x00\x00\x00" * width for _ in range(height))
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


class ImageTests(unittest.TestCase):
    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp())

    def test_png_size_reads_the_header(self):
        write_png(self.dir / "a.png", 64, 32)
        self.assertEqual(png_size(self.dir / "a.png"), (64, 32))

    def test_a_file_that_is_not_a_png_has_no_size(self):
        (self.dir / "b.png").write_bytes(b"not an image at all, longer than 24 bytes")
        self.assertIsNone(png_size(self.dir / "b.png"))

    def test_only_maps_larger_than_the_budget_are_shrunk(self):
        # Meshy delivers 2048 px maps; a rock's budget is 1024 and a prop's 512 (config/pipeline.json).
        write_png(self.dir / "big.png", 40, 20)
        write_png(self.dir / "small.png", 16, 16)
        found = oversized({"base_color": self.dir / "big.png", "normal": self.dir / "small.png"}, 32)
        self.assertEqual(list(found), ["base_color"])


if __name__ == "__main__":
    unittest.main()
