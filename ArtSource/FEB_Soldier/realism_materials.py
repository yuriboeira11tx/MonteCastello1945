"""Editable, physically scaled materials. Executed inside the Blender build."""
def srgb(v):
    v=v/255
    return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4

def pbr(name,rgb,rough,metal=0,kind='cloth'):
    m=bpy.data.materials.new(name);m.use_nodes=True
    n=m.node_tree.nodes;l=m.node_tree.links;b=n.get('Principled BSDF')
    col=tuple(srgb(x) for x in rgb)
    m.diffuse_color=(*col,1);b.inputs['Base Color'].default_value=(*col,1)
    b.inputs['Roughness'].default_value=rough;b.inputs['Metallic'].default_value=metal
    coord=n.new('ShaderNodeTexCoord')
    noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=38 if kind=='cloth' else 65
    noise.inputs['Detail'].default_value=3;noise.inputs['Roughness'].default_value=.7
    l.new(coord.outputs['Object'],noise.inputs['Vector'])
    ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.12;ramp.color_ramp.elements[1].position=.88
    ramp.color_ramp.elements[0].color=(*[x*.77 for x in col],1)
    ramp.color_ramp.elements[1].color=(*[min(1,x*1.16) for x in col],1)
    l.new(noise.outputs['Fac'],ramp.inputs[0]);l.new(ramp.outputs[0],b.inputs['Base Color'])
    variation=n.new('ShaderNodeMapRange');variation.inputs['From Min'].default_value=0;variation.inputs['From Max'].default_value=1
    variation.inputs['To Min'].default_value=max(.05,rough-.12);variation.inputs['To Max'].default_value=min(1,rough+.09)
    l.new(noise.outputs['Fac'],variation.inputs[0]);l.new(variation.outputs[0],b.inputs['Roughness'])
    micro=n.new('ShaderNodeTexNoise');micro.inputs['Scale'].default_value={'cloth':1800,'canvas':1300,'skin':2400,'paint':1500,'leather':900}.get(kind,1800)
    micro.inputs['Detail'].default_value=2;l.new(coord.outputs['Object'],micro.inputs['Vector'])
    bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.33;bump.inputs['Distance'].default_value=.00035 if kind in ['cloth','canvas'] else .00017
    l.new(micro.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs['Normal'],b.inputs['Normal'])
    if kind in ['cloth','canvas']:
        b.inputs['Sheen Weight'].default_value=.2;b.inputs['Sheen Roughness'].default_value=.8
        waves=[]
        for axis in ['X','Z']:
            w=n.new('ShaderNodeTexWave');w.wave_type='BANDS';w.bands_direction=axis;w.inputs['Scale'].default_value=1100 if kind=='cloth' else 700
            w.inputs['Distortion'].default_value=.3;l.new(coord.outputs['Object'],w.inputs['Vector']);waves.append(w)
        mul=n.new('ShaderNodeMath');mul.operation='MULTIPLY'
        l.new(waves[0].outputs[0],mul.inputs[0]);l.new(waves[1].outputs[0],mul.inputs[1])
        weave=n.new('ShaderNodeBump');weave.inputs['Distance'].default_value=.00012;weave.inputs['Strength'].default_value=.25
        l.new(mul.outputs[0],weave.inputs['Height']);l.new(bump.outputs[0],weave.inputs['Normal']);l.new(weave.outputs[0],b.inputs['Normal'])
    if kind=='skin':
        b.inputs['Subsurface Weight'].default_value=.12;b.inputs['Subsurface Radius'].default_value=(1,.45,.23);b.inputs['Subsurface Scale'].default_value=.012
    return m

mats=[
 pbr('01 • Sarja verde FEB',(94,100,76),.78),
 pbr('02 • Lona caqui',(133,129,90),.85,kind='canvas'),
 pbr('03 • Couro dos coturnos',(28,27,24),.49,kind='leather'),
 pbr('04 • Pintura fosca M1',(63,73,45),.64,kind='paint'),
 pbr('05 • Pele das maos',(179,130,99),.49,kind='skin'),
 pbr('06 • Ferragens escurecidas',(36,35,31),.38,.75,kind='metal'),
 pbr('07 • Latao',(149,118,65),.36,.85,kind='metal'),
 pbr('08 • Borracha',(24,25,23),.86,kind='leather'),
 pbr('09 • Linha de costura',(118,117,86),.85),
 pbr('10 • Labios',(136,83,70),.53,kind='skin'),
 pbr('11 • Esclera',(180,174,161),.28,kind='skin'),
 pbr('12 • Iris',(58,43,26),.35,kind='skin'),
 pbr('13 • Pelos',(34,27,23),.63,kind='hair'),
 pbr('14 • Forro',(62,61,42),.9),
 pbr('15 • Rebaixo da costura',(65,69,49),.9),
 pbr('16 • Aluminio',(135,142,139),.36,.94,kind='metal')]

