"""Render full Run and Jump cycles, GIFs, and contact sheets for visual QA."""
import bpy, math, struct, json
import numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];P=ROOT/'previews'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'model/Dog_Rigged.blend'))
sc=bpy.context.scene;rig=bpy.data.objects['Dog_Rig']
sc.render.engine='BLENDER_EEVEE'
sc.render.resolution_x=384;sc.render.resolution_y=384
cam=sc.camera;cam.location=(2.7,-3.8,1.65)
cam.rotation_euler=(Vector((0,.04,.68))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.ortho_scale=1.9

def bytes_image(path):
    im=bpy.data.images.load(str(path),check_existing=False)
    a=np.array(im.pixels[:],dtype=np.float32).reshape(im.size[1],im.size[0],4)[::-1,:,:3]
    bpy.data.images.remove(im)
    return np.clip(a*255+.5,0,255).astype(np.uint8)

def gif(path,frames,delay=7):
    h,w=frames[0].shape[:2]
    palette=bytes(v for i in range(256) for v in (round(((i>>5)&7)*255/7),round(((i>>2)&7)*255/7),round((i&3)*255/3)))
    out=bytearray(b'GIF89a'+struct.pack('<HHBBB',w,h,0xf7,0,0)+palette)
    out+=b'!\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00'
    for frame in frames:
        idx=((frame[:,:,0]>>5)<<5)|((frame[:,:,1]>>5)<<2)|(frame[:,:,2]>>6)
        raw=idx.ravel();codes=[]
        # Clear the LZW table often, retaining 9-bit literal codes throughout.
        for start in range(0,len(raw),200):codes.append(256);codes.extend(raw[start:start+200].tolist())
        codes.append(257);data=bytearray();acc=0;bits=0
        for code in codes:
            acc|=code<<bits;bits+=9
            while bits>=8:data.append(acc&255);acc>>=8;bits-=8
        if bits:data.append(acc&255)
        out+=b'!\xf9\x04\x00'+struct.pack('<H',delay)+b'\x00\x00'
        out+=b','+struct.pack('<HHHHB',0,0,w,h,0)+b'\x08'
        for start in range(0,len(data),255):block=data[start:start+255];out.append(len(block));out+=block
        out.append(0)
    out+=b';';path.write_bytes(out)

def save_sheet(name,frames):
    picks=np.linspace(0,len(frames)-1,12).round().astype(int)
    canvas=np.full((3*192,4*192,4),255,dtype=np.float32)
    for i,j in enumerate(picks):canvas[(i//4)*192:(i//4+1)*192,(i%4)*192:(i%4+1)*192,:3]=frames[j][::2,::2]/255
    im=bpy.data.images.new(name,width=768,height=576,alpha=True)
    im.pixels.foreach_set(canvas[::-1].ravel());im.filepath_raw=str(P/(name+'.png'));im.file_format='PNG';im.save()

for name in ('Run','Jump'):
    act=bpy.data.actions[name];rig.animation_data.action=act
    directory=P/(name+'_frames');directory.mkdir(exist_ok=True)
    frames=[]
    for f in range(int(act.frame_range[0]),int(act.frame_range[1])+1):
        sc.frame_set(f);path=directory/f'{f:03}.png';sc.render.filepath=str(path)
        bpy.ops.render.render(write_still=True);frames.append(bytes_image(path))
    save_sheet(name+'_contact_sheet',frames)
    sampled=frames[::2]
    if name=='Run':sampled=sampled[:-1]*3
    else:sampled+=sampled[-1:]*5
    gif(P/(name+'.gif'),sampled)
    print('CYCLE_RENDERED',name,len(frames),flush=True)
print('CYCLES_COMPLETE',flush=True)
