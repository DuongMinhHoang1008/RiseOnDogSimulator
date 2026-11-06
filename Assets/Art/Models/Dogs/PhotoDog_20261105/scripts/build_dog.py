"""Create one smooth reference dog, editable photo-relief mesh, rig and seven actions.
Run: blender --background --factory-startup --python this_file.py
No external Python dependencies. All mesh geometry is real, including face relief.
"""
import bpy, math, json, os, sys, time
import numpy as np
from mathutils import Vector
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCALE = 0.55
SOURCE = bpy.data.images.load(str(ROOT / 'source/reference.png'), check_existing=True)
SW, SH = SOURCE.size
PHOTO = np.array(SOURCE.pixels[:], dtype=np.float32).reshape(SH, SW, 4)[::-1].copy()
print('REFERENCE_SAMPLE_RGBA',PHOTO[460,440].tolist(),flush=True)
REPORT = {}

def active(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

def apply(obj, mod):
    active(obj)
    bpy.ops.object.modifier_apply(modifier=mod.name)

def ellipsoid(name, pos, size, parts):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=20, location=pos)
    ob=bpy.context.object
    ob.name=name
    ob.scale=size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    parts.append(ob)
    return ob

def limb(name, a, b, ra, rb, parts):
    a,b=Vector(a),Vector(b)
    bpy.ops.mesh.primitive_cone_add(vertices=20, radius1=ra, radius2=rb, depth=(b-a).length, location=(a+b)/2)
    ob=bpy.context.object
    ob.name=name
    ob.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    parts.append(ob)

def tube(name, points, radii, parts, sides=20):
    verts=[]; faces=[]
    for i,p in enumerate(points):
        tangent=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
        tangent.normalize()
        u=Vector((1,0,0)); v=tangent.cross(u).normalized()
        for k in range(sides):
            d=u*math.cos(k*math.tau/sides)+v*math.sin(k*math.tau/sides)
            verts.append(Vector(p)+radii[i]*d)
    for i in range(len(points)-1):
        for k in range(sides):
            faces.append((i*sides+k,i*sides+(k+1)%sides,(i+1)*sides+(k+1)%sides,(i+1)*sides+k))
    faces += [tuple(reversed(range(sides))),tuple((len(points)-1)*sides+k for k in range(sides))]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob);parts.append(ob)