def finish(obj,tile,bone='Chest',smooth=True):
    move_collection(obj,char);pieces.append(obj);obj.data.materials.append(mats[tile])
    if not obj.data.uv_layers:
        bpy.ops.object.select_all(action='DESELECT');bpy.context.view_layer.objects.active=obj;obj.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.02);bpy.ops.object.mode_set(mode='OBJECT');obj.select_set(False)
    for p in obj.data.polygons:p.use_smooth=smooth
    weights(obj,bone);return obj

def stitched(name,points,bone='Chest',tile=8,step=.004):
    # Individual short stitches, not a dark rope floating on the garment.
    cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.bevel_depth=.0003;cu.bevel_resolution=0
    for a,b in zip(points[:-1],points[1:]):
        a=Vector(a);b=Vector(b);d=b-a;count=max(1,int(d.length/step))
        for k in range(count):
            sp=cu.splines.new('POLY');sp.points.add(1)
            sp.points[0].co=(*(a+d*(k/count)),1);sp.points[1].co=(*(a+d*((k+.48)/count)),1)
    ob=bpy.data.objects.new(name,cu);scene.collection.objects.link(ob)
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.object.convert(target='MESH');finish(ob,tile,bone);ob.select_set(False)

def load_obj(path):
    v=[];uv=[];f=[];fuv=[];gr=[];group=''
    for line in Path(path).read_text().splitlines():
        p=line.split()
        if not p:continue
        if p[0]=='v':v.append(Vector(tuple(map(float,p[1:4]))))
        elif p[0]=='vt':uv.append(tuple(map(float,p[1:3])))
        elif p[0]=='g':group=p[1]
        elif p[0]=='f':
            f.append([int(x.split('/')[0])-1 for x in p[1:]])
            fuv.append([int(x.split('/')[1])-1 for x in p[1:]])
            gr.append(group)
    return v,uv,f,fuv,gr

