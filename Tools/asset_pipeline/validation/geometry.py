"""Model inspection without Blender or Unity: OBJ and glTF binary (GLB).

Counts triangles, measures bounds, and reports UVs, normals, materials, textures, skins and
animations. FBX is not parsed here; Unity's validator measures it after import.
"""
import json
import pathlib
import struct

COMPONENTS = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}
TYPES = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def inspect(path) -> dict:
    path = pathlib.Path(path)
    suffix = path.suffix.lower()
    if suffix == ".obj":
        return inspect_obj(path)
    if suffix == ".glb":
        return inspect_glb(path)
    raise ValueError(f"Inspekcja obsługuje OBJ i GLB, nie {suffix}")


def inspect_obj(path) -> dict:
    lo, hi = [float("inf")] * 3, [float("-inf")] * 3
    vertices = triangles = 0
    has_uv = has_normals = False
    materials = []
    with open(path, encoding="utf-8", errors="replace") as source:
        for line in source:
            if line.startswith("v "):
                values = [float(x) for x in line.split()[1:4]]
                vertices += 1
                for k in range(3):
                    lo[k] = min(lo[k], values[k])
                    hi[k] = max(hi[k], values[k])
            elif line.startswith("f "):
                corners = line.split()[1:]
                triangles += max(0, len(corners) - 2)
                first = corners[0].split("/")
                has_uv = has_uv or (len(first) > 1 and first[1] != "")
                has_normals = has_normals or (len(first) > 2 and first[2] != "")
            elif line.startswith("usemtl "):
                name = line.split(None, 1)[1].strip()
                if name not in materials:
                    materials.append(name)
    return _result("obj", vertices, triangles, lo, hi, has_uv, has_normals, materials, 0, False, 0)


def inspect_glb(path) -> dict:
    data = pathlib.Path(path).read_bytes()
    magic, version, length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2:
        raise ValueError("To nie jest plik glTF 2.0 (GLB)")
    offset, gltf = 12, None
    while offset < min(length, len(data)):
        chunk_length, chunk_type = struct.unpack_from("<I4s", data, offset)
        if chunk_type == b"JSON":
            gltf = json.loads(data[offset + 8: offset + 8 + chunk_length].decode("utf-8"))
        offset += 8 + chunk_length
    if gltf is None:
        raise ValueError("GLB bez części JSON")
    accessors = gltf.get("accessors", [])
    nodes = gltf.get("nodes", [])
    lo, hi = [float("inf")] * 3, [float("-inf")] * 3
    vertices = triangles = 0
    has_uv = has_normals = True
    mesh_nodes = [n for n in nodes if "mesh" in n] or [{"mesh": i} for i in range(len(gltf.get("meshes", [])))]
    for node in mesh_nodes:
        scale = node.get("scale", [1, 1, 1])
        translation = node.get("translation", [0, 0, 0])
        for primitive in gltf["meshes"][node["mesh"]].get("primitives", []):
            attributes = primitive.get("attributes", {})
            position = accessors[attributes["POSITION"]]
            count = position["count"]
            vertices += count
            mode = primitive.get("mode", 4)
            if mode == 4:
                triangles += (accessors[primitive["indices"]]["count"] if "indices" in primitive else count) // 3
            if "min" in position and "max" in position:
                for k in range(3):
                    a = position["min"][k] * scale[k] + translation[k]
                    b = position["max"][k] * scale[k] + translation[k]
                    lo[k] = min(lo[k], a, b)
                    hi[k] = max(hi[k], a, b)
            has_uv = has_uv and "TEXCOORD_0" in attributes
            has_normals = has_normals and "NORMAL" in attributes
    materials = [m.get("name", f"material_{i}") for i, m in enumerate(gltf.get("materials", []))]
    return _result("glb", vertices, triangles, lo, hi, has_uv and vertices > 0, has_normals and vertices > 0, materials,
                   len(gltf.get("images", [])), bool(gltf.get("skins")), len(gltf.get("animations", [])))


def _result(fmt, vertices, triangles, lo, hi, has_uv, has_normals, materials, images, skinned, animations) -> dict:
    if vertices == 0:
        lo, hi = [0.0] * 3, [0.0] * 3
    return {
        "format": fmt,
        "vertices": vertices,
        "triangles": triangles,
        "bounds": {"min": [round(v, 4) for v in lo], "max": [round(v, 4) for v in hi], "size": [round(hi[k] - lo[k], 4) for k in range(3)]},
        "has_uv": has_uv,
        "has_normals": has_normals,
        "materials": materials,
        "images": images,
        "skinned": skinned,
        "animations": animations,
    }


def lod_problems(reports, triangle_budget: int) -> list:
    """Budget and ordering problems of a LOD chain (first entry is LOD0)."""
    problems = []
    if not reports:
        return ["brak poziomów LOD"]
    if reports[0]["triangles"] > triangle_budget:
        problems.append(f"LOD0 ma {reports[0]['triangles']} trójkątów, budżet {triangle_budget}")
    for level in range(1, len(reports)):
        if reports[level]["triangles"] >= reports[level - 1]["triangles"]:
            problems.append(f"LOD{level} nie ma mniej trójkątów niż LOD{level - 1}")
    for level, report in enumerate(reports):
        if not report["has_uv"]:
            problems.append(f"LOD{level} bez UV")
        if not report["has_normals"]:
            problems.append(f"LOD{level} bez normalnych")
    return problems
