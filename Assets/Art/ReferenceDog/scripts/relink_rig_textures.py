import bpy,importlib.util
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('dog_rig_builder',ROOT/'scripts/rig_and_animate.py')
builder=importlib.util.module_from_spec(spec);spec.loader.exec_module(builder)
for key in ('low_poly','realistic_low_vertex'):
    dest=ROOT/key
    bpy.ops.wm.open_mainfile(filepath=str(dest/'Dog_Rigged.blend'))
    for image in bpy.data.images:
        for filename in ('Dog_BaseColor.png','Dog_Normal.png'):
            if image.name.startswith(Path(filename).stem) and (dest/filename).exists():
                if image.packed_file:image.unpack(method='REMOVE')
                image.filepath=str(dest/filename)
                image.reload();image.pack()
                print('RELINKED',image.name,image.filepath,flush=True)
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    mesh=next(o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Dog_Rigged'))
    builder.export(mesh,rig,dest)
    for image in bpy.data.images:
        if image.name.startswith(('Dog_BaseColor','Dog_Normal')):
            image.filepath='//'+Path(image.filepath).name
    bpy.ops.wm.save_as_mainfile(filepath=str(dest/'Dog_Rigged.blend'))
print('TEXTURE_PATHS_RELINKED',flush=True)
