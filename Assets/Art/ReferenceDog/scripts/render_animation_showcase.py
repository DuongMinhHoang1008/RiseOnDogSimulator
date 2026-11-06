"""Fast animated viewport review, separate from the delivered editable rig scene."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'realistic_low_vertex/Dog_Rigged.blend'))
scene=bpy.context.scene
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
rig.animation_data.action=None
track=rig.animation_data.nla_tracks.new();track.name='Review_only'
cursor=1.;ranges=[]
for name,repeat in [('Idle',1),('Run',3),('Jump',1),('Bark',1),('Poop',1),('Pee',1),('Bite',1)]:
    action=bpy.data.actions[name]
    strip=track.strips.new(name,int(cursor),action)
    strip.action_frame_start=1;strip.action_frame_end=action.frame_range[1]
    strip.scale=.5;strip.repeat=repeat;strip.extrapolation='NOTHING';strip.blend_type='REPLACE'
    ranges.append((int(cursor),int(math.ceil(strip.frame_end)),name))
    cursor=strip.frame_end
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO';scene.display.shading.color_type='TEXTURE'
scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
scene.display.shading.cavity_type='BOTH';scene.display.shading.background_type='WORLD'
scene.display.render_aa='16'
scene.world.color=(.08,.10,.13)
scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=0
scene.render.resolution_x=640;scene.render.resolution_y=600;scene.render.resolution_percentage=100
scene.render.fps=15;scene.frame_start=1;scene.frame_end=int(math.ceil(cursor))-1
cam=scene.camera;cam.location=(2.5,-3.7,2.0);cam.data.ortho_scale=1.90
cam.rotation_euler=(Vector((0,.03,.68))-cam.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.text_add()
label=bpy.context.object;label.name='REVIEW_Clip_Label';label.parent=cam
label.location=(0,-.83,-1);label.rotation_euler=(0,0,0)
label.data.align_x='CENTER';label.data.size=.07
mat=bpy.data.materials.new('Review_label');mat.diffuse_color=(1,1,1,1);label.data.materials.append(mat)
def text_at_frame(s):
    for a,b,name in ranges:
        if a<=s.frame_current<=b:label.data.body=name.upper();break
bpy.app.handlers.frame_change_pre.append(text_at_frame)
scene.frame_set(1)
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(ROOT/'previews/showcase_workbench_check.png')
bpy.ops.render.render(write_still=True)
scene.render.image_settings.media_type='VIDEO';scene.render.image_settings.file_format='FFMPEG'
scene.render.ffmpeg.format='MPEG4';scene.render.ffmpeg.codec='H264'
scene.render.ffmpeg.constant_rate_factor='MEDIUM';scene.render.filepath=str(ROOT/'previews/animation_showcase.mp4')
bpy.ops.render.render(animation=True)
(ROOT/'previews/showcase_timeline.json').write_text(json.dumps({'fps':15,'ranges':ranges,'frames':scene.frame_end},indent=2),encoding='utf-8')
print('SHOWCASE_VIDEO_COMPLETE',flush=True)
