"""Quadruped skinning + seven baked, game-ready actions. Blender 5.2, no add-ons."""
import bpy, bmesh, math, json
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

ROOT=Path(__file__).resolve().parents[1]
S=.55
FPS=30
CLIPS={'Idle':2.0,'Run':.8,'Jump':1.4,'Bark':1.2,'Poop':3.6,'Pee':3.2,'Bite':.9}
REPORT={}

def V(p):return Vector(p)*S
def clamp(x):return max(0.,min(1.,x))
def smooth(x):
    x=clamp(x);return x*x*(3-2*x)
def envelope(t,a=.20,b=.76):return smooth(t/a)*(1-smooth((t-b)/(1-b)))
def bump(t,c,w):return math.exp(-((t-c)/w)**2)

def active(ob):
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob

def mouth_slit(ob):
    """A short, actual lip seam lets the jaw open without stretching a painted mouth."""
    bm=bmesh.new();bm.from_mesh(ob.data)
    cutz=1.355*S
    faces=[f for f in bm.faces if any(abs(v.co.x)<.145*S and v.co.y<-.975*S and 1.30*S<v.co.z<1.42*S for v in f.verts)]
    geom=set(faces)
    for f in faces:geom.update(f.edges);geom.update(f.verts)
    result=bmesh.ops.bisect_plane(bm,geom=list(geom),dist=.000001,plane_co=Vector((0,0,cutz)),plane_no=Vector((0,0,1)))
    edges=[e for e in result['geom_cut'] if isinstance(e,bmesh.types.BMEdge) and all(abs(v.co.x)<.125*S and v.co.y<-.99*S for v in e.verts)]
    if edges:bmesh.ops.split_edges(bm,edges=edges)
    tag=bm.verts.layers.int.new('LowerLipSeam')
    for v in bm.verts:
        if abs(v.co.z-cutz)<.00001 and v.co.y<-.99*S and abs(v.co.x)<.14*S:
            if v.link_faces and sum(f.calc_center_median().z for f in v.link_faces)/len(v.link_faces)<cutz:
                v[tag]=1
            else:v[tag]=2
    bm.verts.index_update()
    lower={v.index for v in bm.verts if v[tag]==1}
    upper={v.index for v in bm.verts if v[tag]==2}
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(ob.data);bm.free();ob.data.update()
    return lower,upper,len(edges)

def definitions():
    d=[]
    def add(n,a,b,p=None):d.append((n,V(a),V(b),p))
    add('Root',(0,0,0),(0,0,.20))
    add('Pelvis',(0,.58,1.02),(0,.20,1.02),'Root')
    add('Spine',(0,.20,1.02),(0,-.18,1.06),'Pelvis')
    add('Chest',(0,-.18,1.06),(0,-.43,1.15),'Spine')
    add('Neck',(0,-.43,1.15),(0,-.65,1.40),'Chest')
    add('Head',(0,-.65,1.40),(0,-.78,1.71),'Neck')
    add('Jaw',(0,-.84,1.43),(0,-1.02,1.29),'Head')
    tail=[(0,.84,1.19),(0,1.04,1.55),(.015,.95,1.86),(.025,.60,1.995),(.02,.31,1.75)]
    for i in range(4):add(f'Tail_{i+1:02}',tail[i],tail[i+1],'Pelvis' if i==0 else f'Tail_{i:02}')
    for s,side in [(-1,'L'),(1,'R')]:
        add('Ear_'+side,(s*.255,-.72,1.78),(s*.34,-.67,2.04),'Head')
        x=s*.252
        add('FrontUpper_'+side,(x,-.36,.97),(x,-.36,.56),'Chest')
        add('FrontLower_'+side,(x,-.36,.56),(x,-.43,.15),'FrontUpper_'+side)
        add('FrontPaw_'+side,(x,-.43,.15),(x,-.58,.08),'FrontLower_'+side)
        add('RearUpper_'+side,(x,.69,.93),(x,.55,.54),'Pelvis')
        add('RearLower_'+side,(x,.55,.54),(x,.86,.29),'RearUpper_'+side)
        add('RearAnkle_'+side,(x,.86,.29),(x,.75,.09),'RearLower_'+side)
        add('RearPaw_'+side,(x,.75,.09),(x,.62,.075),'RearAnkle_'+side)
    return d

