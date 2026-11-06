import bpy,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
EXPECTED={'Idle','Run','Jump','Bark','Poop','Pee','Bite'}
report={}
for key in ('low_poly','realistic_low_vertex'):
    report[key]={}
    for ext in ('fbx','glb'):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path=str(ROOT/key/f'Dog_Rigged.{ext}')
        if ext=='fbx':bpy.ops.import_scene.fbx(filepath=path)
        else:bpy.ops.import_scene.gltf(filepath=path)
        rigs=[o for o in bpy.data.objects if o.type=='ARMATURE']
        assert len(rigs)==1,(key,ext,'armature missing')
        rig=rigs[0]
        actions={a.name.split('|')[-1]:a for a in bpy.data.actions}
        assert EXPECTED.issubset(actions),(key,ext,'missing actions',list(actions))
        assert len(rig.data.bones)==27,(key,ext,'wrong skeleton',len(rig.data.bones))
        images=[im for im in bpy.data.images if 'BaseColor' in im.name]
        for im in images:
            if im.size[0]>0:_=im.pixels[0]
        assert images and all(im.has_data and im.size[0]>=1024 for im in images),(key,ext,'missing texture pixels',[(im.name,im.has_data,tuple(im.size)) for im in images])
        helpers={p.custom_shape for p in rig.pose.bones if p.custom_shape is not None}
        meshes=[o for o in bpy.data.objects if o.type=='MESH' and o not in helpers]
        print('IMPORTED_MESHES',key,ext,[(o.name,o.hide_render,[m.type for m in o.modifiers]) for o in meshes],flush=True)
        assert meshes and all(any(m.type=='ARMATURE' for m in o.modifiers) for o in meshes)
        bounds={}
        for name in sorted(EXPECTED):
            action=actions[name];rig.animation_data_create();rig.animation_data.action=action
            if action.slots:rig.animation_data.action_slot=action.slots[0]
            start,end=action.frame_range
            assert end-start>15,(key,ext,name,'empty action')
            bpy.context.scene.frame_set(round((start+end)/2));bpy.context.view_layer.update()
            dg=bpy.context.evaluated_depsgraph_get()
            for ob in meshes:
                evaluated=ob.evaluated_get(dg);mesh=evaluated.to_mesh()
                assert all(math.isfinite(c) for v in mesh.vertices for c in v.co)
                evaluated.to_mesh_clear()
            bounds[name]=[float(start),float(end)]
        report[key][ext]={'status':'PASS','bones':27,'clip_ranges':bounds,'skins':len(meshes),'texture_pixels':'PASS'}
(ROOT/'rig_export_verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('RIG_EXPORTS_VERIFIED',json.dumps(report),flush=True)
