import bpy, math, random
from mathutils import Vector
globals().update(bpy.app.driver_namespace['ITAPAGE_BUILD'])
# White bridge enclosure with varnished timber window frames.
o=bpy.data.objects.get('Ponte | passadico elevado e asas')
wi=next((i for i,m in enumerate(o.data.materials) if m==M[WHITE]),None)
for p in o.data.polygons:
    if o.data.materials[p.material_index]==M[WOOD]:p.material_index=wi
# Brazilian ensign, modelled cloth with individually assigned color fields.
b=Build();pole=Build();flagx=-58;flagz=sheer(-58)+5
pole.rod((-58,0,sheer(-58)),(-58.5,0,flagz+.2),.06,WOOD2,r2=.035,n=12)
pole.object('Bandeira | pau de bandeira da popa','05')
def flagpos(u,v,offset=0):
    return (flagx-.5-u*3.8,.33*math.sin(u*8-v*2)*u+offset,flagz-v*2.5+.16*math.sin(u*5+v)*u)
nv=30;nu=48;verts=[flagpos(i/nu,j/nv) for i in range(nu+1) for j in range(nv+1)]
for i in range(nu):
    for j in range(nv):
        u=(i+.5)/nu;v=(j+.5)/nv
        ma=GREEN
        if abs(u-.5)/.4+abs(v-.5)/.39<1:ma=YELLOW
        if ((u-.5)/.175)**2+((v-.5)/.26)**2<1:ma=BLUE
        ids=(i*(nv+1)+j,(i+1)*(nv+1)+j,(i+1)*(nv+1)+j+1,i*(nv+1)+j+1)
        b.part([verts[k] for k in ids],[(0,1,2,3)],ma,True)
o=b.object('Pavilhao brasileiro | tecido ondulado','07')
bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bm.to_mesh(o.data);bm.free()
sol=o.modifiers.new('Espessura do tecido','SOLIDIFY');sol.thickness=.004
detail=Build()
for s in [-1,1]:
    pts=[flagpos(.34+i*.32/20,.44+.14*i/20,s*.007) for i in range(21)];detail.line(pts,.023,TRIM,n=6)
    for i in range(21):
        a=i*2.39996;r=.13*math.sqrt((i+.5)/21);u=.5+r*math.cos(a);v=.56+r*1.05*math.sin(a)
        p=flagpos(u,v,s*.009);detail.rod(p,Vector(p)+Vector((0,s*.002,0)),.018,TRIM,n=5)
detail.object('Pavilhao | faixa e estrelas estilizadas','07')
# Small signal pennant on the main mast.
b=Build();b.part([(26.9,0,36.45),(23.8,.25,36.1),(26.9,0,35.65)],[(0,1,2)],BLUE)
b.object('Flamula | mastro de proa','07')

# Procedural teak: fine longitudinal grain and restrained variation.
for idx in [WOOD,WOOD2]:
    m=M[idx];nt=m.node_tree;n=nt.nodes;l=nt.links
    p=next(q for q in n if q.type=='BSDF_PRINCIPLED')
    coord=n.new('ShaderNodeTexCoord');mapping=n.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY';mapping.inputs[1].default_value=(.4,22,6)
    l.new(coord.outputs['Object'],mapping.inputs[0]);noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=2;noise.inputs['Detail'].default_value=2.4;l.new(mapping.outputs[0],noise.inputs['Vector'])
    ramp=n.new('ShaderNodeValToRGB');base=(.245,.15,.078) if idx==WOOD else (.34,.245,.135)
    ramp.color_ramp.elements[0].color=(*(v*.65 for v in base),1);ramp.color_ramp.elements[1].color=(*(v*1.25 for v in base),1)
    l.new(noise.outputs['Fac'],ramp.inputs[0]);l.new(ramp.outputs[0],p.inputs['Base Color'])
    bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.18;bump.inputs['Distance'].default_value=.008;l.new(noise.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs[0],p.inputs['Normal'])
# Fine paint grain, not heavy relief.
for idx in [HULL,WHITE,TRIM]:
    for n in M[idx].node_tree.nodes:
        if n.type=='BUMP':n.inputs['Distance'].default_value=.004