def create_rig():
    bpy.ops.object.armature_add(enter_editmode=True,location=(0,0,0))
    rig=bpy.context.object;rig.name='Dog_Rig'
    rig.data.name='Dog_Quadruped_Skeleton'
    rig.data.edit_bones.remove(rig.data.edit_bones[0])
    for name,a,b,parent in definitions():
        bone=rig.data.edit_bones.new(name);bone.head=a;bone.tail=b
        if parent:bone.parent=rig.data.edit_bones[parent]
        bone.use_connect=False
    bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True;rig.data.display_type='OCTAHEDRAL'
    for bone in rig.pose.bones:bone.rotation_mode='QUATERNION'
    rig['animation_notes']='27 deform bones, no runtime constraints. Seven sampled FK/IK-solved actions at 30 fps.'
    return rig

def distance(p,a,b):
    t=clamp((p-a).dot(b-a)/max((b-a).length_squared,1e-10))
    return (p-(a+(b-a)*t)).length

def proximity(p,names,bones):
    ds=[(name,distance(p,bones[name].head_local,bones[name].tail_local)) for name in names]
    ds.sort(key=lambda a:a[1]);ds=ds[:2]
    w=[1/max(d,.014*S)**3 for name,d in ds];total=sum(w)
    return {ds[i][0]:w[i]/total for i in range(len(ds))}

def skin(ob,rig,lower,upper):
    bones=rig.data.bones
    groups={bone.name:ob.vertex_groups.new(name=bone.name) for bone in bones}
    for v in ob.data.vertices:
        p=v.co;x,y,z=p/S;side='L' if x<0 else 'R'
        if (y>.73 and z>1.28) or (y>.24 and z>1.59):
            weights=proximity(p,[f'Tail_{i:02}' for i in range(1,5)],bones)
        elif z>1.78 and abs(x)>.15 and y<-.40:
            weights={'Ear_'+side:.92,'Head':.08}
        elif y<-.55 and z>1.20:
            h=smooth((z-1.21)/.19)*smooth((-y-.55)/.17)
            weights={'Head':h,'Neck':1-h}
            jw=smooth((-y-.89)/.12)*smooth((1.39-z)/.055)*smooth((.205-abs(x))/.065)
            if v.index in lower:jw=1.
            if v.index in upper:jw=0.
            if jw>0:
                weights={n:w*(1-jw) for n,w in weights.items()};weights['Jaw']=jw
        else:
            weights=proximity(p,['Pelvis','Spine','Chest','Neck'],bones)
            leg=None
            if y<-.08 and z<.97:leg='Front'
            elif y>.28 and z<.98:leg='Rear'
            if leg and abs(x)>.10:
                names=[leg+'Upper_'+side,leg+'Lower_'+side,leg+'Paw_'+side]
                if leg=='Rear':names.insert(2,'RearAnkle_'+side)
                blend=smooth((.98-z)/.28)*smooth((abs(x)-.10)/.09)
                weights={n:w*(1-blend) for n,w in weights.items()}
                for n,w in proximity(p,names,bones).items():weights[n]=weights.get(n,0)+w*blend
        kept=sorted(((n,w) for n,w in weights.items() if w>.0005),key=lambda q:q[1],reverse=True)[:4]
        total=sum(w for n,w in kept)
        for name,w in kept:groups[name].add([v.index],w/total,'REPLACE')
    mod=ob.modifiers.new('Dog_Skin','ARMATURE');mod.object=rig;mod.use_deform_preserve_volume=False
    ob.parent=rig

def solid_mat(name,color):
    mat=bpy.data.materials.new(name);mat.diffuse_color=(*color,1);mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=(*color,1)
    shader.inputs['Roughness'].default_value=.63
    return mat

def mouth_parts(ob,rig,low):
    dark=solid_mat('Mouth_Interior',(.035,.006,.009))
    pink=solid_mat('Tongue',(.34,.055,.073))
    ivory=solid_mat('Teeth',(.78,.73,.60))
    parts=[]
    def bind(item,bone,mat):
        item.data.materials.append(mat)
        g=item.vertex_groups.new(name=bone);g.add(list(range(len(item.data.vertices))),1,'REPLACE')
        for p in item.data.polygons:p.use_smooth=not low
        parts.append(item)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12 if low else 16,ring_count=8,location=V((0,-1.009,1.33)))
    cavity=bpy.context.object;cavity.scale=V((.110,.030,.085));bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bind(cavity,'Head',dark)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,location=V((0,-1.012,1.315)))
    tongue=bpy.context.object;tongue.scale=V((.060,.025,.010));bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bind(tongue,'Jaw',pink)
    for top in (True,False):
        for i in range(6):
            x=(i-2.5)*.025
            bpy.ops.mesh.primitive_cube_add(size=1,location=V((x,-1.026,1.348 if top else 1.325)))
            tooth=bpy.context.object;tooth.scale=V((.021,.014,.022 if top else .013));bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
            bind(tooth,'Head' if top else 'Jaw',ivory)
    active(ob)
    for part in parts:part.select_set(True)
    bpy.ops.object.join()