def base_geometry():
    parts=[]
    ellipsoid('Ribcage',(0,.12,1.02),(.345,.66,.385),parts)
    ellipsoid('Loin',(0,.60,1.07),(.325,.39,.33),parts)
    ellipsoid('Chest',(0,-.38,1.00),(.335,.32,.44),parts)
    ellipsoid('Neck_ruff',(0,-.53,1.30),(.32,.33,.40),parts)
    ellipsoid('Cranium',(0,-.72,1.53),(.345,.295,.345),parts)
    for s in (-1,1):
        x=.252*s
        ellipsoid('Scapula',(x,-.34,1.00),(.15,.23,.31),parts)
        limb('Forearm',(x,-.39,.31),(x,-.36,.88),.069,.105,parts)
        ellipsoid('Elbow',(x,-.35,.70),(.087,.092,.11),parts)
        limb('Front_pastern',(x,-.435,.105),(x,-.39,.36),.063,.070,parts)
        ellipsoid('Front_paw',(x,-.48,.085),(.098,.16,.085),parts)
        ellipsoid('Haunch',(x,.65,.91),(.19,.25,.30),parts)
        limb('Rear_thigh',(x,.55,.54),(x,.69,.92),.10,.15,parts)
        ellipsoid('Stifle',(x,.56,.55),(.105,.12,.12),parts)
        limb('Rear_shin',(x,.86,.29),(x,.56,.56),.061,.095,parts)
        ellipsoid('Hock',(x,.85,.29),(.066,.075,.085),parts)
        limb('Rear_pastern',(x,.80,.10),(x,.86,.29),.058,.060,parts)
        ellipsoid('Rear_paw',(x,.75,.075),(.092,.15,.075),parts)
    # Curled, tapered shiba tail: open curl with visible negative space.
    pts=[(0,.84,1.19),(0,1.02,1.40),(.01,1.05,1.64),(.015,.96,1.86),
         (.02,.76,1.99),(.025,.54,1.98),(.025,.36,1.87),(.02,.31,1.75)]
    # Catmull-Rom interpolation avoids a visibly segmented bend.
    smooth=[]; radii=[]
    rs=[.075,.073,.067,.060,.050,.040,.027,.008]
    for i in range(len(pts)-1):
        a,b,c,d=[Vector(pts[max(0,min(len(pts)-1,j))]) for j in (i-1,i,i+1,i+2)]
        for j in range(5):
            t=j/5
            smooth.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
            radii.append(rs[i]*(1-t)+rs[i+1]*t)
    smooth.append(Vector(pts[-1]));radii.append(rs[-1])
    tube('Curled_tail',smooth,radii,parts)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in parts:ob.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join()
    body=bpy.context.object;body.name='ReferenceDog_Master'
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    active(body);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    remesh=body.modifiers.new('Continuous_anatomy','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.018
    apply(body,remesh)
    sm=body.modifiers.new('Anatomical_smoothing','SMOOTH');sm.factor=1.3;sm.iterations=6;apply(body,sm)
    # An oval human face, flush with the forehead and proud of the side cheeks.
    # No paired cheek spheres: these made the previous face look sunken.
    for vert in body.data.vertices:
        x,y,z=vert.co
        if y < -.67 and 1.18 < z < 1.56:
            jaw_taper=max(0,min(1,(1.56-z)/.32))
            head_only=max(0,min(1,(-y-.67)/.13))
            vert.co.x*=1-.18*jaw_taper*head_only
        x,y,z=vert.co
        if y < -.82 and z > 1.18:
            radius=math.sqrt((x/.278)**2+((z-1.495)/.285)**2)
            blend=max(0,min(1,(1.21-radius)/.36))
            blend=blend*blend*(3-2*blend)
            blend*=max(0,min(1,(-y-.82)/.09))
            # Broad, gently convex facial plane. The mouth/chin no longer recede
            # behind two separate spherical cheeks.
            target=-1.055+.052*(x/.26)**2+.024*((z-1.50)/.28)**2
            nose=.050*math.exp(-((x/.042)**2+((z-1.475)/.060)**2))
            bridge=.016*math.exp(-((x/.033)**2+((z-1.55)/.080)**2))
            lips=.014*math.exp(-((x/.102)**2+((z-1.365)/.030)**2))
            chin=.009*math.exp(-((x/.105)**2+((z-1.275)/.055)**2))
            target-=nose+bridge+lips+chin
            vert.co.y=y*(1-blend)+target*blend
    body.data.update()
    return body

def ear_mesh(s, low):
    # Closed solid ears, gently concave inner face and a thick folded rim.
    outline=[(.13,1.77),(.18,1.98),(.46,2.36),(.48,2.28),(.40,1.92),(.30,1.73)]
    verts=[]
    for x,z in outline:verts.append((s*x,-.79+(z-1.75)*.22,z))
    verts.append((s*.31,-.757,2.02))
    for x,z in outline:verts.append((s*x,-.65+(z-1.75)*.22,z))
    verts.append((s*.31,-.62,2.02))
    faces=[]
    for i in range(6):faces.append((6,i,(i+1)%6))
    for i in range(6):faces.append((13,7+(i+1)%6,7+i))
    for i in range(6):faces.append((i,7+i,7+(i+1)%6,(i+1)%6))
    mesh=bpy.data.meshes.new('Ear');mesh.from_pydata(verts,[],faces);mesh.update()
    ob=bpy.data.objects.new('Ear',mesh);bpy.context.collection.objects.link(ob)
    active(ob);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    if not low:
        mod=ob.modifiers.new('Rounded_ear_edges','BEVEL');mod.width=.018;mod.segments=2;apply(ob,mod)
        mod=ob.modifiers.new('Ear_surface','SUBSURF');mod.levels=1;apply(ob,mod)
    return ob

def sample_photo(px,py):
    px=np.clip(px,0,SW-1);py=np.clip(py,0,SH-1)
    ix=px.astype(np.int32);iy=py.astype(np.int32)
    return PHOTO[iy,ix,:3]

def colors_at(p,n,low):
    x,y,z=p.T; nx,ny,nz=n.T
    # Mirrored photograph swatch, triplanar in model space: no extra fur geometry.
    def tile(a,b):
        u=np.mod(a*2.3,2);u=np.where(u>1,2-u,u)
        v=np.mod(b*2.3,2);v=np.where(v>1,2-v,v)
        return sample_photo(688+u*65,550+v*105)
    weights=np.abs(n)**5;weights/=np.maximum(weights.sum(axis=1)[:,None],1e-8)
    fur=tile(y,z)*weights[:,0,None]+tile(x,z)*weights[:,1,None]+tile(x,y)*weights[:,2,None]
    base=np.clip(fur*np.array([.98,.92,.84]),0,1)
    if low:
        # Deliberately broad color planes for the faceted version.
        variation=.04*np.sin(x*9+y*4+z*6)
        base=np.tile(np.array([.67,.405,.18]),(len(p),1))+variation[:,None]
    # Shiba urajiro: chest, underside, inner legs, lower cheeks.
    belly=np.clip((-nz-.15)*1.5,0,1)*np.clip((1.12-z)*3.0,0,1)
    chest=np.exp(-((y+.64)/.22)**2)*np.clip((1.40-z)*4,0,1)*np.clip((-ny)*2,0,1)
    inner=np.clip((.23-np.abs(x))*7,0,1)*np.clip((.78-z)*3,0,1)
    cheek=np.exp(-((z-1.29)/.115)**2)*np.clip((-y-.80)*4,0,1)
    cream=np.maximum.reduce([belly*.80,chest*.48,inner*.68,cheek*.18])
    creamcol=np.clip(base*.28+np.array([.68,.64,.54]),0,1)
    base=base*(1-cream[:,None])+creamcol*cream[:,None]
    # Front head projection from the supplied photograph, wrapped into actual relief.
    px=520+x*490;py=405-(z-1.48)*470
    portrait=sample_photo(px,py)
    front=np.clip((-ny-.10)/.50,0,1)
    front*=np.clip((-y-.87)/.10,0,1)*np.clip((z-1.205)/.075,0,1)
    front*=np.clip((1.895-z)/.07,0,1)
    # The supplied cutout has black/red fringe pixels outside the right cheek.
    # Keep the projection inside the face and feather into the modeled fur.
    radius=np.sqrt(((px-520)/145)**2+((py-405)/136)**2)
    front*=np.clip((1.04-radius)/.20,0,1)
    pixel_alpha=PHOTO[np.clip(py,0,SH-1).astype(np.int32),np.clip(px,0,SW-1).astype(np.int32),3]
    valid=~((portrait[:,0]>.7)&(portrait[:,1]<.15)&(portrait[:,2]<.15))
    front*=pixel_alpha*valid
    base=base*(1-front[:,None])+portrait*front[:,None]
    # Warm recessed ear interiors, surrounded by an ochre rim.
    ear=np.clip((z-1.79)/.08,0,1)*np.clip((-.69-y)*10,0,1)
    ear*=np.clip((np.abs(x)-.16)/.06,0,1)*np.clip((.365-np.abs(x))/.055,0,1)
    earcol=base*.43+np.array([.10,.055,.045])
    base=base*(1-ear[:,None]*.8)+earcol*(ear[:,None]*.8)
    # Low-cost claws and toe separations are painted, not separate meshes.
    for sy in (-.48,.75):
        for sx in (-.252,.252):
            for dx in (-.052,0,.052):
                claw=np.exp(-(((x-sx-dx)/.014)**4+((y-(sy-.133))/.025)**4+((z-.045)/.032)**4))
                base=base*(1-claw[:,None]*.9)+np.array([.12,.105,.09])*claw[:,None]*.9
    return np.clip(base,0,1)

def make_image(name, arr, path, noncolor=False):
    h,w=arr.shape[:2]
    im=bpy.data.images.new(name,width=w,height=h,alpha=True)
    if noncolor:im.colorspace_settings.name='Non-Color'
    rgba=np.ones((h,w,4),dtype=np.float32)
    # Blender's generated PNG buffer preserves these sRGB samples verbatim.
    rgba[:,:,:3]=arr
    im.pixels.foreach_set(rgba.ravel())
    im.filepath_raw=str(path);im.file_format='PNG';im.save()
    return im

def bake_atlas(ob, low, dest):
    active(ob);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(62),island_margin=.012)
    bpy.ops.object.mode_set(mode='OBJECT')
    mesh=ob.data;mesh.calc_loop_triangles()
    size=1024 if low else 2048
    atlas=np.zeros((size,size,3),dtype=np.float32); mask=np.zeros((size,size),bool)
    uv=mesh.uv_layers.active.data
    for tri in mesh.loop_triangles:
        coords=np.array([uv[i].uv[:] for i in tri.loops])* (size-1)
        lo=np.maximum(np.floor(coords.min(axis=0)).astype(int),0)
        hi=np.minimum(np.ceil(coords.max(axis=0)).astype(int),size-1)
        gx,gy=np.meshgrid(np.arange(lo[0],hi[0]+1),np.arange(lo[1],hi[1]+1))
        q=np.stack([gx.ravel(),gy.ravel()],axis=1)
        a,b,c=coords;v0=b-a;v1=c-a;den=v0[0]*v1[1]-v1[0]*v0[1]
        if abs(den)<1e-9:continue
        d=q-a
        wb=(d[:,0]*v1[1]-v1[0]*d[:,1])/den;wc=(v0[0]*d[:,1]-d[:,0]*v0[1])/den;wa=1-wb-wc
        inside=(wa>=-.001)&(wb>=-.001)&(wc>=-.001)
        if not np.any(inside):continue
        w=np.stack([wa[inside],wb[inside],wc[inside]],axis=1)
        p=np.array([mesh.vertices[i].co[:] for i in tri.vertices])
        ns=np.array([mesh.vertices[i].normal[:] for i in tri.vertices])
        points=w@p;normals=w@ns;normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-8)
        ids=q[inside].astype(int)
        atlas[ids[:,1],ids[:,0]]=colors_at(points,normals,low)
        mask[ids[:,1],ids[:,0]]=True
    # Extrude island edge pixels to protect mipmaps at UV seams.
    for _ in range(10):
        oldmask=mask.copy();oldatlas=atlas.copy()
        for axis,shift in ((0,1),(0,-1),(1,1),(1,-1)):
            available=np.roll(oldmask,shift,axis=axis)&~mask
            atlas[available]=np.roll(oldatlas,shift,axis=axis)[available];mask[available]=True
    atlas[~mask]=[.48,.28,.13]
    color=make_image('Dog_BaseColor',atlas,dest/'Dog_BaseColor.png')
    normal=None
    if not low:
        # Tangent-space micro-normal texture. Fur detail comes from maps, not vertices.
        lum=atlas@np.array([.2126,.7152,.0722])
        dy,dx=np.gradient(lum)
        dx*=1.7;dy*=1.7
        normaldata=np.stack([-dx,-dy,np.ones_like(dx)],axis=2)
        normaldata/=np.linalg.norm(normaldata,axis=2)[:,:,None]
        normal=make_image('Dog_Normal',normaldata*.5+.5,dest/'Dog_Normal.png',True)
    mat=bpy.data.materials.new('Dog_Fur_and_ReferenceFace');mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;bsdf=nodes.get('Principled BSDF')
    bsdf.inputs['Roughness'].default_value=.83
    bsdf.inputs['Specular IOR Level'].default_value=.24
    tex=nodes.new('ShaderNodeTexImage');tex.image=color;tex.location=(-450,150)
    links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
    if normal:
        nt=nodes.new('ShaderNodeTexImage');nt.image=normal;nt.location=(-450,-150)
        nm=nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.35;nm.location=(-180,-100)
        links.new(nt.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],bsdf.inputs['Normal'])
    ob.data.materials.clear();ob.data.materials.append(mat)
    return [color]+([normal] if normal else [])

