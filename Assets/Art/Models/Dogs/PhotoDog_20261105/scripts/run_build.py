"""Self-contained build: current reference only; no previous model inputs."""
import sys, math, json
from pathlib import Path
import bpy, numpy as np
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent))
import build_dog as m
import rig_helpers as r

ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'model'
PRE=ROOT/'previews'

original_colors=m.colors_at
def colors_at(p,n,low):
    col=original_colors(p,n,low)
    x,y,z=p.T
    # Gold outer ear with a broad fleshy inner bowl, inset from the rim.
    t=np.clip((z-1.78)/.54,0,1)
    center=.245+.20*t
    half=.092*(1-.73*t)
    inside=np.clip((half-np.abs(np.abs(x)-center))/.022,0,1)
    inside*=np.clip((z-1.82)/.06,0,1)*np.clip((2.33-z)/.06,0,1)
    inside*=np.clip((-n[:,1]-.1)*2,0,1)
    grain=.018*np.sin(x*490+z*270)*np.sin(z*413)
    pink=np.array([.69,.35,.28])[None,:]+grain[:,None]
    col=col*(1-inside[:,None])+pink*inside[:,None]
    return np.clip(col,0,1)
m.colors_at=colors_at

def render_view(scene,cam,label,pos,target,scale,size=800):
    cam.location=pos
    cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale=scale
    scene.render.resolution_x=size;scene.render.resolution_y=size
    scene.render.filepath=str(PRE/(label+'.png'))
    bpy.ops.render.render(write_still=True)

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # read_factory_settings clears the source image loaded at module import.
    m.SOURCE=bpy.data.images.load(str(ROOT/'source/reference.png'))
    body=m.base_geometry()
    dec=body.modifiers.new('Applied_geometry_budget','DECIMATE')
    body.data.calc_loop_triangles();dec.ratio=min(1,15500/len(body.data.loop_triangles))
    m.apply(body,dec)
    ears=[m.ear_mesh(s,False) for s in (-1,1)]
    m.active(body)
    for ear in ears:ear.select_set(True)
    bpy.ops.object.join()
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    for face in body.data.polygons:face.use_smooth=True
    images=m.bake_atlas(body,False,DEST)
    for v in body.data.vertices:v.co*=m.SCALE
    body.data.update();body.name='PhotoDog_Mesh'
    cam=m.studio();scene=bpy.context.scene;scene.camera=cam
    scene.cycles.samples=16;scene.render.fps=30
    scene.render.threads=12
    # Review the actual surface before skinning.
    views=[('front',(0,-4,1.0),(0,-.06,.65),1.54),
           ('side',(4,0,1.15),(0,.12,.65),1.66),
           ('back',(2.6,3.5,1.5),(0,.12,.65),1.66),
           ('face',(0,-4,.88),(0,-.50,.86),.46),
           ('face_angle',(2.2,-4,1.2),(0,-.50,.86),.52),
           ('hero',(2.4,-4,1.6),(0,.04,.65),1.65)]
    for view,pos,target,scale in views:render_view(scene,cam,view,pos,target,scale)
    bpy.ops.wm.save_as_mainfile(filepath=str(DEST/'SurfaceReview.blend'))
    print('SURFACE_REVIEW_READY',flush=True)

if __name__=='__main__':main()