sea=bpy.data.collections.new('09 Mar | somente apresentacao');scene.collection.children.link(sea)
water=bpy.data.materials.new('Mar | agua profunda e ondulacao');water.use_nodes=True
n=water.node_tree.nodes;l=water.node_tree.links;p=next(q for q in n if q.type=='BSDF_PRINCIPLED')
p.inputs['Base Color'].default_value=(.012,.049,.065,1);p.inputs['Roughness'].default_value=.21;p.inputs['Metallic'].default_value=.1;p.inputs['IOR'].default_value=1.333
p.inputs['Transmission Weight'].default_value=.12
coord=n.new('ShaderNodeTexCoord');mapping=n.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY';mapping.inputs[1].default_value=(1.0,2.3,1);l.new(coord.outputs['Object'],mapping.inputs[0])
noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=.8;noise.inputs['Detail'].default_value=3;noise.inputs['Roughness'].default_value=.65;l.new(mapping.outputs[0],noise.inputs[0])
bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.35;bump.inputs['Distance'].default_value=.24;l.new(noise.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs[0],p.inputs['Normal'])
def wz(x,y):return -.12+.12*math.sin(x*.38+y*.65)+.08*math.sin(x*.9-y*.5)+.14*math.sin(x*.16+y*.21)
verts=[];nres=220;extent=440
for i in range(nres+1):
    for j in range(nres+1):
        x=-extent/2+i*extent/nres;y=-extent/2+j*extent/nres;verts.append((x,y,wz(x,y)))
faces=[(i*(nres+1)+j,(i+1)*(nres+1)+j,(i+1)*(nres+1)+j+1,i*(nres+1)+j+1) for i in range(nres) for j in range(nres)]
mesh=bpy.data.meshes.new('Mar | malha de ondas');mesh.from_pydata(verts,[],faces);mesh.materials.append(water)
o=bpy.data.objects.new('Mar | superficie independente',mesh);sea.objects.link(o)
for p in mesh.polygons:p.use_smooth=True
sub=o.modifiers.new('Ondulacao suave','SUBSURF');sub.levels=1;sub.render_levels=1
foam=bpy.data.materials.new('Espuma | microbolhas e transparencia');foam.use_nodes=True
n=foam.node_tree.nodes;l=foam.node_tree.links;p=next(q for q in n if q.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=(.55,.65,.64,1);p.inputs['Roughness'].default_value=.65
coord=n.new('ShaderNodeTexCoord');noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=5;noise.inputs['Detail'].default_value=3;l.new(coord.outputs['Object'],noise.inputs['Vector'])
ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.4;ramp.color_ramp.elements[0].color=(0,0,0,1);ramp.color_ramp.elements[1].position=.7
l.new(noise.outputs['Fac'],ramp.inputs[0]);l.new(ramp.outputs[0],p.inputs['Alpha'])
M.append(foam);FOAM=len(M)-1
foamgeo=Build()
for side in [-1,1]:
    vs=[]
    for i in range(150):
        x=-70+i*129/149;w=width(x) if x>=-59 else 2.2+(-59-x)*.14
        w+=.15+.18*math.sin(x*1.5)
        for j in range(3):
            y=side*(w+j*(.18+.14*math.sin(x*.8)));vs.append((x,y,wz(x,y)+.075))
    foamgeo.part(vs,[(i*3+j,(i+1)*3+j,(i+1)*3+j+1,i*3+j+1) for i in range(149) for j in range(2)],FOAM)
o=foamgeo.object('Esteira | espuma junto ao costado','08');COL['08'].objects.unlink(o);sea.objects.link(o)

# Presentation cameras; no sea geometry is linked to the ship collection.
cam=bpy.data.objects['Camera | tres quartos'];cam.location=(70,-190,60);cam.rotation_euler=(Vector((0,0,13))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=136
scene.camera=cam
scene.render.resolution_x=1800;scene.render.resolution_y=1050;scene.render.resolution_percentage=100
scene.cycles.samples=96;scene.cycles.use_denoising=True
scene.render.filepath=str(OUT/'Itapage_Mar.png')
bpy.context.view_layer.update()
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_perspective='CAMERA'
        area.spaces.active.region_3d.view_camera_zoom=0
        area.spaces.active.shading.type='MATERIAL'
        area.spaces.active.overlay.show_overlays=False
bpy.app.driver_namespace['ITAPAGE_BUILD']=globals().copy()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Itapage.blend'))
bpy.ops.render.render(write_still=True)
print('FINAL PREVIEW',scene.render.filepath)