def camera_at(name,pos,target,ortho=2.0):
    bpy.ops.object.camera_add(location=pos)
    cam=bpy.context.object;cam.name=name
    cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.type='ORTHO';cam.data.ortho_scale=ortho
    return cam

def studio():
    scene=bpy.context.scene
    scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
    scene.cycles.device='CPU';scene.render.threads_mode='FIXED';scene.render.threads=12
    scene.render.resolution_x=1200;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.world=bpy.data.worlds.new('Studio_world')
    scene.world.use_nodes=True
    scene.world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.17,.20,.24,1)
    scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.4
    scene.view_settings.view_transform='AgX'
    scene.view_settings.look='AgX - Medium High Contrast'
    scene.view_settings.exposure=-.15
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.007))
    floor=bpy.context.object;floor.name='STUDIO_Floor'
    mat=bpy.data.materials.new('Studio_slate');mat.diffuse_color=(.047,.067,.085,1);mat.use_nodes=True
    mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.047,.067,.085,1)
    mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.87
    floor.data.materials.append(mat)
    for name,pos,power,size in [('Key',(-2,-3,4),480,3),('Fill',(3,-1,2),230,2.5),('Rim',(0,3,3),560,2)]:
        bpy.ops.object.light_add(type='AREA',location=pos);lamp=bpy.context.object;lamp.name='STUDIO_'+name
        lamp.data.energy=power;lamp.data.shape='DISK';lamp.data.size=size
        lamp.rotation_euler=(Vector((0,0,.55))-lamp.location).to_track_quat('-Z','Y').to_euler()
    return camera_at('STUDIO_Hero_Camera',(2.5,-3.7,2.0),(0,.05,.59),1.65)

