"""Round-trip asset verification in a fresh Blender process."""
import bpy, json, math
from mathutils import Vector
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
report={}
for key in ('low_poly','realistic_low_vertex'):
    dest=ROOT/key
    bpy.ops.wm.open_mainfile(filepath=str(dest/'Dog.blend'))
    models=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Dog_')]
    assert len(models)==1, (key,'expected one character mesh')
    ob=models[0];ob.data.calc_loop_triangles()
    vertices=len(ob.data.vertices);triangles=len(ob.data.loop_triangles)
    assert vertices < (2200 if key=='low_poly' else 7000), (key,vertices)
    assert ob.data.uv_layers.active is not None
    assert all(math.isfinite(c) and -.001<=c<=1.001 for uv in ob.data.uv_layers.active.data for c in uv.uv)
    assert all(math.isfinite(c) for v in ob.data.vertices for c in v.co)
    assert len(ob.data.materials)==1
    textures=[n.image for n in ob.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE']
    assert textures and all(im.packed_file for im in textures)
    assert not ob.modifiers
    # The mouth/chin must sit ahead of the side cheeks, rather than in a hollow.
    facial_depth={}
    for label,x,z in [('mouth',0,1.37),('chin',0,1.29),('cheek_l',-.18,1.37),('cheek_r',.18,1.37)]:
        hit,point,normal,index=ob.ray_cast(Vector((x*.55,-2*.55,z*.55)),Vector((0,1,0)))
        assert hit,(key,'missing facial surface',label)
        facial_depth[label]=round(point.y,5)
    assert facial_depth['mouth'] < min(facial_depth['cheek_l'],facial_depth['cheek_r'])-.008,(key,'mouth recessed',facial_depth)
    assert facial_depth['chin'] < min(facial_depth['cheek_l'],facial_depth['cheek_r']), (key,'chin recessed',facial_depth)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(dest/'Dog.fbx'))
    imported=[o for o in bpy.data.objects if o.type=='MESH']
    assert len(imported)==1
    mesh=imported[0].data;mesh.calc_loop_triangles()
    assert len(mesh.loop_triangles)==triangles,(key,'FBX triangle count mismatch')
    assert len(mesh.vertices)==vertices,(key,'FBX vertex count mismatch')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(dest/'Dog.glb'))
    imported=[o for o in bpy.data.objects if o.type=='MESH']
    assert len(imported)==1
    mesh=imported[0].data;mesh.calc_loop_triangles()
    assert len(mesh.loop_triangles)==triangles,(key,'GLB triangle count mismatch')
    report[key]={'blend_vertices':vertices,'triangles':triangles,'fbx_roundtrip':'PASS',
                 'glb_roundtrip':'PASS','packed_textures':'PASS','finite_geometry':'PASS',
                 'UVs':'PASS','static_mesh':True,'face_not_recessed':'PASS','facial_depth_y_m':facial_depth}
(ROOT/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('VERIFIED',json.dumps(report),flush=True)
