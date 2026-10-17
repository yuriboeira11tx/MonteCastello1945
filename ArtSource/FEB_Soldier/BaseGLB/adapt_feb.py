"""Run through Blender Lab MCP bridge. Preserve the supplied skinned body."""
import bpy, bmesh, math, json, hashlib
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from math import sin, cos, pi

ROOT=Path('E:/Unity/MonteCastello1945')
OUT=ROOT/'ArtSource/FEB_Soldier/BaseGLB'
PATCH=ROOT/'Assets/Art/Characters/FEB_Soldier/Textures'
scene=bpy.data.scenes['FEB_BaseGLB'];bpy.context.window.scene=scene
char=bpy.data.collections['FEB_Personagem_BaseGLB']
rig=bpy.data.objects['FEB_Rig_BaseGLB']

# Meaningful bone names preserve the existing vertex weights and hierarchy.
names={'GLTF_created_0_rootJoint':'Root','Bone.040_54':'Hips','Bone_45':'Spine','Bone.001_44':'Chest','Bone.002_43':'UpperChest','Bone.003_4':'Neck','Bone.004_3':'Head','Bone.005_23':'Shoulder.L','Bone.006_22':'UpperArm.L','Bone.007_21':'Forearm.L','Bone.008_17':'Hand.L','Bone.024_42':'Shoulder.R','Bone.025_41':'UpperArm.R','Bone.026_40':'Forearm.R','Bone.027_36':'Hand.R','Bone.044_49':'Thigh.L','Bone.045_48':'Shin.L','Bone.046_46':'Foot.L','Bone.047_47':'FootHelper.L','Bone.048_53':'Thigh.R','Bone.049_52':'Shin.R','Bone.051_51':'Foot.R','Bone.050_50':'FootHelper.R'}
fingers={'L':{'Thumb':['009_20','022_19','023_18'],'Index':['019_16','020_15','021_14'],'Middle':['013_13','017_12','018_11'],'Ring':['014_10','015_9','016_8'],'Little':['010_7','011_6','012_5']},'R':{'Thumb':['028_39','032_38','036_37'],'Index':['041_35','042_34','043_33'],'Middle':['037_32','038_31','039_30'],'Ring':['033_29','034_28','035_27'],'Little':['029_26','030_25','031_24']}}
for side,fs in fingers.items():
    for finger,ids in fs.items():
        for i,b in enumerate(ids):names['Bone.'+b]=f'{finger}{i+1}.{side}'
for old,new in names.items():
    for ob in char.objects:
        if ob.type=='MESH' and old in ob.vertex_groups:ob.vertex_groups[old].name=new
    if old in rig.data.bones:rig.data.bones[old].name=new

# Remove only the imported equipment being replaced; retain the supplied body.
for name in ['FEB_Equipamento_original','FEB_Capacete_original']:
    ob=bpy.data.objects.get(name)
    if ob:bpy.data.objects.remove(ob,do_unlink=True)

def linear(c): return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
def color(rgb):return tuple(linear(c/255) for c in rgb)+(1,)

def fabric(name,rgb,rough=.82,source=None):
    m=bpy.data.materials.new(name);m.use_nodes=True
    n=m.node_tree.nodes;l=m.node_tree.links;bs=n.get('Principled BSDF')
    bs.inputs['Base Color'].default_value=color(rgb);bs.inputs['Roughness'].default_value=rough
    bs.inputs['Specular IOR Level'].default_value=.24
    coords=n.new('ShaderNodeTexCoord')
    noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=155;noise.inputs['Detail'].default_value=2
    l.new(coords.outputs['Object'],noise.inputs['Vector'])
    bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.12;bump.inputs['Distance'].default_value=.00032
    l.new(noise.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs['Normal'],bs.inputs['Normal'])
    ramp=n.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position=.04
    ramp.color_ramp.elements[0].color=tuple(x*.42 for x in color(rgb)[:3])+(1,)
    ramp.color_ramp.elements[1].position=.65 if source else .8
    ramp.color_ramp.elements[1].color=tuple(min(1,x*1.42) for x in color(rgb)[:3])+(1,)
    if source:
        tex=n.new('ShaderNodeTexImage');tex.image=source;tex.interpolation='Linear'
        uv=n.new('ShaderNodeUVMap');uv.uv_map='UVMap';l.new(uv.outputs[0],tex.inputs[0])
        gray=n.new('ShaderNodeRGBToBW');l.new(tex.outputs['Color'],gray.inputs[0]);l.new(gray.outputs[0],ramp.inputs[0])
    else:l.new(noise.outputs['Fac'],ramp.inputs[0])
    l.new(ramp.outputs[0],bs.inputs['Base Color'])
    return m

