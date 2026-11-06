import sys, math, json
from pathlib import Path
import bpy
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent))
import rig_helpers as r
ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'model';PRE=ROOT/'previews'

def main():
    bpy.ops.wm.open_mainfile(filepath=str(DEST/'SurfaceReview.blend'))
    body=bpy.data.objects['PhotoDog_Mesh']
    for im in bpy.data.images:
        if im.name.startswith(('Dog_BaseColor','Dog_Normal')):
            path=DEST/(('Dog_Normal' if im.name.startswith('Dog_Normal') else 'Dog_BaseColor')+'.png')
            if im.packed_file:im.unpack(method='REMOVE')
            im.filepath=str(path);im.reload();im.pack()
    lower,upper,seams=r.mouth_slit(body)
    if seams==0:raise RuntimeError('Mouth must have a real opening')
    rig=r.create_rig();r.skin(body,rig,lower,upper);r.mouth_parts(body,rig,False)
    # Visible individual dark claws, attached to their corresponding paw.
    clawmat=r.solid_mat('Claws',(.045,.026,.019))
    claws=[]
    for side,s in [('L',-1),('R',1)]:
        for front,y in [(True,-.48),(False,.75)]:
            for dx in [-.061,-.020,.020,.061]:
                bpy.ops.mesh.primitive_uv_sphere_add(segments=8,ring_count=5,location=r.V((s*.252+dx,y-.132,.05)))
                c=bpy.context.object;c.scale=r.V((.013,.038,.018))
                bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
                c.data.materials.append(clawmat)
                g=c.vertex_groups.new(name=('FrontPaw_' if front else 'RearPaw_')+side)
                g.add(list(range(len(c.data.vertices))),1,'REPLACE')
                for f in c.data.polygons:f.use_smooth=True
                claws.append(c)
    r.active(body)
    for c in claws:c.select_set(True)
    bpy.ops.object.join()
    col=bpy.data.collections.new('CHARACTER_export');bpy.context.scene.collection.children.link(col)
    for ob in (body,rig):
        for old in list(ob.users_collection):old.objects.unlink(ob)
        col.objects.link(ob)
    r.make_actions(rig)
    scene=bpy.context.scene;scene.render.fps=30
    rig['reference']='One front three-quarter photo; unseen side/back/tail inferred.'
    rig['coordinates']='Meters. Blender forward -Y, up +Z. Export forward -Z, up +Y.'
    body['surface']='Smooth editable mesh, applied decimation, UV color atlas and tangent normal.'
    body.data.calc_loop_triangles()
    report={'blender':bpy.app.version_string,'vertices':len(body.data.vertices),'triangles':len(body.data.loop_triangles),
        'bones':len(rig.data.bones),'mouth_seam_edges':seams,'dimensions_m':list(body.dimensions),
        'clips':{n:{'frames':round(s*30)+1,'seconds':s,'loop':n in ('Idle','Run')} for n,s in r.CLIPS.items()},
        'limitations':['Single-view reconstruction: side/back/tail inferred.','Photo color contains original baked illumination.','Basic game animation, no Unity integration.']}
    (ROOT/'model_stats.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    # No IK or constraints are required at playback: all bone transforms are keyed.
    r.export(body,rig,DEST)
    rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)
    cam=scene.camera;cam.location=(2.4,-4,1.6);cam.data.ortho_scale=1.65
    cam.rotation_euler=(Vector((0,.04,.65))-cam.location).to_track_quat('-Z','Y').to_euler()
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.region_3d.view_distance=2.5
                area.spaces.active.region_3d.view_location=(0,0,.65)
                area.spaces.active.shading.type='MATERIAL'
    r.active(rig)
    bpy.ops.wm.save_as_mainfile(filepath=str(DEST/'Dog_Rigged.blend'))
    scene.cycles.samples=12;scene.render.resolution_x=700;scene.render.resolution_y=700
    for name,fraction in [('Idle',.25),('Run',.17),('Jump',.5),('Bark',.29),('Poop',.5),('Pee',.5),('Bite',.40)]:
        rig.animation_data.action=bpy.data.actions[name];scene.frame_set(1+round(r.CLIPS[name]*30*fraction))
        cam.data.ortho_scale=1.90 if name=='Jump' else 1.70
        target=Vector((0,.03,.78 if name=='Jump' else .65))
        cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(PRE/('pose_'+name+'.png'));bpy.ops.render.render(write_still=True)
    print('ASSET_COMPLETE',json.dumps(report),flush=True)

if __name__=='__main__':main()
