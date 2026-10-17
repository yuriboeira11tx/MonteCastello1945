"""FEB soldier, T-pose. Blender 5.1 authoring source; metres, front = -Y."""
import bpy, math, json
from mathutils import Vector, Quaternion
from pathlib import Path
from math import sin, cos, pi

SRC=Path(__file__).resolve().parent
ROOT=SRC.parents[1]
ASSET=ROOT/'Assets/Art/Characters/FEB_Soldier'
TEX=ASSET/'Textures'
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.name='FEB • Soldado em T'
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
char=bpy.data.collections.new('FEB_Soldier'); scene.collection.children.link(char)
studio=bpy.data.collections.new('Apresentacao'); scene.collection.children.link(studio)
pieces=[]

def move_collection(obj,col):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    col.objects.link(obj)

def image_node(mat, filename, color=True):
    n=mat.node_tree.nodes.new('ShaderNodeTexImage'); n.image=bpy.data.images.load(str(TEX/filename),check_existing=True)
    if not color:n.image.colorspace_settings.name='Non-Color'
    return n

atlas=bpy.data.materials.new('FEB | tecido, couro, aco e pele'); atlas.use_nodes=True
bs=atlas.node_tree.nodes.get('Principled BSDF'); links=atlas.node_tree.links
for filename,socket,color in [('FEB_BaseColor.png','Base Color',True),('FEB_Roughness.png','Roughness',False),('FEB_Metallic.png','Metallic',False)]:
    node=image_node(atlas,filename,color); links.new(node.outputs['Color'],bs.inputs[socket])
node=image_node(atlas,'FEB_Normal.png',False); nm=atlas.node_tree.nodes.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value=.18
links.new(node.outputs['Color'],nm.inputs['Color']); links.new(nm.outputs['Normal'],bs.inputs['Normal'])

def weights(obj,bone):
    if callable(bone):
        groups={}
        for v in obj.data.vertices:
            for name,w in bone(obj.matrix_world@v.co):
                if w>.0001:
                    if name not in groups: groups[name]=obj.vertex_groups.new(name=name)
                    groups[name].add([v.index],w,'REPLACE')
    else:
        g=obj.vertex_groups.new(name=bone); g.add(list(range(len(obj.data.vertices))),1,'REPLACE')

def finish(obj,tile,bone='Chest',smooth=True):
    move_collection(obj,char); pieces.append(obj)
    obj.data.materials.append(atlas)
    if not obj.data.uv_layers:
        bpy.context.view_layer.objects.active=obj; obj.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(island_margin=.02); bpy.ops.object.mode_set(mode='OBJECT'); obj.select_set(False)
    col=tile%4; row=tile//4
    for uv in obj.data.uv_layers.active.data:
        uv.uv.x=(col+.015+uv.uv.x*.97)/4
        uv.uv.y=(3-row+.015+uv.uv.y*.97)/4
    for p in obj.data.polygons:p.use_smooth=smooth
    weights(obj,bone)
    return obj

def mesh_obj(name,verts,faces,tile,bone='Chest',uvs=None):
    me=bpy.data.meshes.new(name); me.from_pydata(verts,[],faces); me.update()
    obj=bpy.data.objects.new(name,me); scene.collection.objects.link(obj)
    if uvs:
        layer=me.uv_layers.new(name='UVMap')
        for poly in me.polygons:
            for li in poly.loop_indices: layer.data[li].uv=uvs[me.loops[li].vertex_index]
    return finish(obj,tile,bone)

def loft(name,rings,tile,bone='Chest',axis='Z',segments=24,wrinkle=0):
    # Each ring: centre XYZ, radius on local first axis, radius on second axis.
    verts=[]; uvs=[]; faces=[]
    for j,(c,a,b) in enumerate(rings):
        for k in range(segments+1):
            t=2*pi*k/segments
            ripple=1+wrinkle*sin(5*t+j*1.9)*sin(pi*j/(len(rings)-1))
            off=Vector((a*cos(t)*ripple,b*sin(t)*ripple,0)) if axis=='Z' else Vector((0,a*cos(t)*ripple,b*sin(t)*ripple))
            verts.append(Vector(c)+off); uvs.append((k/segments,j/(len(rings)-1)))
    for j in range(len(rings)-1):
        for k in range(segments):
            a=j*(segments+1)+k; b=a+segments+1; faces.append((a,a+1,b+1,b))
    faces.append(tuple(range(segments-1,-1,-1)))
    start=(len(rings)-1)*(segments+1); faces.append(tuple(start+k for k in range(segments)))
    obj=mesh_obj(name,verts,faces,tile,bone,uvs)
    # Normals for mirrored limbs are recalculated together before export.
    return obj