olive=(128,125,91);trouser=(86,91,65)
shirt=fabric('FEB_Sarja_oliva',olive)
canvas=fabric('FEB_Lona_cinturao',(149,141,104))
dark=fabric('FEB_Metal_escurecido',(39,42,33),.49);dark.node_tree.nodes['Principled BSDF'].inputs['Metallic'].default_value=.65
helmetmat=fabric('FEB_Pintura_M1',(74,81,52),.76);helmetmat.node_tree.nodes['Principled BSDF'].inputs['Metallic'].default_value=.18
leather=fabric('FEB_Couro_preto',(32,29,25),.59)
stitch=fabric('FEB_Costura',(163,153,118),.9)

for label,rgb in [('Mangas',olive),('Detalhes_mangas',olive),('Gandola',olive),('Quadril',trouser),('Calca',trouser),('Botas',(46,41,33)),('Gola_interna',(103,108,75))]:
    ob=bpy.data.objects['FEB_'+label]
    old=ob.data.materials[0]
    im=next(n.image for n in old.node_tree.nodes if n.type=='TEX_IMAGE')
    mat=fabric('FEB_'+label+'_textura',rgb,.65 if label=='Botas' else .86,im)
    ob.data.materials.clear();ob.data.materials.append(mat)
    # Remove the original collar insignia by using plain matching cloth there.
    if label=='Gandola':
        ob.data.materials.append(shirt)
        for p in ob.data.polygons:
            if p.center.z>1.425:p.material_index=1
        # Slightly shorten the long original coat, retaining its modelled folds.
        for v in ob.data.vertices:
            if v.co.z<.98:v.co.z+=.065*max(0,min(1,(.98-v.co.z)/.205))
        ob.data.update()

# Retain the source face/hand UVs and portrait character; restrain gloss.
for label in ['Rosto','Maos','Olhos']:
    ob=bpy.data.objects['FEB_'+label]
    for mat in ob.data.materials:
        n=mat.node_tree.nodes;l=mat.node_tree.links;bs=n.get('Principled BSDF')
        tex=next(n for n in n if n.type=='TEX_IMAGE');uv=n.new('ShaderNodeUVMap');uv.uv_map='UVMap';l.new(uv.outputs[0],tex.inputs[0])
        bs.inputs['Roughness'].default_value=.48 if label=='Olhos' else .72
        if label!='Olhos':
            hue=n.new('ShaderNodeHueSaturation');hue.inputs['Saturation'].default_value=1.06;hue.inputs['Value'].default_value=.93
            l.new(tex.outputs['Color'],hue.inputs['Color']);l.new(hue.outputs['Color'],bs.inputs['Base Color'])
        mat.name='FEB_Pele_'+label

def torso_weights(p):
    # Existing spine spans the waist, and UpperChest controls the shoulders.
    t=max(0,min(1,(p.z-1.08)/.30))
    return [('Spine',1-t),('UpperChest',t)]

