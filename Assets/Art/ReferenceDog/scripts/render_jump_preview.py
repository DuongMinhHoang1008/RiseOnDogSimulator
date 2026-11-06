import bpy
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
for key in ('low_poly','realistic_low_vertex'):
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/key/'Dog_Rigged.blend'))
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    rig.animation_data.action=bpy.data.actions['Jump']
    scene=bpy.context.scene;scene.frame_set(22)
    scene.render.resolution_x=720;scene.render.resolution_y=660;scene.cycles.samples=16
    cam=scene.camera;cam.location=(2.5,-3.7,2.0);cam.data.ortho_scale=1.90
    cam.rotation_euler=(Vector((0,.03,.75))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(ROOT/'previews'/f'{key}_anim_Jump.png')
    bpy.ops.render.render(write_still=True)
