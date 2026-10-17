"""Itapage: editable photographic reconstruction. Run through Blender MCP."""
import bpy, math, random, json, os
from mathutils import Vector
from pathlib import Path
random.seed(1927)
OUT=Path('E:/Unity/MonteCastello1945/ArtSource/Itapage')
OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes.new('ITAPAGE | Reconstrucao 1927')
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
root=bpy.data.collections.new('ITAPAGE | Navio')
scene.collection.children.link(root)
COL={}
for name in ['01 Casco','02 Conveses','03 Superestrutura','04 Botes e turcos','05 Mastros e cabos','06 Equipamentos','07 Identificacao','08 Apresentacao']:
    c=bpy.data.collections.new(name);root.children.link(c);COL[name[:2]]=c
M=[]
def mat(name,color,rough=.6,metal=0,noise=.08,scale=4):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    n=m.node_tree.nodes;l=m.node_tree.links
    p=next(n for n in n if n.type=='BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    if noise:
        tex=n.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=scale;tex.inputs['Detail'].default_value=3
        coord=n.new('ShaderNodeTexCoord');l.new(coord.outputs['Object'],tex.inputs['Vector'])
        ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.2;ramp.color_ramp.elements[1].position=.8
        ramp.color_ramp.elements[0].color=(*(c*(1-noise) for c in color),1)
        ramp.color_ramp.elements[1].color=(*(min(1,c*(1+noise)) for c in color),1)
        l.new(tex.outputs['Fac'],ramp.inputs[0]);l.new(ramp.outputs[0],p.inputs['Base Color'])
        fine=n.new('ShaderNodeTexNoise');fine.inputs['Scale'].default_value=90;l.new(coord.outputs['Object'],fine.inputs['Vector'])
        bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.16;bump.inputs['Distance'].default_value=.014
        l.new(fine.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs[0],p.inputs['Normal'])
    M.append(m);return len(M)-1
HULL=mat('01 | Aco naval - azul preto gasto',(.027,.037,.043),.48,.55,.35,1.5)
RED=mat('02 | Antifouling - oxido vermelho',(.21,.045,.027),.78,.12,.3,1.2)
BOOT=mat('03 | Linha dagua - betume',(.013,.018,.02),.52,.25,.2,2)
WHITE=mat('04 | Marfim naval - pintura envelhecida',(.71,.69,.60),.62,.18,.16,3)
TRIM=mat('05 | Caixilhos marfim',(.83,.8,.68),.47,.25,.08,12)
DARK=mat('06 | Ferro pintado e ferragens',(.065,.073,.072),.48,.7,.16,8)
WOOD=mat('07 | Teca - tabuas e corrimaos',(.27,.15,.067),.64,0,.42,2)
WOOD2=mat('08 | Teca clara',(.36,.23,.105),.68,0,.3,2)
GLASS=mat('09 | Vidro escuro salino',(.028,.075,.09),.23,.48,.12,12)
BRASS=mat('10 | Bronze patinado',(.27,.19,.08),.39,.78,.3,6)
ROPE=mat('11 | Cabos e cordas',(.2,.17,.12),.85,0,.2,12)
RUST=mat('12 | Oxidacao discreta',(.14,.068,.033),.92,.1,.5,7)
BLACK=mat('13 | Interior sombreado',(.008,.012,.014),.9,0,0)
CANVAS=mat('14 | Lona dos botes',(.43,.40,.31),.88,0,.18,5)
RING=mat('15 | Boias - vermelho queimado',(.4,.047,.023),.72,0,.15,12)
GREEN=mat('16 | Bandeira - verde',(.025,.19,.07),.8,0,.1,8)
YELLOW=mat('17 | Bandeira - ouro',(.82,.55,.035),.8,0,.1,8)
BLUE=mat('18 | Bandeira - azul',(.018,.046,.15),.8,0,0)

class Build:
    def __init__(self):self.v=[];self.f=[];self.mi=[];self.sm=[]
    def part(self,v,f,ma=WHITE,smooth=False):
        k=len(self.v);self.v.extend(v);self.f.extend([tuple(k+i for i in face) for face in f]);self.mi.extend([ma]*len(f));self.sm.extend([smooth]*len(f))
    def box(self,c,d,ma=WHITE):
        x,y,z=c;a,b,h=(q/2 for q in d)
        v=[(x+sx*a,y+sy*b,z+sz*h) for sx,sy,sz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
        self.part(v,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],ma)
    def rod(self,a,b,r,ma=DARK,r2=None,n=10):
        a=Vector(a);b=Vector(b);axis=(b-a).normalized();up=Vector((0,0,1))
        if abs(axis.dot(up))>.98:up=Vector((1,0,0))
        u=axis.cross(up).normalized();v=axis.cross(u).normalized();r2=r if r2 is None else r2
        verts=[tuple(p+rad*(u*math.cos(i*2*math.pi/n)+v*math.sin(i*2*math.pi/n))) for p,rad in [(a,r),(b,r2)] for i in range(n)]
        faces=[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]+[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
        self.part(verts,faces,ma,True)
    def line(self,pts,r=.025,ma=DARK,n=6):
        for a,b in zip(pts,pts[1:]):self.rod(a,b,r,ma,n=n)
    def ring(self,c,r,t,ma=BRASS,axis=(0,1,0),n=32,m=8):
        c=Vector(c);ax=Vector(axis).normalized();up=Vector((0,0,1))
        if abs(ax.dot(up))>.98:up=Vector((1,0,0))
        u=ax.cross(up).normalized();v=ax.cross(u).normalized();verts=[]
        for i in range(n):
            direction=u*math.cos(i*math.tau/n)+v*math.sin(i*math.tau/n)
            for j in range(m):verts.append(tuple(c+direction*(r+t*math.cos(j*math.tau/m))+ax*t*math.sin(j*math.tau/m)))
        faces=[(i*m+j,((i+1)%n)*m+j,((i+1)%n)*m+(j+1)%m,i*m+(j+1)%m) for i in range(n) for j in range(m)]
        self.part(verts,faces,ma,True)
    def object(self,name,col,bevel=0):
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(self.v,[],self.f);mesh.update();ob=bpy.data.objects.new(name,mesh);COL[col].objects.link(ob)
        used=sorted(set(self.mi));mapping={i:j for j,i in enumerate(used)}
        for i in used:mesh.materials.append(M[i])
        for p,ma,sm in zip(mesh.polygons,self.mi,self.sm):p.material_index=mapping[ma];p.use_smooth=sm
        uv=mesh.uv_layers.new(name='UV_Metros')
        for p in mesh.polygons:
            normal=p.normal;drop=max(range(3),key=lambda i:abs(normal[i]));axes=[i for i in range(3) if i!=drop]
            for li in p.loop_indices:
                co=mesh.vertices[mesh.loops[li].vertex_index].co;uv.data[li].uv=(co[axes[0]]/4,co[axes[1]]/4)
        if bevel:
            mod=ob.modifiers.new('Arestas de fabricacao','BEVEL');mod.width=bevel;mod.segments=2
        return ob

def interp(x,keys):
    for (a,u),(b,v) in zip(keys,keys[1:]):
        if a<=x<=b:
            t=(x-a)/(b-a);t=t*t*(3-2*t);return u+(v-u)*t
    return keys[0][1] if x<keys[0][0] else keys[-1][1]
def width(x):return interp(x,[(-59.85,.04),(-58,2.4),(-54,4.5),(-46,6.4),(-30,7.7),(-12,7.94),(16,7.94),(30,7.2),(42,5.7),(52,3.3),(59.85,.025)])
def sheer(x):return 4.35+2.35*max(0,x/59.85)**3+.7*max(0,-x/59.85)**5
levels=[(-6.3,.055),(-5.95,.32),(-5.1,.66),(-3.8,.9),(-2.3,.98),(-.7,1),(0,1),(1.3,1),(2.6,1),(3.7,1),(None,1)]
def hp(x,j,s):
    z,w=levels[j];z=sheer(x) if z is None else z
    shrink=max(0,-z/6.3)
    shift=(-6.4*max(0,x/59.85)**6+5.0*max(0,-x/59.85)**6)*shrink
    return (x+shift,s*width(x)*w,z)
xs=[-59.85+i*119.7/160 for i in range(161)]
h=Build();cross=[(j,-1) for j in range(10,-1,-1)]+[(j,1) for j in range(11)]
verts=[hp(x,j,s) for x in xs for j,s in cross];nc=len(cross)
for i in range(len(xs)-1):
    for j in range(nc-1):
        ids=(i*nc+j,(i+1)*nc+j,(i+1)*nc+j+1,i*nc+j+1)
        z=sum(verts[k][2] for k in ids)/4;ma=RED if z<-.7 else (BOOT if z<.12 else HULL)
        h.part([verts[k] for k in ids],[(0,1,2,3)],ma,True)
h.part([hp(xs[0],j,s) for j,s in cross],[tuple(range(nc-1,-1,-1))],HULL)
h.part([hp(xs[-1],j,s) for j,s in cross],[tuple(range(nc))],HULL)
ob=h.object('Casco | 119.70m - chapas de aco','01')
# Weld coincident surface vertices so the shell is continuous.
import bmesh
bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()

deck=Build()
for a,b in zip(xs,xs[1:]):
    deck.part([(a,-width(a),sheer(a)),(b,-width(b),sheer(b)),(b,width(b),sheer(b)),(a,width(a),sheer(a))],[(0,1,2,3)],WOOD)
deck.object('Conves principal | teca e tosamento','02')
planks=Build()
for y in [i*.24 for i in range(-32,33)]:
    seg=[(x,y,sheer(x)+.014) for x in xs if width(x)>abs(y)+.08]
    if len(seg)>1:planks.line(seg,.006,DARK,4)
for x in range(-58,59,4):
    for j in range(-30,31):
        xx=x+(j%3)*1.32;y=j*.24
        if abs(y)<width(xx)-.3:planks.line([(xx,y,sheer(xx)+.015),(xx,y+.24,sheer(xx)+.015)],.006,DARK,4)
planks.object('Calafetagem | juntas das tabuas','02')
# Metal bulwarks at bow and stern, and a narrow rubbing strake.
b=Build()
for a,c in zip(xs,xs[1:]):
    for s in [-1,1]:
        if a>23 or c<-47:
            b.part([(a,s*width(a),sheer(a)),(c,s*width(c),sheer(c)),(c,s*width(c),sheer(c)+.9),(a,s*width(a),sheer(a)+.9)],[(0,1,2,3)],HULL)
            b.line([(a,s*width(a),sheer(a)+.92),(c,s*width(c),sheer(c)+.92)],.055,TRIM)
        b.line([(a,s*(width(a)+.025),.27),(c,s*(width(c)+.025),.27)],.045,BOOT)
b.object('Amuradas | proa e popa','01')
# Subtle horizontal plate laps and vertical riveted seams.
p=Build()
for s in [-1,1]:
    for z in [1.0,2.2,3.4]:
        p.line([(x,s*(width(x)+.013),z) for x in xs[3:-3]],.012,DARK,5)
    for x in range(-55,57,4):
        y=s*(width(x)+.026);zt=sheer(x)-.12
        p.rod((x,y,.5),(x,y,zt),.008,DARK,n=5)
        for k in range(int((zt-.5)/.25)):
            z=.6+k*.25;p.rod((x+.075,y,z),(x+.075,y+s*.018,z),.025,HULL,n=6)
p.object('Chapas | sobreposicoes e rebites','01')

def rail(b,pts,height=1.0,ma=TRIM,spacing=1.7):
    for a,c in zip(pts,pts[1:]):
        a=Vector(a);c=Vector(c);count=max(1,math.ceil((c-a).length/spacing))
        for i in range(count):
            p=a.lerp(c,i/count);b.rod(p,p+Vector((0,0,height)),.029,ma,n=8)
        for z in [.34,.68,height]:b.rod(a+Vector((0,0,z)),c+Vector((0,0,z)),.023 if z<height else .035,ma,n=8)
    b.rod(pts[-1],Vector(pts[-1])+Vector((0,0,height)),.03,ma,n=8)

def floor(xa,xb,y,z,b,ma=WHITE):b.box(((xa+xb)/2,0,z),(xb-xa,2*y,.18),ma)
floors=Build();walls=Build();posts=Build();window=Build();rails=Build();wood=Build()
for xa,xb,y,z in [(-47,23,7.55,4.65),(-46,23,7.55,7.65),(-44,23,7.35,10.65)]:
    floor(xa,xb,y,z,floors);floors.box(((xa+xb)/2,0,z+.11),(xb-xa-.1,2*y-.12,.035),WOOD2)
    for s in [-1,1]:
        floors.box(((xa+xb)/2,s*y,z-.12),(xb-xa,.16,.33),TRIM)
        for y0 in [k*.25 for k in range(-28,29)]:
            wood.rod((xa+.1,y0,z+.135),(xb-.1,y0,z+.135),.005,DARK,n=4)
# Cabin walls are set back; open promenades are actual passages.
for xa,xb,z in [(-44,20,4.83),(-42,20,7.83)]:
    walls.box(((xa+xb)/2,0,z+1.28),(xb-xa,10.7,2.56),WHITE)
    for s in [-1,1]:
        y=s*5.39
        for i in range(int((xb-xa-2)/2.25)):
            x=xa+1.3+i*2.25
            window.box((x,y,z+1.57),(.94,.07,1.17),GLASS)
            for dx in [-.5,.5]:window.box((x+dx,y+s*.06,z+1.57),(.075,.09,1.33),TRIM)
            for dz in [-.63,.63,0]:window.box((x,y+s*.06,z+1.57+dz),(1.07,.09,.065),TRIM)
        for x in [xa+3,xa+16,xb-5]:
            window.box((x,y+s*.1,z+1.05),(1.0,.1,2.05),WOOD)
            window.box((x,y+s*.16,z+1.52),(.64,.08,.66),GLASS)
            window.rod((x+.35,y+s*.24,z+.95),(x+.35,y+s*.24,z+1.1),.034,BRASS)
for xa,xb,y,z in [(-46,23,7.42,4.78),(-44,23,7.23,7.78)]:
    for s in [-1,1]:
        for i in range(int((xb-xa)/2.4)+1):
            x=xa+i*(xb-xa)/int((xb-xa)/2.4)
            posts.box((x,s*y,z+1.37),(.16,.16,2.74),TRIM)
            posts.box((x,s*y,z+2.65),(.34,.24,.2),TRIM)
        rail(rails,[(xa,s*y,z+.04),(xb,s*y,z+.04)],1.0)
    rail(rails,[(xa,-y,z+.04),(xa,y,z+.04)],1.0)
floors.object('Conveses | tres niveis com passadicos','02',.025)
walls.object('Cabines | paredes recuadas','03',.045)
posts.object('Colunatas | corredores laterais abertos','03',.015)
window.object('Janelas e portas | caixilhos e vidros','03',.012)
rails.object('Guarda-corpos | conveses de passeio','03')
wood.object('Teca | juntas dos conveses elevados','02')
# Raised forward bridge, with wooden wheelhouse and bridge wings.
b=Build();w=Build();r=Build()
b.box((13,0,12.12),(18,12.0,2.76),WHITE)
b.box((13,0,13.58),(19,15.3,.25),TRIM)
b.box((14,0,15.05),(12.0,10.9,2.74),WOOD)
b.box((14,0,16.55),(12.8,11.6,.25),TRIM)
b.box((14,0,16.72),(12.5,11.3,.09),CANVAS)
for s in [-1,1]:
    for x in [6,8.2,10.4,12.6,14.8,17,19.2]:
        w.box((x,s*6.025,12.35),(1.7,.08,1.22),GLASS)
        for dx in [-.87,.87]:w.box((x+dx,s*6.09,12.35),(.075,.1,1.4),TRIM)
        for dz in [-.65,.65]:w.box((x,s*6.09,12.35+dz),(1.8,.1,.075),TRIM)
    for x in [9,11,13,15,17,19]:
        w.box((x,s*5.49,15.25),(1.7,.07,1.32),GLASS)
        for dx in [-.86,.86]:w.box((x+dx,s*5.54,15.25),(.07,.08,1.46),WOOD2)
    rail(r,[(4,-s*7.5,13.72),(22,-s*7.5,13.72),(22,-s*5.8,13.72)],1)
for y in [-4.5,-2.7,-.9,.9,2.7,4.5]:
    w.box((20.04,y,15.25),(.09,1.58,1.32),GLASS)
    for dy in [-.83,.83]:w.box((20.11,y+dy,15.25),(.1,.08,1.45),WOOD2)
    for dz in [-.7,.7]:w.box((20.11,y,15.25+dz),(.1,1.7,.08),WOOD2)
b.object('Ponte | passadico elevado e asas','03',.035);w.object('Ponte | vidros e molduras','03',.012);r.object('Ponte | balaustradas','03')
# Long casing under the funnel and boat deck fittings.
b=Build();b.box((-13,0,11.42),(23,6.3,1.35),WHITE)
b.box((-13,0,12.15),(23.4,6.7,.18),DARK)
for x in range(-23,-2,3):
    for s in [-1,1]:
        b.box((x,s*3.18,11.5),(1.8,.04,.58),BLACK)
        for zz in [11.28,11.43,11.58,11.73]:b.box((x,s*3.24,zz),(1.85,.06,.045),TRIM)
b.object('Casa das maquinas | ventilacao','03',.02)
print('STAGE1',len(scene.objects),'objects',sum(len(o.data.polygons) for o in scene.objects if o.type=='MESH'),'faces')
# Reusable definitions are kept in a namespace for following MCP stages.
bpy.app.driver_namespace['ITAPAGE_BUILD']=globals().copy()
