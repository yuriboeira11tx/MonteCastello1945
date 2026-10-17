import bpy, math, json
from mathutils import Vector
globals().update(bpy.app.driver_namespace['ITAPAGE_BUILD'])
sea=bpy.data.collections['09 Mar | somente apresentacao']
# Extend the distant water without a visible rectangular edge.
mesh=bpy.data.meshes.new('Horizonte maritimo');mesh.from_pydata([(-2500,-2500,-.48),(2500,-2500,-.48),(2500,2500,-.48),(-2500,2500,-.48)],[],[(0,1,2,3)])
mesh.materials.append(bpy.data.materials['Mar | agua profunda e ondulacao']);o=bpy.data.objects.new('Mar | horizonte continuo',mesh);sea.objects.link(o)
# Optional static exhaust only in the presentation collection.
smoke=bpy.data.materials.new('Fumaca | volume procedural');smoke.use_nodes=True
n=smoke.node_tree.nodes;n.clear();l=smoke.node_tree.links
out=n.new('ShaderNodeOutputMaterial');p=n.new('ShaderNodeVolumePrincipled');p.inputs['Color'].default_value=(.17,.17,.16,1);p.inputs['Anisotropy'].default_value=.15
tex=n.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=.75;tex.inputs['Detail'].default_value=4;tex.inputs['Roughness'].default_value=.7
c=n.new('ShaderNodeTexCoord');l.new(c.outputs['Object'],tex.inputs['Vector'])
ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.32;ramp.color_ramp.elements[0].color=(0,0,0,1);ramp.color_ramp.elements[1].position=.72
l.new(tex.outputs['Fac'],ramp.inputs[0]);sep=n.new('ShaderNodeSeparateXYZ');l.new(c.outputs['Object'],sep.inputs[0])
fade=n.new('ShaderNodeMapRange');fade.inputs['From Min'].default_value=-46;fade.inputs['From Max'].default_value=-20;fade.inputs['To Min'].default_value=0;fade.inputs['To Max'].default_value=.18;l.new(sep.outputs['X'],fade.inputs['Value'])
mul=n.new('ShaderNodeMath');mul.operation='MULTIPLY';l.new(ramp.outputs[0],mul.inputs[0]);l.new(fade.outputs[0],mul.inputs[1]);l.new(mul.outputs[0],p.inputs['Density']);l.new(p.outputs['Volume'],out.inputs['Volume'])
verts=[];nu=60;nv=40
for i in range(nu+1):
    t=i/nu;x=-14.93-31*t;z=24.15+4.8*(t**.7);rad=.62+2.1*t
    for j in range(nv):
        a=j*math.tau/nv;r=rad*(1+.13*math.sin(j*1.7+i*.8))
        verts.append((x,.4*math.sin(t*7)+r*math.cos(a),z+r*.7*math.sin(a)))
faces=[(i*nv+j,(i+1)*nv+j,(i+1)*nv+(j+1)%nv,i*nv+(j+1)%nv) for i in range(nu) for j in range(nv)]
faces += [tuple(range(nv-1,-1,-1)),tuple(nu*nv+j for j in range(nv))]
mesh=bpy.data.meshes.new('Exaustao | malha de volume');mesh.from_pydata(verts,[],faces);mesh.materials.append(smoke);o=bpy.data.objects.new('Fumaca | apresentacao estatica opcional',mesh);sea.objects.link(o)
o.display_type='WIRE'
# Scene notes and provenance, stored both with the asset and inside the .blend.
notes='''ITAPAGE - RECONSTRUCAO EXTERIOR PARA BLENDER

Criado em 17/10/2026 usando a ponte MCP local do Blender.
Arquivo principal: Itapage.blend
Unidades: metros. Comprimento do casco: 119,70 m. Boca modelada: 15,88 m.
Proa: eixo +X. Linha d'agua: Z=0. Casco completo abaixo da linha d'agua.

Organizacao: colecoes 01 a 07 = navio; 08 = cameras e luzes;
09 Mar = agua, esteira e fumaca estatica de apresentacao, independentes do navio.
Para ver as obras vivas, desligue a colecao 09 Mar.
Materiais: nos procedurais editaveis, com pintura naval, teca, metal, vidro,
bronze, lona e cordas. Materiais independem de imagens externas.
Malhas com UV_Metros para edicao posterior. Nao sao mapas PBR exportados.

O modelo foi construido como reconstrucao visual com base no postal fornecido
e em referencias historicas. Nao e uma replica certificada por plantas de estaleiro.
As fontes publicam comprimentos diferentes (cerca de 113 m e 119,7 m).
Adotou-se 119,7 m para o comprimento total desta reconstrucao.
Arranjo superior, interiores aparentes, botes, ferragens, emblema, bandeira
e obras vivas possuem aproximacoes onde nao havia detalhes suficientes.
Emblema da chamine interpretado do postal; estrela em objeto separado.
Nao foram modelados interiores completos, sistema de vapor ou salas de maquinas.
Fumaca e mar servem somente a visualizacao, sem simulacao nem animacao.

REFERENCIAS CONSULTADAS
1. Imagem fornecida pelo usuario, correspondente ao postal colorizado:
   Celio Maielo, Vapor Itapage (2007), Wikimedia Commons.
   https://commons.wikimedia.org/wiki/File:Vapor_Itapage.jpg
   A pagina identifica a imagem como interpretacao artistica de um postal.
2. Uboat.net - Itapage, construido em 1927, Chantier de Normandie, Rouen.
   https://uboat.net/allies/merchants/ships/3087.html
   Fotografia historica creditada a State Library of New South Wales.
3. Mauricio Carvalho, Naufragios do Brasil - Itapage.
   https://www.naufragiosdobrasil.com.br/naufragio/itapage/
   Medidas, casco de aco, dois eixos, guincho de proa, turcos e maquinas.
4. Biblioteca Nacional, reportagem de 1943: comprimento 112,77 m,
   boca 15,85 m, duas helices e dois motores.
   https://hemeroteca-pdf.bn.gov.br/093092/per093092_1943_04693.pdf

O navio de 1927 e descrito como mercante a motor diesel nas fontes;
o levantamento de naufragio tambem registra equipamentos a vapor.
Foi preservado o aspecto externo com chamine solicitado pelo usuario.

Nao foi importado para Assets do Unity. O arquivo permanece em ArtSource.
Para um futuro jogo, ainda sera necessario preparar LODs, colisoes e
converter/bakear os materiais procedurais para os shaders do motor.
'''
(OUT/'LEIA-ME_Itapage.txt').write_text(notes,encoding='utf-8')
t=bpy.data.texts.get('LEIA-ME | Itapage') or bpy.data.texts.new('LEIA-ME | Itapage');t.clear();t.write(notes)
for filename in ['build_itapage.py','details_itapage.py','equipment_itapage.py','presentation_itapage.py']:
    t=bpy.data.texts.get(filename) or bpy.data.texts.new(filename);t.clear();t.write((OUT/filename).read_text(encoding='utf-8'))