def bind(ob,bone='Spine'):
    for c in list(ob.users_collection):c.objects.unlink(ob)
    char.objects.link(ob)
    if callable(bone):
        groups={}
        for v in ob.data.vertices:
            for nm,w in bone(ob.matrix_world@v.co):
                if w>0:
                    if nm not in groups:groups[nm]=ob.vertex_groups.new(name=nm)
                    groups[nm].add([v.index],w,'REPLACE')
    else:ob.vertex_groups.new(name=bone).add(list(range(len(ob.data.vertices))),1,'REPLACE')
    ob.parent=rig
    mod=ob.modifiers.new('FEB_Rig','ARMATURE');mod.object=rig
    return ob

def mesh(name,verts,faces,mat,bone='Spine',uvs=None,smooth=False):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);me.materials.append(mat)
    if uvs:
        layer=me.uv_layers.new(name='UVMap')
        for p in me.polygons:
            for li in p.loop_indices:layer.data[li].uv=uvs[me.loops[li].vertex_index]
    for p in me.polygons:p.use_smooth=smooth
    bind(ob,bone)
    return ob

def activate(ob):
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob

def box(name,loc,dim,mat,bone='Spine',bevel=.002,rot=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);ob=bpy.context.object;ob.name=name;ob.dimensions=dim;ob.rotation_euler.z=rot
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        m=ob.modifiers.new('Cantos','BEVEL');m.width=bevel;m.segments=1;bpy.ops.object.modifier_apply(modifier=m.name)
        m=ob.modifiers.new('Normais','WEIGHTED_NORMAL');bpy.ops.object.modifier_apply(modifier=m.name)
    ob.data.materials.append(mat);bind(ob,bone);return ob

def sphere(name,loc,dim,mat,bone='Spine',seg=16,rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,location=loc);ob=bpy.context.object;ob.name=name;ob.scale=dim
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for p in ob.data.polygons:p.use_smooth=True
    ob.data.materials.append(mat);bind(ob,bone);return ob