def box(name,loc,scale,tile,bone='Chest',bevel=.008,rot=None):
    bpy.ops.object.select_all(action='DESELECT'); bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=bpy.context.object; o.name=name; o.dimensions=scale
    if rot:o.rotation_euler=rot
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Bordas costuradas','BEVEL');mod.width=bevel;mod.segments=3
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=o.modifiers.new('Normais','WEIGHTED_NORMAL');mod.keep_sharp=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    finish(o,tile,bone,False);o.select_set(False);return o

def ellipsoid(name,loc,scale,tile,bone='Head',segments=24,rings=16):
    bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=loc)
    o=bpy.context.object;o.name=name;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    finish(o,tile,bone);o.select_set(False);return o

def cord(name,points,radius,tile,bone='Chest',closed=False):
    cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.resolution_u=2
    sp=cu.splines.new('POLY');sp.points.add(len(points)-1)
    for p,co in zip(sp.points,points):p.co=(*co,1)
    sp.use_cyclic_u=closed;cu.bevel_depth=radius;cu.bevel_resolution=1
    o=bpy.data.objects.new(name,cu);scene.collection.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.convert(target='MESH');finish(o,tile,bone);o.select_set(False);return o

def ribbon(name,points,width,tile,bone='Chest',width_axis=(1,0,0)):
    v=[]; axis=Vector(width_axis)*width/2
    for p in points:v.extend([Vector(p)-axis,Vector(p)+axis])
    f=[(2*i,2*i+1,2*i+3,2*i+2) for i in range(len(points)-1)]
    o=mesh_obj(name,v,f,tile,bone,[(k%2,k//2/max(1,len(points)-1)) for k in range(len(v))])
    bpy.context.view_layer.objects.active=o
    so=o.modifiers.new('Espessura da lona','SOLIDIFY');so.thickness=.003
    bpy.ops.object.modifier_apply(modifier=so.name)
    be=o.modifiers.new('Bordas suaves','BEVEL');be.width=.001;be.segments=2;bpy.ops.object.modifier_apply(modifier=be.name)
    return o

def blend_joint(p,joint,bone_a,bone_b,axis='x',spread=.06):
    t=max(0,min(1,(abs(getattr(p,axis))-joint+spread)/(2*spread)))
    return [(bone_a,1-t),(bone_b,t)]
def arm_weights(side):
    return lambda p:blend_joint(p,.49,'UpperArm.'+side,'Forearm.'+side)
def leg_weights(side):
    return lambda p:blend_joint(p,.53,'Shin.'+side,'Thigh.'+side,axis='z',spread=.08)

# Jacket and trousers; garment folds built into the cross sections.
torso=[(1.00,.171,.107),(1.025,.179,.111),(1.07,.177,.112),(1.12,.165,.106),(1.18,.163,.108),(1.25,.179,.118),(1.34,.194,.122),(1.42,.213,.114),(1.48,.211,.101),(1.515,.163,.087),(1.55,.075,.062)]
loft('Gandola_M41', [((0,0,z),a,b) for z,a,b in torso],0,lambda p:[('Hips',max(0,min(1,(1.25-p.z)/.16))),('Chest',max(0,min(1,(p.z-1.09)/.16)))],wrinkle=.025)
loft('Calca_M43_quadril',[((0,0,z),a,b) for z,a,b in [(.855,.145,.085),(.91,.161,.105),(.99,.164,.108),(1.045,.158,.102)]],0,'Hips',wrinkle=.015)
for sign,side in [(1,'L'),(-1,'R')]:
    arm=[(.165,.089,.083),(.21,.091,.087),(.28,.086,.082),(.36,.079,.075),(.43,.073,.069),(.475,.077,.071),(.50,.067,.063),(.53,.070,.065),(.58,.067,.059),(.64,.057,.052),(.70,.047,.044),(.735,.042,.040)]
    loft('Manga_M41.'+side,[((sign*x,0,1.465),a,b) for x,a,b in arm],0,arm_weights(side),axis='X',wrinkle=.035)
    loft('Punho.'+side,[((sign*x,0,1.465),a,b) for x,a,b in [(.70,.049,.046),(.706,.049,.046),(.738,.043,.041),(.742,.042,.040)]],0,'Forearm.'+side,axis='X')
    ellipsoid('Botao_punho.'+side,(sign*.724,-.047,1.464),(.007,.003,.007),5,'Forearm.'+side,12,8)
    # Articulated trousers, tucked into the gaiters.
    leg=[(.37,.051,.052),(.405,.064,.064),(.43,.070,.070),(.46,.077,.078),(.50,.073,.078),(.535,.080,.080),(.57,.078,.085),(.64,.086,.091),(.74,.098,.096),(.83,.103,.103),(.91,.099,.103),(.97,.086,.096)]
    loft('Perna_M43.'+side,[((sign*(.092 if z>.7 else .105),.006,z),a,b) for z,a,b in leg],0,leg_weights(side),wrinkle=.04)
    # Long seams and restrained folds.
    cord('Costura_lateral_calca.'+side,[(sign*(.105+a*.99),.012,z) for z,a,b in leg],.0009,14,leg_weights(side))
    for z in [.445,.49,.565]:
        cord('Dobra_joelho.'+side,[(sign*.105+u*.064,-.068+.009*abs(u),z+.009*sin(u*2)) for u in [-1,-.6,0,.6,1]],.0015,14,leg_weights(side))
    # Boots: anatomical foot, broad toe, sole tread, leather uppers.
    footx=sign*.105
    box('Sola_coturno.'+side,(footx,-.058,.025),(.112,.269,.04),7,'Foot.'+side,.018)
    box('Salto.'+side,(footx,.025,.019),(.102,.094,.036),7,'Foot.'+side,.009)
    ellipsoid('Biqueira.'+side,(footx,-.108,.069),(.057,.104,.047),2,'Foot.'+side)
    ellipsoid('Peito_coturno.'+side,(footx,-.025,.104),(.052,.085,.072),2,'Foot.'+side)
    loft('Cano_coturno.'+side,[((footx,.018,z),a,b) for z,a,b in [(.09,.048,.055),(.14,.044,.05),(.19,.042,.046),(.255,.047,.05)]],2,'Shin.'+side)
    for j in range(7):
        box('Trava_sola.'+side,(footx,-.17+j*.033,.012),(.108,.015,.014),7,'Foot.'+side,.002)
    cord('Costura_biqueira.'+side,[(footx+.052*cos(t),-.105+.082*sin(t),.079) for t in [pi+i*pi/16 for i in range(17)]],.0011,8,'Foot.'+side)
    for j in range(4):
        z=.125+j*.015
        cord('Cadarco_coturno.'+side,[(footx-.021,-.047,z),(footx+.021,-.047,z+.012)],.0017,12,'Shin.'+side)
    # Canvas gaiters with inner eyelets and crossed laces.
    loft('Perneira.'+side,[((footx,.012,z),a,b) for z,a,b in [(.16,.049,.054),(.18,.050,.056),(.24,.052,.057),(.31,.060,.061),(.375,.066,.067),(.397,.066,.066)]],1,'Shin.'+side)
    for z,a,b in [(.17,.05,.055),(.39,.067,.067)]:
        cord('Debrum_perneira.'+side,[(footx+a*cos(t),.012+b*sin(t),z) for t in [i*2*pi/32 for i in range(32)]],.002,8,'Shin.'+side,True)
    for j in range(9):
        z=.187+j*.022; rad=.05+(z-.18)*.075; xx=footx-sign*rad
        for yy in [-.008,.021]:ellipsoid('Ilhos_perneira.'+side,(xx,yy,z),(.0025,.004,.004),5,'Shin.'+side,8,6)
        if j<8:
            cord('Laco_perneira.'+side,[(xx-sign*.003,-.008,z),(xx-sign*.004,.021,z+.022)],.0015,8,'Shin.'+side)
            cord('Laco_perneira.'+side,[(xx-sign*.003,.021,z),(xx-sign*.004,-.008,z+.022)],.0015,8,'Shin.'+side)
    # Palm and five individually modelled fingers, all horizontal for the T-pose.
    loft('Mao.'+side,[((sign*x,0,1.465),a,b) for x,a,b in [(.733,.031,.027),(.77,.037,.027),(.80,.041,.027),(.835,.037,.023)]],4,'Hand.'+side,axis='X',segments=16)
    for j,(yy,length) in enumerate([(-.027,.073),(-.009,.082),(.010,.077),(.028,.06)]):
        rr=.0095 if j<3 else .008
        loft('Dedo_'+str(j)+'.'+side,[((sign*x,yy,1.465-dz),r,r*.85) for x,r,dz in [(.824,rr,0),(.849,rr,0),(.86+length*.25,rr*.9,.002),(.832+length,rr*.65,.004),(.838+length,.002,.004)]],4,'Hand.'+side,axis='X',segments=10)
        box('Unha_'+str(j)+'.'+side,(sign*(.825+length),yy,1.468),(.014,.009,.001),4,'Hand.'+side,.002)
    thumb=cord('Polegar.'+side,[(sign*.777,-.027,1.46),(sign*.791,-.055,1.46),(sign*.823,-.067,1.46)],.013,4,'Hand.'+side)
    ellipsoid('Ponta_polegar.'+side,(sign*.825,-.067,1.46),(.014,.012,.011),4,'Hand.'+side,16,10)

# Shirt, open collar, button placket and waist adjustment tabs.
loft('Camiseta_gola',[((0,0,z),a,b) for z,a,b in [(1.49,.074,.063),(1.545,.065,.058),(1.56,.057,.051)]],0,'Chest')
for sign in [1,-1]:
    ribbon('Gola_M41',[(sign*.046,-.041,1.56),(sign*.085,-.095,1.515),(sign*.052,-.123,1.465)],.050,0,width_axis=(1,.2,.15))
    cord('Costura_gola',[(sign*.06,-.063,1.552),(sign*.104,-.1,1.512),(sign*.069,-.125,1.47)],.001,8)
    box('Ajuste_cintura',(sign*.166,-.052,1.055),(.034,.008,.022),0,'Hips',.003)
    ellipsoid('Botao_ajuste',(sign*.174,-.058,1.056),(.005,.002,.005),5,'Hips',12,8)
    cord('Bolso_inferior',[(sign*.079,-.113,1.115),(sign*.133,-.109,1.155)],.003,14,'Hips')
ribbon('Vista_botoes',[(0,-.111,1.015),(0,-.112,1.12),(0,-.12,1.25),(0,-.126,1.39),(0,-.118,1.49)],.020,0)
for z in [1.05,1.13,1.22,1.31,1.40,1.475]:ellipsoid('Botao_gandola',(0,-(.129 if z>1.2 else .118),z),(.005,.0025,.005),5,'Chest',12,8)

# Belt with ten ammunition pouches, five each side around the waist.
loft('Cinto_interno',[((0,0,z),.173,.115) for z in [1.12,1.155]],3,'Hips')
box('Fivela_latao',(0,-.121,1.139),(.047,.012,.031),6,'Hips',.004)
loft('Cinturao_NA',[((0,0,z),.188,.133) for z in [1.091,1.15]],1,'Hips')
box('Fecho_cinturao',(0,-.145,1.12),(.037,.017,.044),5,'Hips',.004)
for sign,side in [(1,'L'),(-1,'R')]:
    for j,t in enumerate([.27,.67,1.07,1.47,1.87]):
        theta=sign*t; loc=Vector((.199*sin(theta),-.149*cos(theta),1.12)); ang=theta
        # Local -Y points away from the torso.
        box(f'Bolsa_municao_{j+1}.{side}',loc,(.059,.049,.10),1,'Hips',.009,(0,0,ang))
        outward=Vector((sin(theta),-cos(theta),0))
        flap=loc+outward*.026+Vector((0,0,.024))
        box(f'Tampa_municao_{j+1}.{side}',flap,(.057,.008,.043),1,'Hips',.004,(0,0,ang))
        ellipsoid('Botao_pressao',flap+outward*.006-Vector((0,0,.007)),(.005,.005,.005),5,'Hips',10,6)
    # Front straps routed over shoulders and crossed at the rear.
    pts=[(sign*.115,-.153,1.17),(sign*.119,-.132,1.29),(sign*.129,-.119,1.43),(sign*.13,-.084,1.515),(sign*.13,0,1.54),(sign*.125,.087,1.49),(sign*.07,.125,1.31),(-sign*.08,.123,1.16)]
    ribbon('Suspensorio_NA.'+side,pts,.036,1)
    for offset in [-.014,.014]: cord('Costura_suspensorio',[(x+offset,y-.001,z) for x,y,z in pts],.0007,8)
    for z in [1.225,1.255]:box('Regulador_suspensorio',(sign*.119,-.143,z),(.041,.009,.009),5,'Chest',.002)
    ribbon('Tira_lateral.'+side,[(sign*.13,.025,1.51),(sign*.19,.075,1.30),(sign*.18,.088,1.16)],.022,1)
    for x,y in [(sign*.115,-.153),(sign*.18,.088),(-sign*.08,.123)]:
        cord('Mosquetao',[(x-.008,y,1.18),(x-.008,y,1.15),(x+.008,y,1.15),(x+.008,y,1.18)],.0025,5,'Hips',True)

# Canteen and first aid bag at the rear hips.
ellipsoid('Cantil_aluminio',(.195,.124,1.022),(.065,.043,.089),15,'Hips')
ellipsoid('Porta_cantil_lona',(.195,.124,1.022),(.073,.053,.086),1,'Hips')
box('Tampa_cantil',(.195,.127,1.113),(.028,.026,.023),15,'Hips',.007)
for xx in [.16,.23]:
    ribbon('Aba_porta_cantil',[(xx,.143,1.077),(xx,.18,1.04),(xx,.178,1.015)],.034,1,'Hips')
    ellipsoid('Pressao_porta_cantil',(xx,.182,1.023),(.005,.003,.005),5,'Hips',12,8)
cord('Corrente_cantil',[(.209,.135,1.113),(.222,.14,1.10),(.225,.143,1.081)],.0014,15,'Hips')
box('Porta_curativo',(-.15,.135,1.045),(.104,.045,.088),1,'Hips',.012)
box('Aba_porta_curativo',(-.15,.162,1.064),(.10,.008,.04),1,'Hips',.008)
ellipsoid('Pressao_curativo',(-.15,.170,1.052),(.005,.003,.005),5,'Hips',12,8)

# Head and facial features: a neutral adult face, no portrait reference supplied.
loft('Pescoco',[((0,.004,z),a,b) for z,a,b in [(1.52,.047,.044),(1.575,.045,.042),(1.615,.047,.045)]],4,'Neck')
headrings=[(1.588,.027,.031,-.025),(1.603,.047,.047,-.017),(1.625,.060,.064,-.004),(1.65,.069,.073,.003),(1.68,.073,.081,.005),(1.71,.072,.081,.006),(1.735,.071,.081,.007),(1.765,.065,.075,.009),(1.79,.048,.059,.009),(1.808,.021,.028,.009),(1.812,.003,.005,.009)]
head=loft('Rosto',[((0,y,z),a,b) for z,a,b,y in headrings],4,'Head',segments=40)
# The face is slightly flatter than the rear cranium.
for v in head.data.vertices:
    if v.co.y<-.04 and 1.63<v.co.z<1.75:
        v.co.y=max(v.co.y,-.074)+.004*math.exp(-((abs(v.co.x)-.031)/.015)**2)*math.exp(-((v.co.z-1.706)/.018)**2)
for sign,side in [(1,'L'),(-1,'R')]:
    ellipsoid('Orelha.'+side,(sign*.073,.005,1.678),(.016,.018,.032),4,'Head',20,12)
    ellipsoid('Concha_orelha.'+side,(sign*.084,-.005,1.68),(.003,.009,.018),9,'Head',16,10)
    ellipsoid('Olho.'+side,(sign*.03,-.069,1.708),(.014,.009,.007),10,'Head',24,12)
    ellipsoid('Iris.'+side,(sign*.03,-.0778,1.708),(.0045,.0012,.0045),11,'Head',20,12)
    ellipsoid('Pupila.'+side,(sign*.03,-.079,1.708),(.002,.0007,.0025),12,'Head',16,8)
    cord('Palpebra_superior.'+side,[(sign*.03+.014*cos(t),-.078+abs(cos(t))*.004,1.708+.006*sin(t)) for t in [i*pi/12 for i in range(13)]],.0022,4,'Head')
    cord('Palpebra_inferior.'+side,[(sign*.03+.014*cos(t),-.077+abs(cos(t))*.004,1.708-.005*sin(t)) for t in [i*pi/12 for i in range(13)]],.0016,4,'Head')
    cord('Sobrancelha.'+side,[(sign*.016,-.077,1.724),(sign*.029,-.080,1.728),(sign*.045,-.074,1.724)],.003,12,'Head')
    ellipsoid('Asa_nariz.'+side,(sign*.011,-.085,1.676),(.009,.010,.007),4,'Head',20,12)
    ellipsoid('Narina.'+side,(sign*.009,-.089,1.671),(.0035,.003,.0018),9,'Head',12,8)
loft('Nariz',[((0,y,z),a,b) for z,a,b,y in [(1.672,.01,.008,-.086),(1.681,.012,.015,-.087),(1.693,.008,.013,-.081),(1.717,.006,.006,-.073),(1.724,.004,.003,-.072)]],4,'Head',segments=20)
cord('Labio_superior',[(-.021,-.068,1.647),(-.01,-.078,1.649),(0,-.078,1.647),(.01,-.078,1.649),(.021,-.068,1.647)],.0028,9,'Head')
cord('Labio_inferior',[(-.018,-.07,1.644),(0,-.079,1.641),(.018,-.07,1.644)],.003,9,'Head')
cord('Linha_boca',[(-.018,-.071,1.646),(0,-.081,1.645),(.018,-.071,1.646)],.0008,12,'Head')

# M1 shell: domed steel, rolled rim, separate inner liner and chinstrap.
helmet=[]
for i in range(13):
    t=(pi/2)*i/12
    z=1.738+.111*sin(t)
    helmet.append(((0,.011,z),max(.002,.107*cos(t)),max(.002,.128*cos(t))))
shell=loft('Capacete_M1_aco',helmet,3,'Head',segments=48)
cord('Aro_capacete',[(.108*cos(t),.011+.13*sin(t),1.738-.007*cos(2*t)) for t in [i*2*pi/64 for i in range(64)]],.003,5,'Head',True)
loft('Forro_capacete',[((0,.011,z),a,b) for z,a,b in [(1.731,.101,.121),(1.739,.103,.123)]],13,'Head',segments=40)
for sign in [1,-1]:
    ribbon('Jugular_M1',[(sign*.098,.005,1.738),(sign*.080,-.029,1.661),(sign*.044,-.053,1.609),(sign*.012,-.059,1.598)],.014,1,'Head')
    box('Fivela_jugular',(sign*.063,-.045,1.638),(.017,.006,.014),5,'Head',.002)

# Source insignia are mapped to shaped cloth patches, excluding white image corners.
def patch(name,side,filename):
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.91
    tex=image_node(mat,filename);mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    # In T-pose the upper-arm outside faces upward; top of badge points to shoulder.
    outline=[(.20,1),(.80,1),(1,.82),(1,.18),(.80,0),(.20,0),(0,.18),(0,.82)]
    sign=1 if side=='L' else -1
    verts=[(sign*(.29+(1-v)*.088), (u-.5)*.065, 1.549-.004*(2*u-1)**2) for u,v in outline]
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],[tuple(range(8))]);me.update()
    ob=bpy.data.objects.new(name,me);char.objects.link(ob);pieces.append(ob);me.materials.append(mat)
    uv=me.uv_layers.new(name='UVMap')
    for poly in me.polygons:
        for li in poly.loop_indices:uv.data[li].uv=outline[me.loops[li].vertex_index]
    weights(ob,'UpperArm.'+side)
    so=ob.modifiers.new('Bordado','SOLIDIFY');so.thickness=.001
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=so.name)
    cord('Debrum_'+name,verts,.001,0,'UpperArm.'+side,True)
