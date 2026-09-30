# -*- coding: utf-8 -*-
"""Diagnostic only: isolate each mesh of the exported FBX to locate artefacts."""
import bpy, math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
MODEL = os.path.dirname(HERE)
GEN = os.path.join(MODEL, "Generated")
PREV = os.path.join(GEN, "preview")
FBX = os.path.join(GEN, "FigureRig.fbx")


def C(x, y, z):
    return (x, -z, y)


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX, global_scale=1.0)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'TEXTURE'
sc.display.shading.show_shadows = False
sc.render.resolution_x = 512
sc.render.resolution_y = 512
cam_data = bpy.data.cameras.new("C")
cam_data.sensor_fit = 'VERTICAL'
cam_data.angle_y = math.radians(35.0)
cam = bpy.data.objects.new("C", cam_data)
sc.collection.objects.link(cam)
cam.location = C(0.0, 0.0, -11.5)
cam.rotation_euler = (math.radians(90.0), 0.0, math.radians(180.0))
sc.camera = cam

meshes = [o for o in bpy.data.objects if o.type == 'MESH']
print("MESHES:", [o.name for o in meshes])
groups = {}
for o in meshes:
    key = o.name.split('.')[0].replace("Mesh", "")
    groups.setdefault(key, []).append(o)

for key, obs in groups.items():
    for o in meshes:
        o.hide_render = o not in obs
    p = os.path.join(PREV, "isolate_%s.png" % key)
    sc.render.filepath = p
    bpy.ops.render.render(write_still=True)
    print("wrote", p)
