import bpy, json, math, heapq, hashlib
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

OUT=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
SKIN=json.loads((OUT/'skin_variants.json').read_text(encoding='utf-8'))
HANDS=json.loads((OUT/'rig_hands_geometry.json').read_text())
COMP={r['name']:r['components'] for r in json.loads((OUT/'skin_components.json').read_text())}
SRC=bpy.data.scenes['FEB_Soldiers']
assert 'FEB_Soldiers_Rig' not in bpy.data.scenes, 'Rig scene already exists; inspect before replacing.'
scene=bpy.data.scenes.new('FEB_Soldiers_Rig'); scene.world=SRC.world
bpy.context.window.scene=scene
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
scene.render.resolution_percentage=100
scene.render.resolution_x=2000;scene.render.resolution_y=1000
for ob in SRC.objects:
    if ob.type=='LIGHT':scene.collection.objects.link(ob)
cam=SRC.camera.copy();cam.data=SRC.camera.data.copy();cam.name='Camera_FEB_Rigs';scene.collection.objects.link(cam);scene.camera=cam
report=[]

def smooth(a,b,x):
    t=float(np.clip((x-a)/(b-a),0,1));return t*t*(3-2*t)
def norm(w):
    w={k:float(v) for k,v in w.items() if v>1e-5}
    w=dict(sorted(w.items(),key=lambda kv:kv[1],reverse=True)[:4]);s=sum(w.values())
    return {k:v/s for k,v in w.items()}
def mix(a,b,t):
    w={k:v*(1-t) for k,v in a.items()}
    for k,v in b.items():w[k]=w.get(k,0)+v*t
    return norm(w)
def segdist(p,a,b):
    d=b-a;t=float(np.clip(np.dot(p-a,d)/np.dot(d,d),0,1));return float(np.linalg.norm(p-(a+t*d)))
def geodesic(P,adj,seeds):
    D=np.full(len(P),1e6);q=[]
    for i in seeds:D[i]=0;heapq.heappush(q,(0,i))
    while q:
        d,u=heapq.heappop(q)
        if d>D[u]:continue
        for v,w in adj[u].items():
            nd=d+w
            if nd<D[v]:D[v]=nd;heapq.heappush(q,(nd,v))
    return D
def hand_landmarks(h,side):
    P=np.array(h['points']);D=np.array(h['distances']);adj=[{} for _ in P]
    for f in h['faces']:
        for a,b in zip(f,f[1:]+f[:1]):adj[a][b]=adj[b][a]=float(np.linalg.norm(P[a]-P[b]))
    # Four distal fingers become separate connected branches beyond the palm.
    cut=.16;seen=set();branches=[]
    for i in np.where(D>cut)[0]:
        if i in seen:continue
        todo=[int(i)];seen.add(int(i));ids=[]
        while todo:
            u=todo.pop();ids.append(u)
            for v in adj[u]:
                if D[v]>cut and v not in seen:seen.add(v);todo.append(v)
        if len(ids)>8:branches.append(ids)
    assert len(branches)==4,(side,len(branches))
    tips=[max(ids,key=lambda j:D[j]) for ids in branches]
    tips.sort(key=lambda j:P[j,1])
    sign=1 if side=='L' else -1
    maxima=[i for i in range(len(P)) if all(D[i]>=D[j] for j in adj[i])]
    thumb=min(maxima,key=lambda j:sign*P[j,0])
    specs=[];finger_fields=[]
    for name,tip in list(zip(['index','middle','ring','pinky'],tips))+[('thumb',thumb)]:
        G=geodesic(P,adj,[tip]);base=.111 if name!='thumb' else .060
        if name=='pinky':base=.105
        end=D[tip]-.005
        levels=[base,base+(end-base)*.46,base+(end-base)*.76,end]
        pts=[]
        corridor=G+D-D[tip]
        for level in levels:
            score=np.abs(D-level)+np.maximum(corridor-.009,0)*1.8
            ids=np.argsort(score)[:7]
            weight=np.exp(-score[ids]/.009)
            pts.append(np.average(P[ids],axis=0,weights=weight))
        for i in range(3):specs.append((f'{name}_{i+1:02}.{side}',pts[i],pts[i+1]))
        finger_fields.append((name,tip,G,base,pts))
    # Finger ownership is determined along the surface graph, preventing leakage
    # between nearby fingertips in this already-curled rest pose.
    weights=[]
    for i,p in enumerate(P):
        if D[i]<.065:weights.append({f'hand.{side}':1});continue
        scores=[]
        for name,tip,G,base,pts in finger_fields:
            dist=min(segdist(p,pts[j],pts[j+1]) for j in range(3))
            scores.append(max(0,G[i]+D[i]-D[tip])*.55+dist)
        name,tip,G,base,pts=finger_fields[int(np.argmin(scores))]
        lengths=[np.linalg.norm(pts[j+1]-pts[j]) for j in range(3)]
        values=[]
        for j in range(3):
            distance=segdist(p,pts[j],pts[j+1]);values.append(1/(distance+.005)**5)
        fw={f'{name}_{j+1:02}.{side}':values[j] for j in range(3)}
        t=smooth(base-.015,base+.018,D[i])
        weights.append(mix({f'hand.{side}':1},norm(fw),t))
    return specs,weights