patch('Distintivo_FEB_esquerdo','L','FEB_Patch.png')
patch('Distintivo_5Exercito_direito','R','FifthArmy_Patch.png')

# Fuse overlapping construction volumes into continuous garment and hand surfaces.
def fuse(name,objects,tile,weight_fn,voxel=.004):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:
        ob.select_set(True);pieces.remove(ob)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();ob=bpy.context.object;ob.name=name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    # Weld UV seam vertices before voxelizing closed surfaces.
    import bmesh
    bm=bmesh.new();bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(ob.data);bm.free()
    mod=ob.modifiers.new('Superficie_continua','REMESH');mod.mode='VOXEL';mod.voxel_size=voxel;mod.use_smooth_shade=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=ob.modifiers.new('Suavizar_costuras','SMOOTH');mod.factor=1;mod.iterations=4;bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=ob.modifiers.new('Densidade_jogo','DECIMATE');mod.ratio=.08;bpy.ops.object.modifier_apply(modifier=mod.name)
    ob.vertex_groups.clear();ob.data.materials.clear()
    while ob.data.uv_layers:ob.data.uv_layers.remove(ob.data.uv_layers[0])
    # Stable cylindrical garment UVs avoid hundreds of tiny projected islands.
    layer=ob.data.uv_layers.new(name='UVMap')
    for poly in ob.data.polygons:
        uv=[]
        for vi in poly.vertices:
            p=ob.data.vertices[vi].co
            if name.startswith('Gandola') and abs(p.x)>.205:
                u=math.atan2(p.z-1.465,p.y)/(2*pi)+.5;v=(abs(p.x)-.16)/.59
            elif name.startswith('Calca'):
                cx=(.1 if p.x>0 else -.1) if p.z<.90 else 0
                u=math.atan2(p.y-.006,p.x-cx)/(2*pi)+.5;v=(p.z-.36)/.70
            else:
                u=math.atan2(p.y,p.x)/(2*pi)+.5;v=(p.z-1.0)/.56 if name.startswith('Gandola') else (p.z%1)
            uv.append([u,max(0,min(1,v))])
        if max(t[0] for t in uv)-min(t[0] for t in uv)>.5:
            for t in uv:
                if t[0]<.5:t[0]=1
        for li,co in zip(poly.loop_indices,uv):layer.data[li].uv=co
    finish(ob,tile,weight_fn);ob.select_set(False)