def matrix_bone(bone,a,b):
    q=(bone.tail_local-bone.head_local).rotation_difference(b-a)
    return Matrix.Translation(a)@q.to_matrix().to_4x4()@bone.matrix_local.to_3x3().to_4x4()

def solve_two(a,c,l1,l2,prefer):
    d=c-a;length=min(max(d.length,abs(l1-l2)+.00001),l1+l2-.00001)
    axis=d.normalized();c=a+axis*length
    along=(l1*l1-l2*l2+length*length)/(2*length)
    height=math.sqrt(max(0,l1*l1-along*along))
    bend=prefer-axis*prefer.dot(axis)
    if bend.length<.001:bend=Vector((1,0,0)).cross(axis)
    b=a+axis*along+bend.normalized()*height
    return b,c

def frame_pose(rig,name,t):
    bones=rig.data.bones;world={};basis={}
    rootpos=Vector((0,0,0));rot={};foot={};toe={}
    jaw=0;pelvis_pitch=0;root_pitch=0;root_roll=0
    for side,s in [('L',-1),('R',1)]:
        foot['Front_'+side]=Vector((s*.252,-.43,.15))
        foot['Rear_'+side]=Vector((s*.252,.75,.09))
    if name=='Idle':
        rootpos.z=.006*(1-math.cos(math.tau*t));rot['Neck']=(0,0,.018*math.sin(math.tau*t))
        rot['Head']=(.012*math.sin(math.tau*t),0,0)
    elif name=='Run':
        phase=math.tau*t;rootpos.z=.024*(1-math.cos(2*phase))
        pelvis_pitch=.035*math.sin(phase);rot['Neck']=(-.07,0,0);rot['Head']=(.025*math.sin(2*phase),0,0)
        for leg in foot:
            diagonal=leg in ('Front_L','Rear_R');p=phase+(0 if diagonal else math.pi)
            foot[leg].y+=.18*math.cos(p);foot[leg].z+=.14*max(0,math.sin(p))**1.5
    elif name=='Jump':
        a=clamp((t-.20)/.60);air=math.sin(math.pi*a)**1.1
        crouch=bump(t,.145,.075);land=bump(t,.86,.065)
        rootpos.z=.47*air-.10*crouch-.075*land
        root_pitch=-.13*math.sin(math.tau*a)*air
        rot['Neck']=(-.08*air+.06*land,0,0)
        for leg in foot:
            foot[leg].z+=.47*air+(.09 if leg.startswith('Front') else .06)*air
            foot[leg].y+=(.04 if leg.startswith('Front') else -.045)*air
    elif name=='Bark':
        e=max(bump(t,.29,.085),bump(t,.65,.085))*math.sin(math.pi*t)**.35
        jaw=math.radians(29)*e;rot['Neck']=(-.06*e,0,0);rot['Head']=(-.06*e,0,0)
        rootpos.y=-.012*e
    elif name=='Poop':
        e=envelope(t);rootpos.z=-.17*e;pelvis_pitch=-.14*e
        rot['Neck']=(.07*e,0,0);rot['Head']=(.10*e,0,0)
        for leg in foot:
            if leg.startswith('Rear'):foot[leg].y-=.13*e
            else:foot[leg].y+=.035*e
    elif name=='Pee':
        e=envelope(t);rootpos.x=-.040*e;rootpos.z=-.025*e;root_roll=-.045*e
        foot['Rear_R']+=Vector((.34*e,.055*e,.29*e))
        toe['Rear_R']=Vector((.11*e,-.13*(1-e),.035*e))
        rot['Neck']=(0,0,.10*e);rot['Head']=(0,0,.20*e)
    elif name=='Bite':
        lunge=bump(t,.60,.14)*math.sin(math.pi*t)
        jaw=math.radians(35)*bump(t,.37,.14)*(1-smooth((t-.47)/.11))
        rootpos.y=-.10*lunge;rootpos.z=-.025*lunge
        rot['Neck']=(.08*lunge,0,0);rot['Head']=(.16*lunge,0,0)
        for leg in foot:
            if leg.startswith('Front'):foot[leg].y-=.06*lunge
    rot['Pelvis']=(pelvis_pitch,0,0)
    rot['Jaw']=(jaw,0,0)
    for i in range(1,5):
        wag=(.065 if name=='Idle' else .10)*math.sin(math.tau*t+(i-1)*.25)
        if name in ('Poop','Pee'):wag*=.35
        rot[f'Tail_{i:02}']=(0,wag*.4,wag)
    for side in ('L','R'):rot['Ear_'+side]=(.018*math.sin(math.tau*t),0,0)
    def set_world(n,m):
        b=bones[n]
        basis[n]=b.matrix_local.inverted()@(bones[b.parent.name].matrix_local@world[b.parent.name].inverted() if b.parent else Matrix.Identity(4))@m
        world[n]=m
    def fk(n):
        b=bones[n];r=Matrix.Identity(4)
        for axis,angle in zip(((1,0,0),(0,1,0),(0,0,1)),rot.get(n,(0,0,0))):
            if angle:
                local=b.matrix_local.to_quaternion().inverted()@Vector(axis)
                r=r@Quaternion(local,angle).to_matrix().to_4x4()
        parent=world[b.parent.name]@bones[b.parent.name].matrix_local.inverted() if b.parent else Matrix.Identity(4)
        set_world(n,parent@b.matrix_local@r)
    rootM=Matrix.Translation(rootpos*S)@Quaternion((1,0,0),root_pitch).to_matrix().to_4x4()@Quaternion((0,1,0),root_roll).to_matrix().to_4x4()@bones['Root'].matrix_local
    set_world('Root',rootM)
    for n in ('Pelvis','Spine','Chest','Neck','Head','Jaw'):fk(n)
    for i in range(1,5):fk(f'Tail_{i:02}')
    for side in ('L','R'):fk('Ear_'+side)
    for side in ('L','R'):
        for leg in ('Front','Rear'):
            upper=leg+'Upper_'+side;lower=leg+'Lower_'+side;paw=leg+'Paw_'+side
            ub,lb=bones[upper],bones[lower]
            parent=world[ub.parent.name]@bones[ub.parent.name].matrix_local.inverted()
            a=parent@ub.head_local
            if leg=='Front':c=foot[leg+'_'+side]*S
            else:c=(foot[leg+'_'+side]+Vector((0,.11,.20)))*S
            preference=Vector((0,1 if leg=='Front' else -1,0))
            b,c=solve_two(a,c,ub.length,lb.length,preference)
            set_world(upper,matrix_bone(ub,a,b));set_world(lower,matrix_bone(lb,b,c))
            if leg=='Rear':
                ankle='RearAnkle_'+side;ab=bones[ankle]
                target=foot[leg+'_'+side]*S
                d=c+(target-c).normalized()*ab.length
                set_world(ankle,matrix_bone(ab,c,d));c=d
            pb=bones[paw];direction=toe.get(leg+'_'+side,pb.tail_local/S-pb.head_local/S)
            set_world(paw,matrix_bone(pb,c,c+direction.normalized()*pb.length))
    for n,m in basis.items():rig.pose.bones[n].matrix_basis=m

