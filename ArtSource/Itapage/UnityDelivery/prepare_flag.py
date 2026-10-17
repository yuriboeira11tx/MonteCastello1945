import bpy, math, json, os
from pathlib import Path
from mathutils import Vector
ROOT=Path('E:/Unity/MonteCastello1945')
OUT=ROOT/'ArtSource/Itapage/UnityDelivery'
ASSET=ROOT/'Assets/Art/Ships/Itapage'
scene=bpy.context.scene
backup=ROOT/'ArtSource/Itapage/Backups/Itapage_Antes_Bandeira_Animada.blend'
if not backup.exists():bpy.ops.wm.save_as_mainfile(filepath=str(backup),copy=True)
old=[o for o in scene.objects if o.name.startswith(('Pavilhao brasileiro','Pavilhao | faixa'))]
flagcol=bpy.data.collections.get('07 Identificacao')
assert flagcol is not None
for o in old:bpy.data.objects.remove(o,do_unlink=True)
# 10:7 historic national ensign. The hoist remains pinned along the pole.
nu,nv=64,44
length,height=3.8,2.66
pole=bpy.data.objects['Bandeira | pau de bandeira da popa']
top=max((pole.matrix_world@Vector(c)).z for c in pole.bound_box)-.24
verts=[];uvs=[]
for i in range(nu+1):
    u=i/nu
    for j in range(nv+1):
        v=j/nv
        verts.append((-58.47-u*length+.10*u*u,.07+.045*math.sin(v*math.tau)*u,top-v*height-.2*u*u))
        uvs.append((u,1-v))
faces=[(i*(nv+1)+j,(i+1)*(nv+1)+j,(i+1)*(nv+1)+j+1,i*(nv+1)+j+1) for i in range(nu) for j in range(nv)]
mesh=bpy.data.meshes.new('Brasil_Tecido_UV');mesh.from_pydata(verts,[],faces);mesh.update()
ob=bpy.data.objects.new('Brasil_Bandeira_Animada',mesh);flagcol.objects.link(ob)
uv=mesh.uv_layers.new(name='UV_Bandeira')
for poly in mesh.polygons:
    poly.use_smooth=True
    for li in poly.loop_indices:uv.data[li].uv=uvs[mesh.loops[li].vertex_index]
m=bpy.data.materials.new('ITAPAGE_Brasil_Tecido');m.use_nodes=True
n=m.node_tree.nodes;l=m.node_tree.links;p=next(t for t in n if t.type=='BSDF_PRINCIPLED')
p.inputs['Roughness'].default_value=.83;p.inputs['Sheen Weight'].default_value=.28;p.inputs['Sheen Roughness'].default_value=.65
img=bpy.data.images.load(str(ASSET/'Textures/Brasil_1889_1960_BaseColor.png'),check_existing=True);img.pack()
tex=n.new('ShaderNodeTexImage');tex.image=img;tex.interpolation='Linear'
col=n.new('ShaderNodeMixRGB');col.blend_type='MULTIPLY';col.inputs[0].default_value=1;col.inputs[2].default_value=(.86,.86,.8,1);l.new(tex.outputs['Color'],col.inputs[1]);l.new(col.outputs[0],p.inputs['Base Color'])
tc=n.new('ShaderNodeTexCoord')
wave1=n.new('ShaderNodeTexWave');wave1.bands_direction='X';wave1.inputs['Scale'].default_value=190;wave1.inputs['Distortion'].default_value=.7
wave2=n.new('ShaderNodeTexWave');wave2.bands_direction='Y';wave2.inputs['Scale'].default_value=240;wave2.inputs['Distortion'].default_value=.7
for wave in [wave1,wave2]:l.new(tc.outputs['UV'],wave.inputs['Vector'])
mul=n.new('ShaderNodeMath');mul.operation='MULTIPLY';l.new(wave1.outputs['Color'],mul.inputs[0]);l.new(wave2.outputs['Color'],mul.inputs[1])
bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.23;bump.inputs['Distance'].default_value=.0012;l.new(mul.outputs[0],bump.inputs['Height']);l.new(bump.outputs[0],p.inputs['Normal'])
mesh.materials.append(m)
# Four travelling waves, represented by positive blend-shape pairs. This exports
# to a native Unity AnimationClip without a runtime cloth simulation or script.
ob.shape_key_add(name='Basis')
functions=[
    lambda u,v:Vector((.028*u*math.cos(5*u-1.5*v),.43*u**.9*(.78+.22*v)*math.sin(5*u-1.5*v),.028*u*math.cos(5*u-1.5*v))),
    lambda u,v:Vector((.02*u*math.sin(5*u-1.5*v),.43*u**.9*(.78+.22*v)*math.cos(5*u-1.5*v),.028*u*math.sin(5*u-1.5*v))),
    lambda u,v:Vector((0,.14*u**1.5*math.sin(12*u+3*v),.055*u**1.4*math.cos(12*u+3*v))),
    lambda u,v:Vector((0,.14*u**1.5*math.cos(12*u+3*v),.055*u**1.4*math.sin(12*u+3*v))),
    lambda u,v:Vector((.025*u,.13*u*math.sin(2*u),.075*u*u*math.cos(2*u))),
    lambda u,v:Vector((.0,.13*u*math.cos(2*u),.075*u*u*math.sin(2*u))),
    lambda u,v:Vector((0,.038*u**2*math.sin(24*u-4*v),.022*u*u*math.cos(24*u-4*v))),
    lambda u,v:Vector((0,.038*u**2*math.cos(24*u-4*v),.022*u*u*math.sin(24*u-4*v)))
]
shapes=[]
for wave,fn in enumerate(functions):
    for sign in [1,-1]:
        key=ob.shape_key_add(name='Vento_%02d_%s'%(wave+1,'A' if sign==1 else 'B'))
        for i,v in enumerate(mesh.vertices):
            u=i//(nv+1)/nu;vv=(i%(nv+1))/nv;key.data[i].co=v.co+fn(u,vv)*sign
        shapes.append((key,wave,sign))