def jacket_weights(p):
    side='L' if p.x>0 else 'R'
    if abs(p.x)>.21:return arm_weights(side)(p)
    if abs(p.x)>.145:
        t=(abs(p.x)-.145)/.065;return [('Chest',1-t),('UpperArm.'+side,t)]
    t=max(0,min(1,(p.z-1.09)/.16));return [('Hips',1-t),('Chest',t)]
fuse('Gandola_M41_continua',[o for o in pieces if o.name=='Gandola_M41' or o.name.startswith('Manga_M41')],0,jacket_weights)
def trouser_weights(p):
    if p.z>.88:return [('Hips',1)]
    return leg_weights('L' if p.x>0 else 'R')(p)
fuse('Calca_M43_continua',[o for o in pieces if o.name=='Calca_M43_quadril' or o.name.startswith('Perna_M43')],0,trouser_weights)
for side in ['L','R']:
    fuse('Couro_coturno.'+side,[o for o in pieces if o.name in ['Biqueira.'+side,'Peito_coturno.'+side,'Cano_coturno.'+side]],2,'Foot.'+side,.003)
    fuse('Mao_anatomica.'+side,[o for o in pieces if o.name.endswith('.'+side) and any(o.name.startswith(s) for s in ['Mao.','Dedo_','Polegar.','Ponta_polegar.'])],4,'Hand.'+side,.002)