def strip(name,points,width,mat,bone=torso_weights):
    vs=[]
    for p in points:vs.extend([Vector(p)+Vector((-width/2,0,0)),Vector(p)+Vector((width/2,0,0))])
    fs=[(i*2,i*2+1,i*2+3,i*2+2) for i in range(len(points)-1)]
    ob=mesh(name,vs,fs,mat,bone,[(i%2,i//2/max(1,len(points)-1)) for i in range(len(vs))])
    activate(ob);m=ob.modifiers.new('Espessura','SOLIDIFY');m.thickness=.002;m.offset=0;bpy.ops.object.modifier_apply(modifier=m.name)
    return ob

def cord(name,points,radius,mat,bone='Spine',closed=False):
    cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.resolution_u=1
    sp=cu.splines.new('POLY');sp.points.add(len(points)-1)
    for p,co in zip(sp.points,points):p.co=(*co,1)
    sp.use_cyclic_u=closed;cu.bevel_depth=radius;cu.bevel_resolution=0
    ob=bpy.data.objects.new(name,cu);scene.collection.objects.link(ob);activate(ob);bpy.ops.object.convert(target='MESH');ob=bpy.context.object
    ob.data.materials.append(mat);bind(ob,bone);return ob

def ring(name,zs,rxs,rys,cx,cy,mat,bone,segments=20):
    vs=[];uv=[];fs=[]
    for j,(z,rx,ry) in enumerate(zip(zs,rxs,rys)):
        for k in range(segments+1):
            t=k*2*pi/segments;vs.append((cx+rx*cos(t),cy+ry*sin(t),z));uv.append((k/segments,j/max(1,len(zs)-1)))
    for j in range(len(zs)-1):
        for k in range(segments):
            a=j*(segments+1)+k;b=a+segments+1;fs.append((a,a+1,b+1,b))
    return mesh(name,vs,fs,mat,bone,uv,True)

# M1 helmet shell: broad low brim, rounded dome, separate rolled edge.
vs=[];uv=[];fs=[];segments=32
for j,(rx,ry,z,fade) in enumerate([(.122,.145,1.668,1),(.118,.139,1.700,.5),(.107,.124,1.751,.1),(.083,.094,1.787,0),(.045,.05,1.806,0),(.002,.002,1.813,0)]):
    for k in range(segments+1):
        a=2*pi*k/segments;vs.append((rx*cos(a),.010+ry*sin(a),z+fade*(-.028*sin(a)+.003*cos(2*a))));uv.append((k/segments,j/5))
for j in range(5):
    for k in range(segments):
        a=j*(segments+1)+k;b=a+segments+1;fs.append((a,a+1,b+1,b))
helmet=mesh('FEB_Capacete_M1',vs,fs,helmetmat,'Head',uv,True)
activate(helmet);sol=helmet.modifiers.new('Chapa','SOLIDIFY');sol.thickness=.0025;bpy.ops.object.modifier_apply(modifier=sol.name)
cord('FEB_Debrum_M1',vs[:segments],.0025,dark,'Head',True)
j=bpy.data.objects['FEB_Jugular_original'];j.name='FEB_Jugular_lona';j.data.materials.clear();j.data.materials.append(canvas)

# Replace the bulky original chest ammunition carriers with FEB web equipment.
ring('FEB_Cinturao',[1.015,1.071],[.203,.200],[.154,.151],0,.015,canvas,'Spine',32)
box('FEB_Fecho_cinturao',(0,-.151,1.043),(.036,.018,.036),dark)
for side,sign in [('L',1),('R',-1)]:
    for idx,t in enumerate([.23,.58,.93,1.28,1.63]):
        a=sign*t;normal=Vector((sin(a),-cos(a),0));p=Vector((.210*sin(a),.015-.163*cos(a),1.040))
        box(f'FEB_Porta_municao_{idx+1}.{side}',p,(.059,.045,.094),canvas,bevel=.006,rot=a)
        flap=p+normal*.024+Vector((0,0,.024))
        box(f'FEB_Aba_municao_{idx+1}.{side}',flap,(.057,.006,.038),canvas,bevel=.002,rot=a)
        sphere(f'FEB_Pressao_{idx+1}.{side}',flap+normal*.005-Vector((0,0,.007)),(.0034,.0034,.0034),dark,seg=8,rings=4)
    # Follow the broad low-poly torso, with continuous strips over the shoulder.
    pts=[(sign*.115,-.173,1.085),(sign*.116,-.167,1.17),(sign*.122,-.157,1.28),(sign*.136,-.108,1.42),(sign*.139,-.048,1.487),(sign*.139,.031,1.492),(sign*.132,.135,1.425),(sign*.10,.183,1.30),(-sign*.078,.185,1.09)]
    strip('FEB_Suspensorio.'+side,pts,.029,canvas)
    for z in [1.145,1.172]:box('FEB_Regulador.'+side,(sign*.115,-.177,z),(.034,.006,.007),dark,bevel=.001)
    for x,y in [(sign*.115,-.174),(-sign*.078,.185)]:
        cord('FEB_Gancho.'+side,[(x-.007,y,1.10),(x-.007,y,1.071),(x+.007,y,1.071),(x+.007,y,1.10)],.002,dark,closed=True)
    # Gaiters cover the upper section of the original boots.
    cx=sign*.116
    gaiter=ring('FEB_Perneira.'+side,[.115,.18,.26,.287],[.062,.063,.069,.075],[.083,.078,.079,.084],cx,.024,canvas,'Shin.'+side,20)
    for z,rx,ry in [(.118,.063,.084),(.282,.075,.084)]:
        cord('FEB_Debrum_perneira.'+side,[(cx+rx*cos(i*2*pi/20),.024+ry*sin(i*2*pi/20),z) for i in range(20)],.0016,stitch,'Shin.'+side,True)
    for i in range(6):
        z=.139+i*.023;x=cx-sign*(.063+.03*(z-.139))
        cord('FEB_Cadarco_perneira.'+side,[(x,-.008,z),(x,.023,z+.022),(x,-.008,z+.044)],.0013,stitch,'Shin.'+side)

# Rear canteen and first aid pouch.
sphere('FEB_Porta_cantil',(.17,.185,.96),(.064,.044,.08),canvas,seg=16,rings=8)
box('FEB_Tampa_cantil',(.17,.185,1.039),(.025,.025,.023),dark,bevel=.003)
strip('FEB_Aba_cantil',[(.17,.19,1.029),(.17,.229,.99),(.17,.230,.955)],.039,canvas,'Spine')
sphere('FEB_Pressao_cantil',(.17,.232,.965),(.004,.003,.004),dark,seg=8,rings=4)
box('FEB_Porta_curativo',(-.15,.183,.984),(.095,.039,.078),canvas,bevel=.005)
box('FEB_Aba_curativo',(-.15,.207,1.004),(.091,.006,.031),canvas,bevel=.002)

# Actual source emblem pixels remain unchanged; only shaped support geometry is new.
sleeve=bpy.data.objects['FEB_Mangas']
bvh=BVHTree.FromPolygons([v.co.copy() for v in sleeve.data.vertices],[tuple(p.vertices) for p in sleeve.data.polygons],all_triangles=False)
def patch(side,filename,name):
    sign=1 if side=='L' else -1
    m=bpy.data.materials.new(name+'_Original_PDF');m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.86
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(PATCH/filename),check_existing=True)
    m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    outline=[(.21,.997),(.79,.997),(.997,.82),(.997,.18),(.79,.003),(.21,.003),(.003,.18),(.003,.82)]
    coords=[(.5,.5)]+outline
    verts=[]
    for u,v in coords:
        x=sign*(.265+(1-v)*.093);y=.019+(u-.5)*.073
        hit=bvh.ray_cast(Vector((x,y,1.70)),Vector((0,0,-1)),.45)[0]
        verts.append((x,y,(hit.z if hit else 1.505)+.002))
    faces=[(0,1+i,1+(i+1)%8) for i in range(8)]
    ob=mesh(name,verts,faces,m,'UpperArm.'+side,coords)
    bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=3,use_grid_fill=True);bm.to_mesh(ob.data);bm.free()
    for v in ob.data.vertices:
        hit=bvh.ray_cast(Vector((v.co.x,v.co.y,1.7)),Vector((0,0,-1)),.45)[0]
        if hit:v.co.z=hit.z+.002
    bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
    activate(ob);sol=ob.modifiers.new('Espessura_bordado','SOLIDIFY');sol.thickness=.0008;bpy.ops.object.modifier_apply(modifier=sol.name)
    return ob
patch('L','FEB_Patch.png','FEB_Emblema_esquerdo')
patch('R','FifthArmy_Patch.png','FEB_5Exercito_direito')

# Fix normals only on new surfaces; imported source topology and UVs are retained.
for ob in char.objects:
    if ob.type=='MESH' and not ob.get('source_object'):
        bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
for im in bpy.data.images:
    if im.type=='IMAGE' and im.has_data:im.pack()

scene['base_model']='Call Of Duty 1 Soldier rig - _NETHER_FIRE_ (user-provided GLB)'
scene['source_license_metadata']='CC-BY-4.0, as declared in the provided file'
scene['adaptation']='FEB uniform, original PDF insignia, M1 helmet and web equipment; T-pose; body and finger rig retained'
scene.render.resolution_x=1200;scene.render.resolution_y=1200;scene.cycles.samples=40
scene.render.film_transparent=False
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.045,.055,.065,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
cam=scene.camera;cam.data.ortho_scale=2.09;target=Vector((0,0,.9));cam.location=(0,-4,1.04);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(OUT/'FEB_frente.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FEB_Soldado_BaseGLB.blend'))
bpy.ops.render.render(write_still=True)
result={'file':bpy.data.filepath,'meshes':len([o for o in char.objects if o.type=='MESH']),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in char.objects if o.type=='MESH'),'bones':len(rig.data.bones),'emblem_sha256':{f:hashlib.sha256((PATCH/f).read_bytes()).hexdigest() for f in ['FEB_Patch.png','FifthArmy_Patch.png']}}
(OUT/'adaptation_report.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
