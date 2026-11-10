"""Runs inside Blender: blender -b --factory-startup -P prepare.py --python-exit-code 3 -- params.json

Imports a generated model, puts it in the project's frame (metres; bow or front along the file's +Z,
which Blender shows as -Y; Y up after export), scales it to the target size, sets the pivot
(ships: waterline at 0 and keel at -draft; other assets: base at 0), cleans and triangulates the
mesh, adds UVs when missing, decimates LOD0 to the triangle budget, builds lower LODs and exports
one FBX per LOD for Unity plus a GLB of LOD0 for inspection and retexturing. Writes report.json.
Written against the Blender 4.2-5.x Python API; not yet run in this project (no Blender installed).
"""
import json
import math
import sys

import bpy
import mathutils

# File axes (glTF/Unity style: Y up, front along +Z) as Blender directions after import.
FORWARD = {"+Z": (0.0, -1.0, 0.0), "-Z": (0.0, 1.0, 0.0), "+X": (1.0, 0.0, 0.0), "-X": (-1.0, 0.0, 0.0)}


def params():
    return json.loads(open(sys.argv[sys.argv.index("--") + 1], encoding="utf-8").read())


def import_model(path: str):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    lower = path.lower()
    if lower.endswith((".glb", ".gltf")):
        bpy.ops.import_scene.gltf(filepath=path)
    elif lower.endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=path)
    elif lower.endswith(".obj"):
        bpy.ops.wm.obj_import(filepath=path, forward_axis="NEGATIVE_Z", up_axis="Y")
    else:
        raise RuntimeError(f"Nieobsługiwany format: {path}")


def select(objects, active=None):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active or (objects[0] if objects else None)


def triangles(obj) -> int:
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def bounds(obj):
    corners = [obj.matrix_world @ mathutils.Vector(c) for c in obj.bound_box]
    lo = mathutils.Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    hi = mathutils.Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    return lo, hi


def edit(obj, action):
    select([obj], obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    action()
    bpy.ops.object.mode_set(mode="OBJECT")


def decimate(obj, ratio: float):
    if ratio >= 0.999:
        return
    select([obj], obj)
    modifier = obj.modifiers.new("Decimate", "DECIMATE")
    modifier.ratio = max(0.01, ratio)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def main():
    p = params()
    import_model(p["source"])
    scene = bpy.context.scene
    for obj in [o for o in scene.objects if o.type not in ("MESH", "ARMATURE")]:
        bpy.data.objects.remove(obj, do_unlink=True)
    meshes = [o for o in scene.objects if o.type == "MESH"]
    if not meshes:
        raise RuntimeError("Model bez siatek")
    rigged = any(o.type == "ARMATURE" for o in scene.objects)
    roots = [o for o in scene.objects if o.parent is None]
    select(roots)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if not rigged and len(meshes) > 1:
        select(meshes, meshes[0])
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active if not rigged else meshes[0]

    # Orientation: bring the front to Blender -Y (file +Z after export).
    forward_checked = p["forward"] != "auto"
    lo, hi = bounds(obj)
    size = hi - lo
    if p["forward"] == "auto":
        direction = (0.0, -1.0, 0.0) if size.y >= size.x else (1.0, 0.0, 0.0)
    else:
        direction = FORWARD[p["forward"]]
    angle = math.atan2(direction[0], -direction[1])
    if abs(angle) > 1e-6:
        rotation = mathutils.Matrix.Rotation(-angle, 4, "Z")
        for root in roots:
            root.matrix_world = rotation @ root.matrix_world
        select(roots)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

    # Scale to the target size.
    lo, hi = bounds(obj)
    size = hi - lo
    target = p.get("target") or {}
    factor = 1.0
    if p["category"] == "ship" and target.get("length"):
        factor = target["length"] / max(size.y, 1e-6)
    elif target.get("height"):
        factor = target["height"] / max(size.z, 1e-6)
    elif target.get("length"):
        factor = target["length"] / max(size.y, 1e-6)
    elif target.get("width"):
        factor = target["width"] / max(size.x, 1e-6)
    if abs(factor - 1.0) > 1e-4:
        scaling = mathutils.Matrix.Scale(factor, 4)
        for root in roots:
            root.matrix_world = scaling @ root.matrix_world
        select(roots)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    # Pivot: centred in plan; ships keel at -draft (waterline 0), others standing on 0.
    lo, hi = bounds(obj)
    floor = -target.get("draft", 0.0) if p["category"] == "ship" else 0.0
    offset = mathutils.Vector((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, floor - lo.z))
    for root in roots:
        root.location = root.location + offset
    select(roots)
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    if not rigged:
        edit(obj, lambda: (bpy.ops.mesh.remove_doubles(threshold=0.0001), bpy.ops.mesh.normals_make_consistent(inside=False),
                           bpy.ops.mesh.quads_convert_to_tris(quad_method="BEAUTY", ngon_method="BEAUTY")))
    if not obj.data.uv_layers:
        edit(obj, lambda: bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.02))

    budget = int(p["triangles"])
    if not rigged and triangles(obj) > budget:
        decimate(obj, budget / triangles(obj) * 0.98)

    stem = p["stem"]
    lods = [obj]
    obj.name = f"{stem}_LOD0"
    if not rigged:
        for level, ratio in enumerate(p["lodRatios"][1:], start=1):
            copy = obj.copy()
            copy.data = obj.data.copy()
            copy.name = f"{stem}_LOD{level}"
            scene.collection.objects.link(copy)
            decimate(copy, ratio)
            lods.append(copy)

    out = p["out"].rstrip("/\\")
    sep = "\\" if "\\" in out else "/"
    report = {"lods": [], "glb": None, "forwardChecked": forward_checked}
    for level, lod in enumerate(lods):
        path = f"{out}{sep}{stem}_LOD{level}.fbx"
        export = [lod] + ([o for o in scene.objects if o.type == "ARMATURE"] if rigged else [])
        select(export, lod)
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                                 bake_space_transform=True, object_types={"MESH", "ARMATURE"}, mesh_smooth_type="FACE",
                                 add_leaf_bones=False, path_mode="AUTO")
        report["lods"].append({"file": path, "triangles": triangles(lod)})
    select([lods[0]], lods[0])
    glb = f"{out}{sep}{stem}_LOD0.glb"
    bpy.ops.export_scene.gltf(filepath=glb, export_format="GLB", use_selection=True, export_yup=True)
    report["glb"] = glb
    lo, hi = bounds(lods[0])
    report["bounds"] = {"min": [lo.x, lo.z, -hi.y], "max": [hi.x, hi.z, -lo.y], "size": [hi.x - lo.x, hi.z - lo.z, hi.y - lo.y]}
    with open(p["report"], "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=2)


if __name__ == "__main__":
    main()