# Consistent outward normals for all mesh pieces.
import bmesh
for ob in pieces:
    bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(ob.data);bm.free()

# Basic humanoid rig. Segmented weights are supplied; animation polish remains an art pass.
arm=bpy.data.armatures.new('FEB_Humanoid');rig=bpy.data.objects.new('FEB_Rig',arm);char.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
def bone(name,head,tail,parent=None):
    b=arm.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=arm.edit_bones[parent]
    return b
bone('Root',(0,0,0),(0,0,.15))
bone('Hips',(0,0,.91),(0,0,1.08),'Root')
bone('Spine',(0,0,1.08),(0,0,1.29),'Hips')
bone('Chest',(0,0,1.29),(0,0,1.49),'Spine')
bone('Neck',(0,0,1.49),(0,0,1.605),'Chest')
bone('Head',(0,0,1.605),(0,0,1.80),'Neck')
for sign,side in [(1,'L'),(-1,'R')]:
    bone('Shoulder.'+side,(sign*.045,0,1.475),(sign*.20,0,1.465),'Chest')
    bone('UpperArm.'+side,(sign*.20,0,1.465),(sign*.49,0,1.465),'Shoulder.'+side)
    bone('Forearm.'+side,(sign*.49,0,1.465),(sign*.735,0,1.465),'UpperArm.'+side)
    bone('Hand.'+side,(sign*.735,0,1.465),(sign*.87,0,1.465),'Forearm.'+side)
    bone('Thigh.'+side,(sign*.092,0,.93),(sign*.105,0,.53),'Hips')
    bone('Shin.'+side,(sign*.105,0,.53),(sign*.105,.012,.14),'Thigh.'+side)
    bone('Foot.'+side,(sign*.105,.012,.14),(sign*.105,-.14,.065),'Shin.'+side)
    bone('Toes.'+side,(sign*.105,-.14,.065),(sign*.105,-.19,.065),'Foot.'+side)