scene.render.fps=30;scene.frame_start=1;scene.frame_end=241
for frame in range(1,242):
    t=(frame-1)/240*math.tau
    for key,wave,sign in shapes:
        freq=[5,5,11,11,1,1,17,17][wave]
        phase=math.sin(freq*t) if wave%2==0 else math.cos(freq*t)
        key.value=.5+sign*.5*phase;key.keyframe_insert(data_path='value',frame=frame)
act=mesh.shape_keys.animation_data.action;act.name='Brasil_Vento_Loop_8s'
for layer in act.layers:
    for strip in layer.strips:
        for slot in act.slots:
            bag=strip.channelbag(slot)
            if bag:
                for f in bag.fcurves:
                    for k in f.keyframe_points:k.interpolation='LINEAR'
# A subtle cotton normal map is baked later; UVs stay attached to the cloth.
ob['Animacao']='Ciclo continuo de 8 segundos, 30 FPS, borda presa ao mastro'
ob['Bandeira']='Brasil 1889-1960, 21 estrelas, proporcao 10:7'
scene.frame_set(1)
# Close camera for checking the flag and its shading.
camdata=bpy.data.cameras.new('Camera | Bandeira');cam=bpy.data.objects.new('Camera | Bandeira',camdata)
bpy.data.collections['08 Apresentacao'].objects.link(cam)
cam.location=(-62.5,-7,top-1);cam.rotation_euler=(Vector((-60.5,0,top-1.3))-cam.location).to_track_quat('-Z','Y').to_euler();camdata.type='ORTHO';camdata.ortho_scale=5.4
previous=(scene.camera,scene.render.resolution_x,scene.render.resolution_y,scene.render.filepath)
scene.camera=cam;scene.render.resolution_x=1100;scene.render.resolution_y=780
sea=bpy.data.collections.get('09 Mar | somente apresentacao');oldsea=sea.hide_render;sea.hide_render=True
scene.render.filepath=str(OUT/'Bandeira_Detalhe.png');scene.cycles.samples=48
bpy.ops.render.render(write_still=True)
sea.hide_render=oldsea;scene.camera,scene.render.resolution_x,scene.render.resolution_y,scene.render.filepath=previous
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Itapage/Itapage.blend'))
print(json.dumps({'flag':ob.name,'vertices':len(mesh.vertices),'shapes':len(shapes),'frames':241,'fps':30,'duration_s':8,'action':act.name,'top_z':top}))
