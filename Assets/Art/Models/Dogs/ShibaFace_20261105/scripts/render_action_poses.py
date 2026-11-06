import bpy
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];P=ROOT/'previews'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'model/Dog_Rigged.blend'))
sc=bpy.context.scene;sc.render.engine='BLENDER_EEVEE';sc.render.resolution_x=720;sc.render.resolution_y=660
rig=bpy.data.objects['Dog_Rig'];cam=sc.camera
clips={'Idle':16,'Run':5,'Jump':22,'Bark':10,'Poop':55,'Pee':49,'Bite':11}
for name,frame in clips.items():
    rig.animation_data.action=bpy.data.actions[name];sc.frame_set(frame)
    if name in ('Bark','Bite'):
        cam.location=(1.35,-3.4,1.35);cam.data.ortho_scale=.78;target=(0,-.48,.88)
    else:
        cam.location=(2.5,-3.7,2.0);cam.data.ortho_scale=1.70;target=(0,.03,.59)
    cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=str(P/f'Shiba_anim_{name}.png');bpy.ops.render.render(write_still=True)
    print('POSE_RENDERED',name,frame,flush=True)
print('POSES_COMPLETE',flush=True)
