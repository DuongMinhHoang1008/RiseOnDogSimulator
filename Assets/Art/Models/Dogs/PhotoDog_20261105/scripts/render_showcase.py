"""Render a compact workbench movie of all delivered actions for motion review."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'model/Dog_Rigged.blend'))
scene=bpy.context.scene;rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
rig.animation_data.action=None
track=rig.animation_data.nla_tracks.new();track.name='Review_only'
cursor=1.;ranges=[]
for name,repeat in [('Idle',1),('Run',3),('Jump',1),('Bark',1),('Poop',1),('Pee',1),('Bite',1)]:
    action=bpy.data.actions[name];strip=track.strips.new(name,int(cursor),action)
    strip.action_frame_start=1;strip.action_frame_end=action.frame_range[1]
    strip.scale=.5;strip.repeat=repeat;strip.extrapolation='NOTHING'
    ranges.append((int(cursor),int(math.ceil(strip.frame_end)),name));cursor=strip.frame_end
scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.light='STUDIO'
scene.display.shading.color_type='TEXTURE';scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True;scene.display.shading.cavity_type='BOTH'
scene.display.shading.background_type='WORLD';scene.display.render_aa='16'
scene.world.color=(.06,.08,.10);scene.view_settings.view_transform='Standard'
scene.render.resolution_x=640;scene.render.resolution_y=600;scene.render.resolution_percentage=100
scene.render.fps=15;scene.frame_start=1;scene.frame_end=int(math.ceil(cursor))-1
cam=scene.camera;cam.location=(2.5,-3.7,2.0);cam.data.ortho_scale=1.9
cam.rotation_euler=(Vector((0,.03,.68))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.image_settings.media_type='VIDEO';scene.render.image_settings.file_format='FFMPEG'
scene.render.ffmpeg.format='MPEG4';scene.render.ffmpeg.codec='H264'
scene.render.ffmpeg.constant_rate_factor='MEDIUM';scene.render.filepath=str(ROOT/'previews/animation_showcase.mp4')
bpy.ops.render.render(animation=True)
(ROOT/'previews/showcase_timeline.json').write_text(json.dumps({'fps':15,'ranges':ranges,'frames':scene.frame_end},indent=2))
print('SHOWCASE_COMPLETE',scene.frame_end,flush=True)
