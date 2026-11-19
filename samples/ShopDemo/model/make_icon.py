# make_icon.py: renders GlowBerry.png, the ShopDemo sample's inventory icon, from GlowBerry.obj next to this file.
# Run: blender -b -P make_icon.py -- <out dir>   (Blender 5.2; writes <out dir>/GlowBerry.png, 256x256, transparent)
# Flat colours with a dark outline, seen from the front and a little above, like the game's own item icons.
import math, os, sys
import bpy

here = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
out_dir = args[0] if args else here

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=os.path.join(here, 'GlowBerry.obj'))   # Y-up OBJ -> Blender Z-up
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']

# Workbench shows a material's viewport colour, not its node colour: copy Kd over, brightened a little.
for o in objs:
    for slot in o.material_slots:
        m = slot.material
        if m is None or not m.use_nodes: continue
        bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if bsdf is None: continue
        c = bsdf.inputs['Base Color'].default_value
        m.diffuse_color = (min(1, c[0] * 1.15), min(1, c[1] * 1.15), min(1, c[2] * 1.15), 1.0)

# Frame the model: its bounds in world space.
pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
lo = [min(p[i] for p in pts) for i in range(3)]
hi = [max(p[i] for p in pts) for i in range(3)]
centre = [(lo[i] + hi[i]) / 2 for i in range(3)]
size = max(hi[i] - lo[i] for i in range(3))

scene = bpy.context.scene
cam_data = bpy.data.cameras.new('IconCam')
cam_data.type = 'ORTHO'
cam_data.ortho_scale = size * 1.25
cam = bpy.data.objects.new('IconCam', cam_data)
scene.collection.objects.link(cam)
pitch, yaw, dist = math.radians(25), math.radians(55), size * 4
cam.location = (centre[0] + dist * math.cos(pitch) * math.sin(yaw),
                centre[1] - dist * math.cos(pitch) * math.cos(yaw),
                centre[2] + dist * math.sin(pitch))
direction = [centre[i] - cam.location[i] for i in range(3)]
from mathutils import Vector
cam.rotation_euler = Vector(direction).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam

scene.render.engine = 'BLENDER_WORKBENCH'
sh = scene.display.shading
sh.light = 'STUDIO'
sh.color_type = 'MATERIAL'
sh.show_object_outline = False   # the outline is drawn below, round the whole silhouette
sh.show_cavity = False
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'   # AgX/Filmic darken flat colours
scene.view_settings.exposure = 2.4                # studio light alone renders the colours too dark
scene.render.resolution_x = scene.render.resolution_y = 256
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.filepath = os.path.join(out_dir, 'GlowBerry.png')
bpy.ops.render.render(write_still=True)

# A dark outline round the silhouette (5 px), as the game's icons have: grow the alpha mask and fill the ring.
import numpy as np
img = bpy.data.images.load(scene.render.filepath)
w, h = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
inside = px[..., 3] > 0.5
grown = inside.copy()
r = 5
for dy in range(-r, r + 1):
    for dx in range(-r, r + 1):
        if dx * dx + dy * dy > r * r: continue
        grown |= np.roll(np.roll(inside, dy, axis=0), dx, axis=1)
ring = grown & ~inside
px[ring] = (0.07, 0.05, 0.03, 1.0)
img.pixels[:] = px.ravel()
img.filepath_raw = scene.render.filepath
img.file_format = 'PNG'
img.save()
print('icon written:', scene.render.filepath)
