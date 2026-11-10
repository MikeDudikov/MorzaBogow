"""Blender (background): renders GLB models side by side with the Workbench engine, for a look at untextured previews
(Meshy text-to-3d previews before paying for a refine). Each model is scaled to 2 m and stood on the ground.

blender -b --factory-startup -P Tools/asset_pipeline/blender/preview_glb.py -- out.png a.glb b.glb ...
"""
import math
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
out, models = args[0], args[1:]

bpy.ops.wm.read_factory_settings(use_empty=True)
offset = 0.0
for path in models:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in new:
        for corner in o.bound_box:
            w = o.matrix_world @ Vector(corner)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    size = max(hi - lo)
    scale = 2.0 / size if size > 0 else 1.0
    roots = [o for o in bpy.data.objects if o not in before and o.parent is None]
    for o in roots:
        o.scale = o.scale * scale
        o.location = (o.location - (lo + hi) * 0.5) * scale + Vector((offset, 0, 0))
        o.location.z += (hi.z - lo.z) * 0.5 * scale
    offset += 2.6

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "SINGLE"
scene.display.shading.single_color = (0.78, 0.74, 0.66)
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "BOTH"
scene.display.shading.show_shadows = True
scene.render.resolution_x = 520 * len(models)
scene.render.resolution_y = 560
scene.render.filepath = out

camera_data = bpy.data.cameras.new("Camera")
camera_data.type = "ORTHO"
camera_data.ortho_scale = 2.7 * len(models)
camera = bpy.data.objects.new("Camera", camera_data)
bpy.context.collection.objects.link(camera)
centre = Vector(((len(models) - 1) * 2.6 * 0.5, 0, 1.0))
direction = Vector((0.0, -1.0, 0.55)).normalized()
camera.location = centre + direction * 12
camera.rotation_euler = (math.radians(90 - 28.8), 0, 0)
scene.camera = camera
bpy.ops.render.render(write_still=True)
print("rendered", out)