# Remove only unused startup objects from this fresh file, keeping the ship unchanged.
startup=bpy.data.scenes.get('Scene')
if startup and startup!=scene and set(o.name for o in startup.objects)=={'Cube','Camera','Light'}:
    for obj in list(startup.objects):bpy.data.objects.remove(obj,do_unlink=True)
    bpy.data.scenes.remove(startup)
ship_objects=[o for key,c in COL.items() if key!='08' for o in c.objects]
report={'file':str(OUT/'Itapage.blend'),'scene':scene.name,'ship_objects':len(ship_objects),'mesh_objects':sum(o.type=='MESH' for o in ship_objects),'faces':sum(len(o.data.polygons) for o in ship_objects if o.type=='MESH'),'vertices':sum(len(o.data.vertices) for o in ship_objects if o.type=='MESH'),'materials':len(set(m.name for o in ship_objects if o.type in {'MESH','FONT'} for m in o.data.materials)),'missing_external_images':[im.filepath for im in bpy.data.images if im.source=='FILE' and not im.packed_file and not os.path.exists(bpy.path.abspath(im.filepath))],'dimensions_hull_m':[round(v,3) for v in bpy.data.objects['Casco | 119.70m - chapas de aco'].dimensions],'lifeboats':sum(o.name.startswith('Baleeira') for o in ship_objects)}
(OUT/'validacao_itapage.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
scene.camera=bpy.data.objects['Camera | tres quartos'];scene.render.filepath=str(OUT/'Itapage_Mar.png')
bpy.context.view_layer.update();bpy.ops.render.render(write_still=True)
# Clean studio view reveals the full hull and propellers.
sea.hide_render=True
scene.render.film_transparent=True;scene.render.filepath=str(OUT/'Itapage_Modelo.png')
scene.camera.location=(87,-159,71);scene.camera.rotation_euler=(Vector((0,0,10))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.ortho_scale=140
bpy.ops.render.render(write_still=True)
scene.camera=bpy.data.objects['Camera | conves'];scene.render.filepath=str(OUT/'Itapage_Conves.png');scene.render.film_transparent=False
bpy.ops.render.render(write_still=True)
sea.hide_render=False;scene.camera=bpy.data.objects['Camera | tres quartos'];scene.camera.location=(70,-190,60);scene.camera.rotation_euler=(Vector((0,0,13))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.ortho_scale=136
scene.render.filepath=str(OUT/'Itapage_Mar.png')
# Keep ocean out of the modelling viewport while preserving it in renders.
for o in sea.objects:o.hide_set(True)
for o in scene.objects:o.select_set(False)
active=bpy.data.objects['Casco | 119.70m - chapas de aco'];active.select_set(True);bpy.context.view_layer.objects.active=active
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_perspective='PERSP';area.spaces.active.region_3d.view_distance=145
        area.spaces.active.region_3d.view_location=(0,0,10);area.spaces.active.region_3d.view_rotation=scene.camera.rotation_euler.to_quaternion()
        area.spaces.active.shading.type='MATERIAL';area.spaces.active.overlay.show_overlays=False
bpy.context.view_layer.update();bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Itapage.blend'))
print('Saved complete model and three previews')