def make_actions(rig):
    rig.animation_data_create()
    for action in list(bpy.data.actions):bpy.data.actions.remove(action)
    for name,seconds in CLIPS.items():
        action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data.action=action
        end=round(seconds*FPS)+1
        action['loop']=name in ('Idle','Run');action['fps']=FPS
        action['description']={'Idle':'Breathing and tail sway','Run':'In-place diagonal quadruped gait','Jump':'Anticipation, takeoff, tuck, landing','Bark':'Two head/neck and jaw barks','Poop':'Squat, hold, stand','Pee':'Right hind-leg raise, hold, lower','Bite':'Wind-up, jaw open, snap and recover'}[name]
        for f in range(1,end+1):
            bpy.context.scene.frame_set(f)
            frame_pose(rig,name,(f-1)/(end-1))
            for pb in rig.pose.bones:
                pb.keyframe_insert('location',frame=f,group=pb.name)
                pb.keyframe_insert('rotation_quaternion',frame=f,group=pb.name)
                pb.keyframe_insert('scale',frame=f,group=pb.name)
        print('ACTION_BAKED',name,end,flush=True)
    rig.animation_data.action=bpy.data.actions['Idle']
    bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=61;bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()

def checks(ob,rig):
    bad=[]
    for v in ob.data.vertices:
        weights=[g.weight for g in v.groups if g.weight>0]
        if not weights or len(weights)>4 or abs(sum(weights)-1)>.001:bad.append(v.index)
    assert not bad,('invalid weights',bad[:10])
    assert len(rig.data.bones)==27
    assert set(CLIPS)=={a.name for a in bpy.data.actions}
    samples={}
    dg=bpy.context.evaluated_depsgraph_get()
    for name,seconds in CLIPS.items():
        rig.animation_data.action=bpy.data.actions[name]
        bounds=[]
        for fraction in (0,.25,.5,.75,1):
            bpy.context.scene.frame_set(1+round(seconds*FPS*fraction));bpy.context.view_layer.update()
            evaluated=ob.evaluated_get(dg);mesh=evaluated.to_mesh()
            coords=[v.co for v in mesh.vertices]
            assert all(math.isfinite(c) for p in coords for c in p),(name,'NaN skin')
            lo=[min(p[i] for p in coords) for i in range(3)];hi=[max(p[i] for p in coords) for i in range(3)]
            assert all(hi[i]-lo[i]<2.0 for i in range(3)),(name,'exploded skin',lo,hi)
            assert lo[2]>-.18,(name,'excessive below-ground geometry',lo)
            bounds.append({'t':fraction,'min':[round(c,4) for c in lo],'max':[round(c,4) for c in hi]})
            evaluated.to_mesh_clear()
        samples[name]=bounds
    rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
    return samples

