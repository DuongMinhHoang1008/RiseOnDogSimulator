"""One photo-fitted Shiba hybrid; self-contained, real mesh, portable textures."""
import bpy, sys, math, json
import numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'scripts'))
import surface as m
import rig as r
D=ROOT/'model'; P=ROOT/'previews'
FUR=m.PHOTO[730:850,385:495,:3].copy()
BLUR=sum(np.roll(np.roll(FUR,i,0),j,1) for i in (-9,-6,-3,0,3,6,9) for j in (-9,-6,-3,0,3,6,9))/49
DETAIL=np.clip(FUR-BLUR,-.10,.10)

def colors(p,n,low=False):
    x,y,z=p.T
    # A large valid fur swatch on the photographed shoulder, mirrored across tiles.
    def swatch(a,b):
        u=np.mod(a*12,2);u=np.where(u>1,2-u,u)
        v=np.mod(b*12,2);v=np.where(v>1,2-v,v)
        detail=DETAIL[(v*119).astype(int),(u*109).astype(int)]
        return np.array([.78,.435,.17])+detail*.75
    w=np.abs(n)**5;w/=np.maximum(w.sum(1)[:,None],1e-8)
    base=swatch(y,z)*w[:,0,None]+swatch(x,z)*w[:,1,None]+swatch(x,y)*w[:,2,None]
    # Cream urajiro on throat, breast, belly, paws and the inner limbs.
    breast=np.exp(-((y+.62)/.29)**2)*np.clip((1.44-z)*4,0,1)*np.clip(-n[:,1]*2,0,1)
    belly=np.clip((-n[:,2]-.05)*1.5,0,1)*np.clip((1.12-z)*4,0,1)
    inner=np.clip((.26-np.abs(x))*6,0,1)*np.clip((.87-z)*4,0,1)
    paws=np.clip((.31-z)*3.5,0,1)
    ruff=np.clip((1.52-z)*4,0,1)*np.clip((-y-.39)*4,0,1)
    cream=np.maximum.reduce([breast*.92,belly*.93,inner*.7,paws*.90,ruff*.8])
    creamcol=np.clip(np.array([.94,.86,.70])+(base-np.array([.78,.435,.17]))*.45,0,1)
    base=base*(1-cream[:,None])+creamcol*cream[:,None]
    # Photo projection follows a modeled convex facial plane and nose relief.
    px=839+x*450;py=395-(z-1.49)*450
    portrait=m.sample_photo(px,py)
    radius=np.sqrt(((px-839)/139)**2+((py-400)/146)**2)
    front=np.clip((1.12-radius)/.22,0,1)*np.clip((-n[:,1]-.12)/.42,0,1)
    front*=np.clip((-y-.86)/.12,0,1)
    alpha=m.PHOTO[np.clip(py,0,m.SH-1).astype(int),np.clip(px,0,m.SW-1).astype(int),3]
    front*=alpha
    base=base*(1-front[:,None])+portrait*front[:,None]
    # Soft pink concha with a gold rim, fitted to short triangular ears.
    t=np.clip((z-1.78)/.37,0,1);center=.245+.025*t;half=.105*(1-.80*t)
    ear=np.clip((half-np.abs(np.abs(x)-center))/.023,0,1)
    ear*=np.clip((z-1.81)/.07,0,1)*np.clip((2.13-z)/.05,0,1)*np.clip((-n[:,1]-.1)*2,0,1)
    earcol=base*.46+np.array([.31,.16,.13])
    base=base*(1-ear[:,None])+earcol*ear[:,None]
    tail=np.clip((z-1.60)*3,0,1)*np.clip((y-.29)*3,0,1)
    base=base*(1-tail[:,None]*.45)+creamcol*tail[:,None]*.45
    # Small charcoal claws rather than large painted black paw tips.
    for sy in (-.48,.75):
        for sx in (-.252,.252):
            for dx in (-.047,0,.047):
                claw=np.exp(-(((x-sx-dx)/.009)**4+((y-(sy-.145))/.014)**4+((z-.060)/.022)**4))
                base=base*(1-claw[:,None]*.85)+np.array([.20,.16,.12])*claw[:,None]*.85
    return np.clip(base,0,1)