def anatomical_head():
    import shutil
    ref=SRC/'ReferenceAssets'
    vs,uv,fs,fuv,gr=load_obj(ref/'mh_base.obj')
    for line in (ref/'male-young.target').read_text().splitlines():
        p=line.split()
        if p and not p[0].startswith('#'):vs[int(p[0])]+=Vector(tuple(map(float,p[1:4])))
    def transform(v):
        z=1.708+(v.y-8.2164)*.095
        x=v.x*.095
        blend=max(0,min(1,(1.608-z)/.035))
        limited=x/(1+(abs(x)/.052)**6)**(1/6)
        return (x*(1-blend)+limited*blend,-(v.z-.47)*.095+.006,z)
    selected=[i for i,f in enumerate(fs) if gr[i]=='body' and all(vs[j].y>6.26 for j in f)]
    inds=sorted(set(j for i in selected for j in fs[i]));idx={v:i for i,v in enumerate(inds)}
    me=bpy.data.meshes.new('Topologia_anatomica_CC0');me.from_pydata([transform(vs[j]) for j in inds],[],[[idx[j] for j in fs[i]] for i in selected]);me.update()
    ob=bpy.data.objects.new('Rosto_anatomico_e_pescoco',me);char.objects.link(ob);pieces.append(ob)
    layer=me.uv_layers.new(name='UV_MakeHuman')
    for poly,i in zip(me.polygons,selected):
        for li,ui in zip(poly.loop_indices,fuv[i]):layer.data[li].uv=uv[ui]
        poly.use_smooth=True
    skin=pbr('17 • Pele facial | poros e barba',(180,133,104),.49,kind='skin');nodes=skin.node_tree.nodes;links=skin.node_tree.links;b=nodes.get('Principled BSDF')
    for fname,socket,color in [('young_lightskinned_male_diffuse_Bronze.png','Base Color',True)]:
        shutil.copyfile(ref/fname,REAL_TEX/fname)
        tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(REAL_TEX/fname));tex.label=fname
        if not color:tex.image.colorspace_settings.name='Non-Color'
        if socket=='Normal':
            nm=nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.52;links.new(tex.outputs[0],nm.inputs['Color']);links.new(nm.outputs[0],b.inputs['Normal'])
        else:
            hs=nodes.new('ShaderNodeHueSaturation');hs.inputs['Saturation'].default_value=.84;hs.inputs['Value'].default_value=.84
            links.new(tex.outputs[0],hs.inputs['Color']);links.new(hs.outputs[0],b.inputs[socket])
    # A restrained beard shadow and tiny follicle variation, in metric object space.
    tc=nodes.new('ShaderNodeTexCoord');sep=nodes.new('ShaderNodeSeparateXYZ');links.new(tc.outputs['Object'],sep.inputs[0])
    def mathnode(op,a,b):
        n=nodes.new('ShaderNodeMath');n.operation=op
        for inp,v in zip(n.inputs,[a,b]):
            if isinstance(v,(int,float)):inp.default_value=v
            else:links.new(v,inp)
        return n.outputs[0]
    def smoothmask(socket,lo,hi,a=0,bv=1):
        n=nodes.new('ShaderNodeMapRange');n.interpolation_type='SMOOTHERSTEP';n.clamp=True
        n.inputs['From Min'].default_value=lo;n.inputs['From Max'].default_value=hi;n.inputs['To Min'].default_value=a;n.inputs['To Max'].default_value=bv;links.new(socket,n.inputs[0]);return n.outputs[0]
    lower=smoothmask(sep.outputs['Z'],1.650,1.693,1,0);front=smoothmask(sep.outputs['Y'],-.055,-.020,1,0)
    ax=mathnode('ABSOLUTE',sep.outputs['X'],0)
    outside=smoothmask(ax,.013,.036)
    chin=smoothmask(sep.outputs['Z'],1.614,1.642,1,0)
    region=mathnode('MULTIPLY',mathnode('MULTIPLY',lower,front),mathnode('MAXIMUM',outside,chin))
    follicles=nodes.new('ShaderNodeTexNoise');follicles.inputs['Scale'].default_value=2000;links.new(tc.outputs['Object'],follicles.inputs[0])
    beard=mathnode('MULTIPLY',region,mathnode('MULTIPLY',follicles.outputs['Fac'],.36))
    mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.inputs[2].default_value=(.05,.035,.024,1)
    links.new(hs.outputs[0],mix.inputs[1]);links.new(beard,mix.inputs[0]);links.new(mix.outputs[0],b.inputs['Base Color'])
    me.materials.append(skin)
    weights(ob,lambda p:[('Head',max(0,min(1,(p.z-1.55)/.06))),('Neck',1-max(0,min(1,(p.z-1.55)/.06)))])
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    sub=ob.modifiers.new('Definicao facial','SUBSURF');sub.levels=1;bpy.ops.object.modifier_apply(modifier=sub.name);ob.select_set(False)
    # Eyes fit the anatomical eyelids using the asset's explicit vertex correspondence.
    ev,eu,ef,efu,eg=load_obj(ref/'eyes.obj')
    lines=(ref/'eyes.mhclo').read_text().splitlines();start=next(i for i,x in enumerate(lines) if x.startswith('verts '))+1
    mapping=[int(x) for x in lines[start:] if x.strip().isdigit()]
    assert len(mapping)==len(ev)
    me=bpy.data.meshes.new('Olhos_anatomicos');me.from_pydata([transform(vs[i]) for i in mapping],[],ef);me.update()
    eyes=bpy.data.objects.new('Olhos_castanhos',me);char.objects.link(eyes);pieces.append(eyes)
    layer=me.uv_layers.new(name='UVMap')
    for poly,uis in zip(me.polygons,efu):
        for li,ui in zip(poly.loop_indices,uis):layer.data[li].uv=eu[ui]
        poly.use_smooth=True
    eye=pbr('18 • Olhos castanhos',(190,185,174),.2,kind='eye');en=eye.node_tree.nodes;el=eye.node_tree.links
    shutil.copyfile(ref/'brown_eye.png',REAL_TEX/'brown_eye.png')
    tex=en.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(REAL_TEX/'brown_eye.png'))
    hs=en.new('ShaderNodeHueSaturation');hs.inputs['Saturation'].default_value=.48;hs.inputs['Value'].default_value=.80
    el.new(tex.outputs[0],hs.inputs['Color']);el.new(hs.outputs[0],en.get('Principled BSDF').inputs['Base Color'])
    me.materials.append(eye);weights(eyes,'Head')
    bpy.context.view_layer.objects.active=eyes
    su=eyes.modifiers.new('Curvatura da cornea','SUBSURF');su.levels=2;bpy.ops.object.modifier_apply(modifier=su.name)
    # Short individual brow hairs projected on the forehead.
    import random
    random.seed(1945)
    for s in [-1,1]:
        for i in range(80):
            t=random.random();x=s*(.013+.039*t);z=1.727+.006*sin(pi*t)+random.uniform(-.0018,.0018)
            hit,loc,norm,face=ob.ray_cast(Vector((x,-.25,z)),Vector((0,1,0)))
            if hit:
                cord('Pelo_sobrancelha',[loc+Vector((0,-.0005,0)),loc+Vector((s*.002,-.0007,.0017))],.00018,12,'Head')
    ob['Base anatomica']='MakeHuman hm08 + male-young target, CC0. Textura Bronze, Margaret Toigo, CC0.'
    return ob
