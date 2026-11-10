"""Ship blockout from the game's own generator (GreekShips in Assets/MorzaBogow/Runtime/Presentation): Unity exports a
class's ship as OBJ, one file per level of detail, with an MTL of the flat palette and a JSON manifest (files, triangles,
paints). This provider converts LOD0 to glTF binary (GLB) for Meshy retexture (`enable_original_uv`) and for inspection,
so the pipeline's model is exactly the ship the battle shows. Axes: metres, +Y up, bow along +Z, waterline at Y = 0.
"""
import json
import pathlib
import struct

from ..core.errors import Fatal
from .base import capability


def _linear(c: float) -> float:
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def read_obj(path):
    """Positions, UVs, normals and triangles per material of an OBJ (1-based, v/vt/vn faces)."""
    positions, uvs, normals = [], [], []
    groups = {}
    current = "default"
    with open(path, encoding="utf-8") as source:
        for line in source:
            if line.startswith("v "):
                positions.append(tuple(float(x) for x in line.split()[1:4]))
            elif line.startswith("vt "):
                uvs.append(tuple(float(x) for x in line.split()[1:3]))
            elif line.startswith("vn "):
                normals.append(tuple(float(x) for x in line.split()[1:4]))
            elif line.startswith("usemtl "):
                current = line.split(None, 1)[1].strip()
            elif line.startswith("f "):
                corners = [tuple(int(i) if i else 0 for i in c.split("/")) for c in line.split()[1:]]
                for k in range(1, len(corners) - 1):
                    groups.setdefault(current, []).append((corners[0], corners[k], corners[k + 1]))
    return positions, uvs, normals, groups


def obj_to_glb(obj_path, glb_path, paints: dict) -> int:
    """Converts an OBJ to GLB, one primitive per material; colours from the paints (sRGB) become linear factors."""
    positions, uvs, normals, groups = read_obj(obj_path)
    buffer = bytearray()
    views, accessors, primitives, materials = [], [], [], []

    def view(data: bytes, target: int) -> int:
        while len(buffer) % 4:
            buffer.append(0)
        views.append({"buffer": 0, "byteOffset": len(buffer), "byteLength": len(data), "target": target})
        buffer.extend(data)
        return len(views) - 1

    def accessor(index, component, count, kind, extra=None) -> int:
        accessors.append({"bufferView": index, "componentType": component, "count": count, "type": kind, **(extra or {})})
        return len(accessors) - 1

    triangles = 0
    for name, faces in groups.items():
        remap, pos, nor, tex, idx = {}, [], [], [], []
        for face in faces:
            for corner in face:
                if corner not in remap:
                    remap[corner] = len(pos)
                    v = corner[0] - 1
                    t = corner[1] - 1 if len(corner) > 1 and corner[1] else -1
                    n = corner[2] - 1 if len(corner) > 2 and corner[2] else -1
                    pos.append(positions[v])
                    tex.append((uvs[t][0], 1.0 - uvs[t][1]) if t >= 0 else (0.0, 0.0))
                    nor.append(normals[n] if n >= 0 else (0.0, 1.0, 0.0))
                idx.append(remap[corner])
        triangles += len(faces)
        flat = [c for p in pos for c in p]
        lo = [min(p[k] for p in pos) for k in range(3)]
        hi = [max(p[k] for p in pos) for k in range(3)]
        a_pos = accessor(view(struct.pack(f"<{len(flat)}f", *flat), 34962), 5126, len(pos), "VEC3", {"min": lo, "max": hi})
        a_nor = accessor(view(struct.pack(f"<{len(nor) * 3}f", *[c for n in nor for c in n]), 34962), 5126, len(nor), "VEC3")
        a_tex = accessor(view(struct.pack(f"<{len(tex) * 2}f", *[c for t in tex for c in t]), 34962), 5126, len(tex), "VEC2")
        a_idx = accessor(view(struct.pack(f"<{len(idx)}I", *idx), 34963), 5125, len(idx), "SCALAR")
        primitives.append({"attributes": {"POSITION": a_pos, "NORMAL": a_nor, "TEXCOORD_0": a_tex}, "indices": a_idx, "material": len(materials), "mode": 4})
        paint = paints.get(name, {"baseColor": [0.7, 0.7, 0.7], "metallic": 0.0, "smoothness": 0.5})
        materials.append({"name": name, "pbrMetallicRoughness": {"baseColorFactor": [_linear(c) for c in paint["baseColor"]] + [1.0],
                                                                 "metallicFactor": paint.get("metallic", 0.0), "roughnessFactor": round(1.0 - paint.get("smoothness", 0.5), 3)}})
    stem = pathlib.Path(obj_path).stem
    gltf = {"asset": {"version": "2.0", "generator": "Morza Bogow asset pipeline (GreekShips export)"}, "scene": 0, "scenes": [{"nodes": [0]}],
            "nodes": [{"mesh": 0, "name": stem}], "meshes": [{"name": stem, "primitives": primitives}], "materials": materials,
            "buffers": [{"byteLength": len(buffer)}], "bufferViews": views, "accessors": accessors}
    data = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    data += b" " * (-len(data) % 4)
    while len(buffer) % 4:
        buffer.append(0)
    with open(glb_path, "wb") as out:
        out.write(struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(data) + 8 + len(buffer)))
        out.write(struct.pack("<I4s", len(data), b"JSON") + data)
        out.write(struct.pack("<I4s", len(buffer), b"BIN\x00") + bytes(buffer))
    return triangles


class Blockout:
    name = "blockout"
    paid = False

    def __init__(self, config):
        self.config = config

    def capability(self) -> dict:
        from ..unity import Unity
        found = Unity(self.config).path()
        return capability("Okręt z generatora gry (Unity)", ["generating"], "available" if found else "needs_configuration", "uruchomione lokalnie",
                          "Okręt klasy z GreekShips (Egeja XII w. p.n.e.) eksportowany przez Unity: 3 LOD w OBJ, paleta 6 farb, GLB z LOD0 do retekstury Meshy.",
                          (), self.config)

    def build(self, brief: dict, out: pathlib.Path, ctx) -> dict:
        out.mkdir(parents=True, exist_ok=True)
        manifest = ctx.providers["unity"].export_blockout(ctx, brief["shipClass"], out, brief["id"])
        count = brief.get("lods", 3)
        lods = [out / name for name in manifest["lods"]][:count]
        missing = [str(p) for p in lods if not p.exists()]
        if missing:
            raise Fatal("Eksport Unity bez plików: " + ", ".join(missing))
        glb = out / f"{brief['id']}_LOD0.glb"
        obj_to_glb(lods[0], glb, {m["name"]: m for m in manifest["materials"]})
        files = lods + [out / f"{brief['id']}.mtl", out / f"{brief['id']}.json", glb]
        return {"files": [f for f in files if f.exists()], "lods": lods, "glb": glb, "materials": manifest["materials"],
                "triangles": manifest["triangles"][:count], "type": manifest.get("type")}