m.colors_at=colors

# Reload the saved byte PNGs: generated in-memory images shade differently from
# imported sRGB byte textures, despite retaining the same encoded PNG samples.
original_image=m.make_image
def portable_image(name,arr,path,noncolor=False):
    im=original_image(name,arr,path,noncolor)
    bpy.data.images.remove(im)
    im=bpy.data.images.load(str(path));im.name=name
    im.colorspace_settings.name='Non-Color' if noncolor else 'sRGB'
    return im
m.make_image=portable_image

def view(label,pos,target,scale,size=800):
    sc=bpy.context.scene;cam=sc.camera
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale=scale;sc.render.resolution_x=size;sc.render.resolution_y=size
    sc.render.filepath=str(P/(label+'.png'));bpy.ops.render.render(write_still=True)

def surface():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m.SOURCE=bpy.data.images.load(str(ROOT/'source/reference.png'))
    body=m.base_geometry()
    dec=body.modifiers.new('Applied_vertex_budget','DECIMATE')
    body.data.calc_loop_triangles();dec.ratio=min(1,15800/len(body.data.loop_triangles));m.apply(body,dec)
    ears=[m.ear_mesh(s,False) for s in (-1,1)]
    m.active(body)
    for e in ears:e.select_set(True)
    bpy.ops.object.join()
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    for p in body.data.polygons:p.use_smooth=True
    images=m.bake_atlas(body,False,D)
    for v in body.data.vertices:v.co*=m.SCALE
    body.data.update();body.name='ShibaFace_Mesh'
    cam=m.studio();sc=bpy.context.scene;sc.camera=cam;sc.cycles.samples=20;sc.render.fps=30
    views=[('front',(0,-4,1.1),(0,-.05,.60),1.45),('side',(4,0,1.3),(0,.12,.60),1.62),
           ('back',(2.4,3.7,1.6),(0,.12,.62),1.60),('face',(0,-4,.96),(0,-.53,.88),.51),
           ('face_angle',(1.8,-4,1.2),(0,-.52,.88),.57),('hero',(2.3,-4,1.8),(0,.03,.60),1.60)]
    for label,pos,target,scale in views:view(label,pos,target,scale)
    for im in images:im.pack()
    m.active(body);bpy.ops.wm.save_as_mainfile(filepath=str(D/'SurfaceReview.blend'))
    print('SURFACE_READY',flush=True)

def glasses(body):
    """Thin metal rims and temples provide actual side-view geometry."""
    mat=r.solid_mat('Brushed_silver_glasses',(.28,.24,.19))
    bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Metallic'].default_value=.75;bs.inputs['Roughness'].default_value=.3
    parts=[]
    def wire(name,points,radius=.0020):
        cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.resolution_u=1;cu.bevel_depth=radius*m.SCALE;cu.bevel_resolution=2
        sp=cu.splines.new('POLY');sp.points.add(len(points)-1)
        for pt,p in zip(sp.points,points):pt.co=(*r.V(p),1)
        ob=bpy.data.objects.new(name,cu);bpy.context.collection.objects.link(ob);m.active(ob);bpy.ops.object.convert(target='MESH')
        ob=bpy.context.object;ob.data.materials.append(mat)
        g=ob.vertex_groups.new(name='Head');g.add(list(range(len(ob.data.vertices))),1,'REPLACE')
        parts.append(ob)
    for cx,cz,rx,rz in [(-.098,1.625,.105,.092),(.119,1.601,.098,.092)]:
        pts=[]
        for k in range(65):
            a=k*math.tau/64;x=cx+rx*math.cos(a);z=cz+rz*math.sin(a)
            y=-1.077+.038*(x/.26)**2
            pts.append((x,y,z))
        wire('Eyeglass_rim',pts,.0018)
    wire('Nose_bridge',[(-.006,-1.082,1.64),(.008,-1.105,1.652),(.024,-1.083,1.63)],.0018)
    for s in (-1,1):wire('Temple_arm',[(s*.22,-1.05,1.64),(s*.30,-.91,1.65),(s*.32,-.72,1.59)],.0021)
    m.active(body)
    for ob in parts:ob.select_set(True)
    bpy.ops.object.join()

