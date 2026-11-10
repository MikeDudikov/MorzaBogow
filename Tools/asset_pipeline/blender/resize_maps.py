"""Blender (headless) script: shrinks texture maps in place to a budget size, keeping their aspect.

    blender -b --factory-startup -P resize_maps.py -- params.json
params: {"maps": {role: absolute png path}, "size": 1024, "report": absolute path}
The base colour stays sRGB; every other map is data (Non-Color), so scaling never runs it through a gamma curve.
"""
import json
import sys

import bpy

params = json.load(open(sys.argv[sys.argv.index("--") + 1], encoding="utf-8"))
size = int(params["size"])
report = {}
for role, path in params["maps"].items():
    image = bpy.data.images.load(path)
    image.colorspace_settings.name = "sRGB" if role in ("base_color", "emission") else "Non-Color"
    width, height = image.size
    if max(width, height) > size:
        scale = size / max(width, height)
        image.scale(max(1, round(width * scale)), max(1, round(height * scale)))
        image.filepath_raw = path
        image.file_format = "PNG"
        image.save()
    report[role] = list(image.size)
with open(params["report"], "w", encoding="utf-8") as handle:
    json.dump(report, handle, indent=1)