def export_model(ob, dest):
    active(ob)
    bpy.ops.export_scene.fbx(filepath=str(dest/'Dog.fbx'),use_selection=True,object_types={'MESH'},
        apply_unit_scale=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,
        mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=True)
    bpy.ops.export_scene.gltf(filepath=str(dest/'Dog.glb'),use_selection=True,export_format='GLB',
        export_yup=True,export_materials='EXPORT',export_normals=True,export_texcoords=True)

def run_variant(master,low):
    key='low_poly' if low else 'realistic_low_vertex';dest=ROOT/key
    ob=master.copy();ob.data=master.data.copy();bpy.context.collection.objects.link(ob)
    ob.name='Dog_LowPoly' if low else 'Dog_Realistic_LowVertex'
    master.hide_render=True;master.hide_viewport=True
    active(ob)
    dec=ob.modifiers.new('Vertex_budget','DECIMATE')
    ob.data.calc_loop_triangles()
    dec.ratio=min(1,(3000 if low else 11000)/len(ob.data.loop_triangles));apply(ob,dec)
    ears=[ear_mesh(s,low) for s in (-1,1)]
    active(ob)
    for ear in ears:ear.select_set(True)
    bpy.ops.object.join()
    # Normalize winding and normals after joining closed anatomy components.
    active(ob);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    for face in ob.data.polygons:face.use_smooth=not low
    images=bake_atlas(ob,low,dest)
    for vert in ob.data.vertices:vert.co*=SCALE
    ob.data.update()
    bpy.context.view_layer.update()
    ob['reference']='source/reference.png';ob['style']=key;ob['rigged']=False
    ob['notes']='Face revision 2: no cheek spheres; tapered jaw; forward convex human facial plane. Face crop in source/face_reference.png.'
    ob.data.calc_loop_triangles()
    stats={'vertices':len(ob.data.vertices),'triangles':len(ob.data.loop_triangles),
           'polygons':len(ob.data.polygons),'materials':len(ob.data.materials),
           'texture_size':1024 if low else 2048,'dimensions_m':[round(v,4) for v in ob.dimensions],
           'rigged':False}
    export_model(ob,dest)
    for im in images:im.pack()
    cam=studio();bpy.context.scene.camera=cam
    # Put the actual mesh in a clean MODEL collection; studio content stays separate.
    col=bpy.data.collections.new('MODEL_export_this');bpy.context.scene.collection.children.link(col)
    for existing in list(ob.users_collection):existing.objects.unlink(ob)
    col.objects.link(ob)
    active(ob)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.region_3d.view_distance=2.2
                area.spaces.active.region_3d.view_location=(0,0,.55)
                area.spaces.active.shading.type='MATERIAL'
    # Remove hidden construction source from the delivered scene.
    bpy.data.objects.remove(master,do_unlink=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(dest/'Dog.blend'))
    scene=bpy.context.scene
    for view,pos in [('hero',(2.5,-3.7,2.0)),('front',(0,-4,1.20)),('side',(4,0,1.35)),('back',(2.6,3.5,1.8)),('face',(0,-4,.90)),('face_angle',(2.8,-4,1.5))]:
        target=(0,-.48,.84) if view.startswith('face') else (0,.04,.58)
        cam.data.ortho_scale=.48 if view=='face' else (.56 if view=='face_angle' else 1.65)
        cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
        if view!='hero':scene.render.resolution_x=800;scene.render.resolution_y=800;scene.cycles.samples=20
        scene.render.filepath=str(ROOT/'previews'/f'{key}_{view}.png')
        bpy.ops.render.render(write_still=True)
    REPORT[key]=stats
    print('VARIANT_COMPLETE',key,json.dumps(stats),flush=True)

def main():
    for low in (True,False):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        master=base_geometry()
        run_variant(master,low)
        (ROOT/'model_stats.json').write_text(json.dumps(REPORT,indent=2),encoding='utf-8')
    print('ALL_MODELS_COMPLETE',json.dumps(REPORT),flush=True)

if __name__=='__main__':main()