def finish():
    bpy.ops.wm.open_mainfile(filepath=str(D/'SurfaceReview.blend'))
    body=bpy.data.objects['ShibaFace_Mesh'];sc=bpy.context.scene
    lower,upper,seams=r.mouth_slit(body)
    rig=r.create_rig();r.skin(body,rig,lower,upper)
    # Mouth cavity remains behind the closed photographic lips.
    r.mouth_parts(body,rig,False)
    glasses(body)
    for p in body.data.polygons:p.use_smooth=True
    rig['animation_notes']='27 bones; four tail bones fitted to curl; baked 30 fps analytic limb solve.'
    body['reference']='../source/reference.png'
    body['limitations']='Single view estimate; projected human face carries photo lighting; hidden surfaces synthesized.'
    r.make_actions(rig)
    body.data.calc_loop_triangles()
    stats={'vertices':len(body.data.vertices),'triangles':len(body.data.loop_triangles),'bones':len(rig.data.bones),'max_influences':4,
           'mouth_seam_edges':seams,'dimensions_m':[round(float(v),4) for v in body.dimensions],
           'clips':{n:{'seconds':sec,'fps':30,'frames':round(sec*30)+1,'loop':n in ('Idle','Run')} for n,sec in r.CLIPS.items()},
           'forward':'-Y in Blender; +Z up','textures':'2048 PNG base color and tangent normal; packed BLEND; embedded GLB/FBX'}
    (ROOT/'asset_report.json').write_text(json.dumps(stats,indent=2),encoding='utf-8')
    # Keep all file-backed images valid before exporting, even when packed.
    for im in bpy.data.images:
        fp=D/(im.name+'.png')
        if fp.exists():im.filepath=str(fp);im.pack()
    m.active(rig);body.select_set(True)
    r.export(body,rig,D)
    col=bpy.data.collections.new('CHARACTER_export');sc.collection.children.link(col)
    for ob in (body,rig):
        for old in list(ob.users_collection):old.objects.unlink(ob)
        col.objects.link(ob)
    rig.animation_data.action=bpy.data.actions['Idle'];sc.frame_set(1)
    view('hero',(2.3,-4,1.8),(0,.03,.60),1.60,1000)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.region_3d.view_distance=2.0
                area.spaces.active.region_3d.view_location=(0,0,.6)
                area.spaces.active.shading.type='MATERIAL'
    m.active(rig);sc.frame_start=1;sc.frame_end=61
    bpy.ops.wm.save_as_mainfile(filepath=str(D/'Dog_Rigged.blend'))
    r.render_poses(rig,'Shiba')
    print('MODEL_COMPLETE',json.dumps(stats),flush=True)

if __name__=='__main__':
    if '--finish' in sys.argv:finish()
    elif '--retexture' in sys.argv:
        bpy.ops.wm.open_mainfile(filepath=str(D/'SurfaceReview.blend'))
        body=bpy.data.objects['ShibaFace_Mesh']
        for im in list(bpy.data.images):
            if im.name in ('Dog_BaseColor','Dog_Normal'):bpy.data.images.remove(im)
        # Atlas color fitting uses the unscaled construction coordinates.
        for v in body.data.vertices:v.co/=m.SCALE
        images=m.bake_atlas(body,False,D)
        for v in body.data.vertices:v.co*=m.SCALE
        body.data.update()
        for im in images:im.pack()
        for label,pos,target,scale in [('front',(0,-4,1.1),(0,-.05,.60),1.45),('side',(4,0,1.3),(0,.12,.60),1.62),('face',(0,-4,.96),(0,-.53,.88),.51),('face_angle',(1.8,-4,1.2),(0,-.52,.88),.57)]:view(label,pos,target,scale)
        bpy.ops.wm.save_as_mainfile(filepath=str(D/'SurfaceReview.blend'))
    else:surface()