for idx in range(1,7):
    prefix=f'FEB_{idx:02}_';sources=[o for o in bpy.data.collections[f'FEB_{idx:02}'].objects if o.type=='MESH']
    collection=bpy.data.collections.new(f'FEB_Soldado_{idx:02}_Rig');scene.collection.children.link(collection)
    ardata=bpy.data.armatures.new(f'FEB_Soldado_{idx:02}_Esqueleto')
    arm=bpy.data.objects.new(f'FEB_Soldado_{idx:02}_Rig',ardata);collection.objects.link(arm)
    arm.location=sources[0].location.copy();arm.show_in_front=True;ardata.display_type='OCTAHEDRAL'
    arm['personagem']=idx;arm['instrucoes']='Pose Mode: ossos FK. IK opcional: propriedades IK_Braco/Perna; CTRL_mao/pe e POLE controlam membros. CTRL_root move todo o personagem.'
    bones={};parents={};deform={};landmarks={};handweights={}
    def bone(name,a,b,parent=None,use_deform=True):
        bones[name]=(np.array(a,dtype=float),np.array(b,dtype=float));parents[name]=parent;deform[name]=use_deform
    bone('CTRL_root',(0,0,0),(0,0,.20),use_deform=False)
    bone('hips',(0,-.025,.94),(0,-.025,1.08),'CTRL_root')
    bone('spine',(0,-.025,1.08),(0,-.025,1.22),'hips')
    bone('chest',(0,-.025,1.22),(0,-.025,1.36),'spine')
    bone('upper_chest',(0,-.025,1.36),(0,-.035,1.47),'chest')
    bone('neck',(0,-.035,1.47),(0,-.065,1.575),'upper_chest')
    bone('head',(0,-.065,1.575),(0,-.065,1.77),'neck')
    body=[o for o in sources if any(s in o.name for s in ['Corpo_uniforme','M41_parte_superior','Calca_perneiras'])]
    allbody=np.array([tuple(v.co) for o in body for v in o.data.vertices])
    for side,sign in [('L',1),('R',-1)]:
        h=HANDS[str(idx)][side];wrist=np.array(h['wrist'])
        shoulder=np.array([sign*.205,-.025,1.415])
        select=allbody[(allbody[:,0]*sign>.275)&(allbody[:,2]>1.245)&(allbody[:,2]<1.285)]
        elbow=np.unique(np.round(select,5),axis=0).mean(axis=0)
        # Cross-section center follows each soldier's original arm pose.
        elbow[2]=1.265
        kneepts=allbody[(allbody[:,0]*sign>.045)&(allbody[:,2]>.505)&(allbody[:,2]<.565)]
        knee=np.unique(np.round(kneepts,5),axis=0).mean(axis=0);knee[2]=.535
        anklepts=allbody[(allbody[:,0]*sign>.045)&(allbody[:,2]>.105)&(allbody[:,2]<.155)]
        ankle=np.unique(np.round(anklepts,5),axis=0).mean(axis=0);ankle[2]=.12
        hip=np.array([sign*.105,-.025,.925]);toe=np.array([ankle[0],-.135,.045])
        palm=np.array(h['points'])[np.where((np.array(h['distances'])>.065)&(np.array(h['distances'])<.105))[0]].mean(axis=0)
        bone(f'clavicle.{side}',(sign*.025,-.025,1.435),shoulder,'upper_chest')
        bone(f'upper_arm.{side}',shoulder,elbow,f'clavicle.{side}')
        bone(f'forearm.{side}',elbow,wrist,f'upper_arm.{side}')
        bone(f'hand.{side}',wrist,palm,f'forearm.{side}')
        specs,hw=hand_landmarks(h,side)
        for name,a,b in specs:
            n=int(name.split('_')[1].split('.')[0]);parent=f'hand.{side}' if n==1 else name.replace(f'_{n:02}.',f'_{n-1:02}.')
            bone(name,a,b,parent)
        handweights[side]={tuple(round(float(x),5) for x in p):w for p,w in zip(h['points'],hw)}
        bone(f'thigh.{side}',hip,knee,'hips');bone(f'shin.{side}',knee,ankle,f'thigh.{side}')
        bone(f'foot.{side}',ankle,toe,f'shin.{side}');bone(f'toe.{side}',toe,(ankle[0],-.195,.04),f'foot.{side}')
        for label,pt,end in [('mao',wrist,palm),('pe',ankle,toe)]:bone(f'CTRL_{label}.{side}',pt,end,'CTRL_root',False)
        for label,a,b,c in [('cotovelo',shoulder,elbow,wrist),('joelho',hip,knee,ankle)]:
            ac=c-a;proj=a+ac*np.dot(b-a,ac)/np.dot(ac,ac);v=b-proj;v=v/np.linalg.norm(v)
            pole=b+v*.35;bone(f'POLE_{label}.{side}',pole,pole+np.array([0,0,.07]),'CTRL_root',False)
        landmarks[side]=(shoulder,elbow,wrist,hip,knee,ankle)
    bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);bpy.context.view_layer.objects.active=arm
    bpy.ops.object.mode_set(mode='EDIT')
    for name,(a,b) in bones.items():
        eb=ardata.edit_bones.new(name);eb.head=a;eb.tail=b;eb.use_deform=deform[name]
        if parents[name]:eb.parent=ardata.edit_bones[parents[name]]
        if name.startswith(('upper_arm','forearm','thigh','shin')):
            eb.align_roll(Vector((0,1,0)))
    bpy.ops.object.mode_set(mode='OBJECT')
    cols={n:ardata.collections.new(n) for n in ['Corpo FK','Dedos','Controles IK','Raiz']}
    for b in ardata.bones:
        group='Raiz' if b.name=='CTRL_root' else 'Controles IK' if b.name.startswith(('CTRL_','POLE_')) else 'Dedos' if b.name.startswith(('thumb','index','middle','ring','pinky')) else 'Corpo FK'
        cols[group].assign(b);b.color.palette='THEME04' if group=='Controles IK' else 'THEME03' if b.name.endswith('.L') else 'THEME01' if b.name.endswith('.R') else 'THEME09'
        arm.pose.bones[b.name].rotation_mode='XYZ'
    cols['Dedos'].is_visible=False
    # FK is the stable default. Optional IK controls share the exact rest pose.
    ikerrors={}
    for side in ['L','R']:
        for label,chain,target,pole,terminal in [('Braco','forearm','mao','cotovelo','hand'),('Perna','shin','pe','joelho','foot')]:
            prop=f'IK_{label}_{side}';arm[prop]=0.0;arm.id_properties_ui(prop).update(min=0,max=1,description='0 = FK (rotacionar ossos); 1 = IK (mover alvo e polo)')
            pb=arm.pose.bones[f'{chain}.{side}'];con=pb.constraints.new('IK');con.name=f'IK_{label}';con.target=arm;con.subtarget=f'CTRL_{target}.{side}';con.pole_target=arm;con.pole_subtarget=f'POLE_{pole}.{side}';con.chain_count=2;con.use_stretch=False;con.iterations=64
            # Calibrate the pole angle against the original knee/elbow position.
            desired=bones[f'{chain}.{side}'][0];best=(1e9,0)
            for angle in np.linspace(-math.pi,math.pi,25):
                con.pole_angle=float(angle);bpy.context.view_layer.update();err=(pb.head-Vector(desired)).length
                if err<best[0]:best=(err,float(angle))
            for angle in np.linspace(best[1]-.14,best[1]+.14,29):
                con.pole_angle=float(angle);bpy.context.view_layer.update();err=(pb.head-Vector(desired)).length
                if err<best[0]:best=(err,float(angle))
            con.pole_angle=best[1];ikerrors[prop]=best[0]
            fc=con.driver_add('influence');dr=fc.driver;v=dr.variables.new();v.name='ik';v.targets[0].id=arm;v.targets[0].data_path=f'["{prop}"]';dr.expression='ik'
            rot=arm.pose.bones[f'{terminal}.{side}'].constraints.new('COPY_ROTATION');rot.name=f'Orientacao_{target}_IK';rot.target=arm;rot.subtarget=f'CTRL_{target}.{side}';rot.target_space='POSE';rot.owner_space='POSE'
            fc=rot.driver_add('influence');dr=fc.driver;v=dr.variables.new();v.name='ik';v.targets[0].id=arm;v.targets[0].data_path=f'["{prop}"]';dr.expression='ik'
    bpy.context.view_layer.update()
    def torso(p):
        z=p[2];centers=[.99,1.14,1.28,1.42];names=['hips','spine','chest','upper_chest']
        if z<=centers[0]:w={'hips':1}
        elif z>=centers[-1]:w={'upper_chest':1}
        else:
            j=next(j for j in range(3) if centers[j]<=z<=centers[j+1]);t=smooth(centers[j],centers[j+1],z);w={names[j]:1-t,names[j+1]:t}
        # Shoulder seam: same continuous field on jacket and sleeve.
        side='L' if p[0]>0 else 'R';t=smooth(.155,.275,abs(p[0]))*smooth(1.27,1.36,z)
        return mix(w,{f'upper_arm.{side}':1},t)
    def armweight(p,side):
        shoulder,elbow,wrist,*_=landmarks[side];d=(wrist-shoulder);d/=np.linalg.norm(d)
        t=smooth(-.055,.055,np.dot(p-elbow,d));w={f'upper_arm.{side}':1-t,f'forearm.{side}':t}
        st=smooth(.15,.275,abs(p[0]));return mix(torso(p),norm(w),st)
    def legweight(p,side):
        *_,hip,knee,ankle=landmarks[side];z=p[2]
        if z<.16:
            t=smooth(.07,.17,z);return norm({f'foot.{side}':1-t,f'shin.{side}':t})
        k=smooth(knee[2]-.065,knee[2]+.065,z);w={f'shin.{side}':1-k,f'thigh.{side}':k}
        return mix(norm(w),{'hips':1},smooth(.81,.98,z))
    copies=[];sourcehash={};meshweights={}
    for src in sources:
        ob=src.copy();ob.data=src.data.copy();ob.name=f'FEB_Soldado_{idx:02}__'+src.name[len(prefix):];collection.objects.link(ob)
        ob['fonte_original']=src.name;ob.parent=arm;ob.matrix_parent_inverse=arm.matrix_world.inverted();ob.matrix_world=src.matrix_world.copy()
        W=[None]*len(ob.data.vertices);name=src.name;bodytype=src in body
        if bodytype:
            handids=set(vi for pi in SKIN['hands_selection'].get(name,[]) for vi in src.data.polygons[pi].vertices)
            for component in COMP.get(name,[]):
                lo,hi=np.array(component['b']);uv0,uv1=np.array(component['uv']);mid=(lo+hi)/2
                vids=set(vi for pi in component['p'] for vi in src.data.polygons[pi].vertices)
                ispants=hi[2]<1.0 and uv1[0]<.312 and uv1[1]<.27
                isboot=hi[2]<.12;isgaiter=hi[2]<.35 and uv0[0]>.28 and uv1[1]>.6
                issleeve=(min(abs(lo[0]),abs(hi[0]))>.17 and hi[2]>1.02 and hi[2]<1.47 and (hi[0]-lo[0]<.40))
                isbackpack=lo[1]>.045 and hi[2]>1.28 and uv0[0]>.24 and uv1[0]<.44
                isbeltgear=lo[1]>.015 and hi[2]<1.02 and not ispants and not isgaiter and not isboot
                for vi in vids:
                    p=np.array(src.data.vertices[vi].co);side='L' if p[0]>0 else 'R'
                    if vi in handids:
                        key=tuple(round(float(x),5) for x in p);W[vi]=handweights[side][key]
                    elif isboot:W[vi]={f'foot.{side}':1}
                    elif ispants or isgaiter:W[vi]=legweight(p,side)
                    elif issleeve:W[vi]=armweight(p,side)
                    elif isbackpack:W[vi]={'chest':1}
                    elif isbeltgear:W[vi]={'hips':1}
                    else:W[vi]=torso(p)
            for vi,w in enumerate(W):
                if w is None:W[vi]=torso(np.array(src.data.vertices[vi].co))
        elif 'Emblema' in name:
            for v in src.data.vertices:W[v.index]=armweight(np.array(v.co),'L' if v.co.x>0 else 'R')
        else:
            lo=np.min([v.co[:] for v in src.data.vertices],axis=0);hi=np.max([v.co[:] for v in src.data.vertices],axis=0)
            face=any('Pele_Rosto' in m.name for m in src.data.materials if m)
            for v in src.data.vertices:
                p=np.array(v.co)
                if face:W[v.index]=mix({'neck':1},{'head':1},smooth(1.48,1.59,p[2]))
                elif lo[2]>1.55:W[v.index]={'head':1}
                elif 'Radio' in ' '.join(m.name for m in src.data.materials if m):W[v.index]={'chest':1}
                elif hi[2]<1.12:W[v.index]={'hips':1}
                else:W[v.index]=torso(p)
        copies.append(ob);meshweights[ob.name]=[norm(w) for w in W]
    # Average coincident garment vertices across UV seams and split meshes.
    pool={}
    for ob in copies:
        if any(x in ob.name for x in ['Corpo_uniforme','M41_parte_superior','Calca_perneiras']):
            for v in ob.data.vertices:pool.setdefault(tuple(round(float(x),5) for x in v.co),[]).append((ob.name,v.index))
    for vals in pool.values():
        if len(vals)<2:continue
        w={}
        for name,vi in vals:
            for b,value in meshweights[name][vi].items():w[b]=w.get(b,0)+value
        w=norm(w)
        for name,vi in vals:meshweights[name][vi]=w
    # Transfer sleeve weights to insignia triangles using the nearest garment face.
    surf=[];faces=[];ref=[]
    for ob in copies:
        if any(x in ob.name for x in ['Corpo_uniforme','M41_parte_superior']):
            offset=len(surf);surf.extend([v.co.copy() for v in ob.data.vertices]);ref.extend([(ob.name,v.index) for v in ob.data.vertices])
            faces.extend([tuple(offset+i for i in p.vertices) for p in ob.data.polygons if all(abs(ob.data.vertices[i].co.x)>.19 for i in p.vertices) and sum(ob.data.vertices[i].co.z for i in p.vertices)/len(p.vertices)>1.15])
    bvh=BVHTree.FromPolygons(surf,faces,all_triangles=True)
    for ob in copies:
        if 'Emblema' not in ob.name:continue
        for v in ob.data.vertices:
            near,normal,fi,dist=bvh.find_nearest(v.co)
            if fi is None:continue
            ids=faces[fi];a,b,c=[surf[j] for j in ids];bary=barycentric_transform(near,a,b,c,Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)));w={}
            for j,t in zip(ids,bary):
                n,vi=ref[j]
                for bn,value in meshweights[n][vi].items():w[bn]=w.get(bn,0)+value*max(0,t)
            meshweights[ob.name][v.index]=norm(w)
    for ob in copies:
        for g in list(ob.vertex_groups):ob.vertex_groups.remove(g)
        groups={n:ob.vertex_groups.new(name=n) for n in bones if deform[n]}
        for vi,w in enumerate(meshweights[ob.name]):
            for n,v in w.items():groups[n].add([vi],v,'REPLACE')
        mod=ob.modifiers.new('FEB_Deformacao','ARMATURE');mod.object=arm;mod.use_deform_preserve_volume=False
    bpy.context.view_layer.update()
    max_rest_error=0
    dg=bpy.context.evaluated_depsgraph_get()
    for ob in copies:
        ev=ob.evaluated_get(dg);me=ev.to_mesh()
        max_rest_error=max(max_rest_error,max((v.co-me.vertices[v.index].co).length for v in ob.data.vertices));ev.to_mesh_clear()
    report.append(dict(soldier=idx,armature=arm.name,bones=len(bones),deform_bones=sum(deform.values()),meshes=len(copies),vertices=sum(len(o.data.vertices) for o in copies),rest_error=max_rest_error,ik_rest_joint_error=ikerrors,unweighted=sum(not w for ob in copies for w in meshweights[ob.name]),max_influences=max(len(w) for ob in copies for w in meshweights[ob.name])))
    print('RIG',idx,json.dumps(report[-1]))

# Save an independent editable rig file; original static file remains available.
scene.frame_start=1;scene.frame_end=60;scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
arm=bpy.data.objects['FEB_Soldado_01_Rig'];arm.select_set(True);bpy.context.view_layer.objects.active=arm
cam.location=(6,-12,5);target=Vector((0,-.05,.90));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=9.4
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=8.5;area.spaces.active.region_3d.view_location=(0,0,.9)
            area.spaces.active.shading.type='MATERIAL'
(OUT/'rig_build_report.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FEB_Soldiers_Rig.blend'))
scene.render.filepath=str(OUT/'FEB_Soldiers_Rig_Repouso.png');bpy.ops.render.render(write_still=True)
result={'rigs':report}