bpy.ops.object.mode_set(mode='OBJECT');rig.select_set(False);rig.show_in_front=True
# Join by material, keeping the atlas and original patch images in three draw groups.
groups={}
for ob in pieces:groups.setdefault(ob.data.materials[0].name,[]).append(ob)
joined=[]
for i,objects in enumerate(groups.values()):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();ob=bpy.context.object
    ob.name=['FEB_Uniforme_Corpo_Equipamento','FEB_Distintivo_Esquerdo','FEB_Distintivo_Direito'][i]
    # Bake all transforms to ensure skeletal deformation uses consistent model space.
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    mod=ob.modifiers.new('Humanoid','ARMATURE');mod.object=rig;ob.parent=rig;joined.append(ob)
rig['Referencia']='feb-textura.pdf: uniforme historico do Grupo Montese; nao reconstrucao independente de 1944.'
rig['Escala']='Metros; altura aproximada 1.85m com capacete'
rig['Rig']='22 bones; T-pose. Rig basico, dedos sem articulacao individual.'

# Export only the character, without the presentation lighting or cameras.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for ob in joined:ob.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(ASSET/'Models/FEB_Soldier_TPose.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,mesh_smooth_type='FACE',path_mode='RELATIVE')
bpy.ops.export_scene.gltf(filepath=str(SRC/'FEB_Soldier_TPose.glb'),export_format='GLB',use_selection=True,export_animations=False)