def export(ob,rig,dest):
    active(rig);ob.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(dest/'Dog_Rigged.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},
        apply_unit_scale=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',
        add_leaf_bones=False,use_armature_deform_only=True,armature_nodetype='NULL',bake_anim=True,
        bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0.0,
        bake_anim_step=1.0,path_mode='COPY',embed_textures=True)
    bpy.ops.export_scene.gltf(filepath=str(dest/'Dog_Rigged.glb'),use_selection=True,export_format='GLB',
        export_animations=True,export_animation_mode='ACTIONS',export_bake_animation=True,
        export_skins=True,export_yup=True,export_materials='EXPORT',export_morph=False)

def render_poses(rig,key):
    scene=bpy.context.scene;scene.render.resolution_x=720;scene.render.resolution_y=660
    scene.cycles.samples=16
    cam=scene.camera;cam.location=(2.5,-3.7,2.0);cam.data.ortho_scale=1.70
    cam.rotation_euler=(Vector((0,.03,.59))-cam.location).to_track_quat('-Z','Y').to_euler()
    poses={'Idle':.25,'Run':.17,'Jump':.5,'Bark':.29,'Poop':.50,'Pee':.50,'Bite':.40}
    for name,fraction in poses.items():
        rig.animation_data.action=bpy.data.actions[name]
        scene.frame_set(1+round(CLIPS[name]*FPS*fraction))
        if name=='Jump':
            cam.data.ortho_scale=1.90
            cam.rotation_euler=(Vector((0,.03,.75))-cam.location).to_track_quat('-Z','Y').to_euler()
        else:
            cam.data.ortho_scale=1.70
            cam.rotation_euler=(Vector((0,.03,.59))-cam.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(ROOT/'previews'/f'{key}_anim_{name}.png')
        bpy.ops.render.render(write_still=True)
    rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)

def main():
    for key in ('low_poly','realistic_low_vertex'):
        dest=ROOT/key
        bpy.ops.wm.open_mainfile(filepath=str(dest/'Dog.blend'))
        for image in bpy.data.images:
            for filename in ('Dog_BaseColor.png','Dog_Normal.png'):
                if image.name.startswith(Path(filename).stem) and (dest/filename).exists():
                    if image.packed_file:image.unpack(method='REMOVE')
                    image.filepath=str(dest/filename)
                    image.reload();image.pack()
        scene=bpy.context.scene;scene.render.fps=FPS
        ob=next(o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Dog_'))
        lower,upper,seams=mouth_slit(ob)
        rig=create_rig();skin(ob,rig,lower,upper);mouth_parts(ob,rig,key=='low_poly')
        ob.name='Dog_Rigged_'+key
        make_actions(rig)
        samples=checks(ob,rig)
        ob.data.calc_loop_triangles()
        REPORT[key]={'bones':len(rig.data.bones),'vertices':len(ob.data.vertices),'triangles':len(ob.data.loop_triangles),
            'materials':len(ob.data.materials),'max_influences':4,'mouth_slit_edges':seams,
            'clips':{name:{'seconds':seconds,'frames':round(seconds*FPS)+1,'loop':name in ('Idle','Run')} for name,seconds in CLIPS.items()},
            'weights':'PASS','sampled_deformation':'PASS','sample_bounds':samples}
        export(ob,rig,dest)
        active(rig);rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)
        bpy.ops.wm.save_as_mainfile(filepath=str(dest/'Dog_Rigged.blend'))
        (ROOT/'rig_animation_report.json').write_text(json.dumps(REPORT,indent=2),encoding='utf-8')
        render_poses(rig,key)
        print('RIG_COMPLETE',key,REPORT[key]['vertices'],flush=True)
    print('ALL_RIGS_COMPLETE',flush=True)

if __name__=='__main__':main()