# Studio for honest inspection and reusable renders.
def plain(name,col,rough=.7):
    m=bpy.data.materials.new(name);m.diffuse_color=(*col,1);m.use_nodes=True
    b=m.node_tree.nodes.get('Principled BSDF');b.inputs['Base Color'].default_value=(*col,1);b.inputs['Roughness'].default_value=rough;return m
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.006));floor=bpy.context.object;floor.name='Chao_estudio';move_collection(floor,studio);floor.data.materials.append(plain('Estudio carvão',(.025,.035,.032)))
def light(name,loc,power,color,size):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=size
    o=bpy.data.objects.new(name,data);studio.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
light('Key suave',(-3,-4,5),450,(1,.86,.69),4)
light('Fill frio',(3,-2,3),260,(.71,.83,1),3)
light('Rim',(1,3,4),600,(1,.9,.70),3)
scene.world=bpy.data.worlds.new('Estudio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.16,.18,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.35
camdata=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',camdata);studio.objects.link(cam);scene.camera=cam
camdata.type='ORTHO';camdata.ortho_scale=2.65
def aim(loc,target=(0,0,.94)):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
aim((3,-7,2.9))
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1600;scene.render.resolution_y=1600;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
# Images are embedded in the blend as well as delivered externally for Unity.
for im in bpy.data.images:
    if im.source=='FILE':im.pack()
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=3.1
            area.spaces.active.region_3d.view_location=(0,0,.95)
            area.spaces.active.region_3d.view_rotation=Quaternion((1,0,0),pi/2)
            area.spaces.active.shading.type='MATERIAL'
studio.hide_viewport=True
bpy.ops.object.select_all(action='DESELECT');joined[0].select_set(True);bpy.context.view_layer.objects.active=joined[0]
stats={'vertices':sum(len(o.data.vertices) for o in joined),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in joined),'bones':len(arm.bones),'mesh_objects':len(joined),'materials':len(groups),'height_metres':1.851,'texture_size':'2048 x 2048'}
(SRC/'model_report.json').write_text(json.dumps(stats,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'FEB_Soldier_TPose.blend'))
studio.hide_viewport=False
for name,loc in [('three_quarter',(3,-7,2.9)),('front',(0,-7,1.15)),('back',(0,7,1.7))]:
    aim(loc);scene.render.filepath=str(SRC/'Previews'/f'{name}.png');bpy.ops.render.render(write_still=True)
print('FEB_COMPLETE',json.dumps(stats))
