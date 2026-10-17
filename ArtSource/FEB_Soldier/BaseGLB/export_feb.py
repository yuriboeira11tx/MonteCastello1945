"""Bake and export through the active Blender MCP session; source stays editable."""
import bpy,json,math,hashlib
import numpy as np
from pathlib import Path
ROOT=Path('E:/Unity/MonteCastello1945')
OUT=ROOT/'ArtSource/FEB_Soldier/BaseGLB'
ASSET=ROOT/'Assets/Art/Characters/FEB_Soldier/BaseGLB'
TEX=ASSET/'Textures';MODELS=ASSET/'Models'
TEX.mkdir(parents=True,exist_ok=True);MODELS.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes['FEB_BaseGLB'];bpy.context.window.scene=scene
source=bpy.data.collections['FEB_Personagem_BaseGLB'];rig=bpy.data.objects['FEB_Rig_BaseGLB']
previous=bpy.data.collections.get('FEB_Exportacao_URP')
if previous:
    for ob in list(previous.objects):bpy.data.objects.remove(ob,do_unlink=True)
    bpy.data.collections.remove(previous)
game=bpy.data.collections.new('FEB_Exportacao_URP');scene.collection.children.link(game)
patches=[bpy.data.objects[n] for n in ['FEB_Emblema_esquerdo','FEB_5Exercito_direito']]
copies=[];matcopies={}
for ob in source.objects:
    if ob.type!='MESH' or ob in patches:continue
    cp=ob.copy();cp.data=ob.data.copy();game.objects.link(cp);cp.hide_set(False);copies.append(cp)
    for slot in cp.material_slots:
        if slot.material:
            original=slot.material
            if original.name not in matcopies:matcopies[original.name]=original.copy()
            slot.material=matcopies[original.name]
bpy.ops.object.select_all(action='DESELECT')
for ob in copies:ob.select_set(True)
bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();body=bpy.context.object;body.name='FEB_GameBody'
uv=body.data.uv_layers.new(name='FEB_Atlas');body.data.uv_layers.active=uv;uv.active_render=True
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.006);bpy.ops.object.mode_set(mode='OBJECT')
scene.render.engine='CYCLES';scene.cycles.samples=16
scene.render.bake.margin=8;scene.render.bake.use_clear=True;scene.render.bake.use_selected_to_active=False

materials=list(body.data.materials);original_outputs={};targets={}
for m in materials:
    n=m.node_tree.nodes;out=next(n for n in n if n.type=='OUTPUT_MATERIAL')
    original_outputs[m.name]=out.inputs['Surface'].links[0].from_socket
    node=n.new('ShaderNodeTexImage');targets[m.name]=node

images={};res=2048
for channel,socket in [('BaseColor','Base Color'),('Normal',None),('Roughness','Roughness'),('Metallic','Metallic')]:
    im=bpy.data.images.get('FEB_BaseGLB_'+channel) or bpy.data.images.new('FEB_BaseGLB_'+channel,width=res,height=res,alpha=False)
    im.colorspace_settings.name='sRGB' if channel=='BaseColor' else 'Non-Color'
    for m in materials:
        nodes=m.node_tree.nodes;links=m.node_tree.links
        for n in nodes:n.select=False
        target=targets[m.name];target.image=im;target.select=True;nodes.active=target
        out=next(n for n in nodes if n.type=='OUTPUT_MATERIAL');bs=nodes.get('Principled BSDF')
        if channel=='Normal':links.new(original_outputs[m.name],out.inputs['Surface'])
        else:
            em=nodes.new('ShaderNodeEmission');inp=bs.inputs[socket]
            if inp.is_linked:links.new(inp.links[0].from_socket,em.inputs['Color'])
            else:em.inputs['Color'].default_value=inp.default_value if channel=='BaseColor' else (inp.default_value,inp.default_value,inp.default_value,1)
            links.new(em.outputs[0],out.inputs['Surface'])
    bpy.ops.object.bake(type='NORMAL' if channel=='Normal' else 'EMIT')
    im.filepath_raw=str(TEX/(im.name+'.png'));im.file_format='PNG';im.save();images[channel]=im
for m in materials:
    out=next(n for n in m.node_tree.nodes if n.type=='OUTPUT_MATERIAL');m.node_tree.links.new(original_outputs[m.name],out.inputs['Surface'])

material=bpy.data.materials.new('FEB_BaseGLB_PBR');material.use_nodes=True
nodes=material.node_tree.nodes;links=material.node_tree.links;bs=nodes.get('Principled BSDF')
for channel,im in images.items():
    node=nodes.new('ShaderNodeTexImage');node.image=im
    if channel=='Normal':
        nm=nodes.new('ShaderNodeNormalMap');links.new(node.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],bs.inputs['Normal'])
    else:links.new(node.outputs['Color'],bs.inputs[{'BaseColor':'Base Color','Roughness':'Roughness','Metallic':'Metallic'}[channel]])
body.data.materials.clear();body.data.materials.append(material)
for p in body.data.polygons:p.material_index=0
for layer in list(body.data.uv_layers):
    if layer.name!='FEB_Atlas':body.data.uv_layers.remove(layer)
body.data.uv_layers[0].name='UVMap'
metal=np.empty(res*res*4,dtype=np.float32);rough=np.empty_like(metal)
images['Metallic'].pixels.foreach_get(metal);images['Roughness'].pixels.foreach_get(rough)
packed=metal.reshape(-1,4);packed[:,3]=1-rough.reshape(-1,4)[:,0]
urp=bpy.data.images.get('FEB_BaseGLB_MetallicSmoothness_URP') or bpy.data.images.new('FEB_BaseGLB_MetallicSmoothness_URP',width=res,height=res,alpha=True);urp.colorspace_settings.name='Non-Color';urp.pixels.foreach_set(packed.ravel());urp.filepath_raw=str(TEX/(urp.name+'.png'));urp.file_format='PNG';urp.save()

# Limit each exported skin vertex to four normalized weights for Unity.
for ob in [body]+patches:
    for v in ob.data.vertices:
        ws=sorted([(g.group,g.weight) for g in v.groups if g.weight>1e-7 and ob.vertex_groups[g.group].name in rig.data.bones],key=lambda v:-v[1])[:4]
        total=sum(w for _,w in ws)
        for g in ob.vertex_groups:g.remove([v.index])
        for i,w in ws:ob.vertex_groups[i].add([v.index],w/total,'REPLACE')

for im in list(images.values())+[urp]:
    if im.packed_file:im.unpack(method='REMOVE')
    im.reload()
    im.pack()
bpy.ops.object.select_all(action='DESELECT')
for ob in [body,rig]+patches:ob.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(MODELS/'FEB_Soldado_BaseGLB.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,mesh_smooth_type='FACE',path_mode='RELATIVE')
bpy.ops.export_scene.gltf(filepath=str(OUT/'FEB_Soldado_BaseGLB.glb'),export_format='GLB',use_selection=True,export_animations=False,export_extras=True)
body.hide_render=True;body.hide_set(True)
for im in list(images.values())+[urp]:im.pack()
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FEB_Soldado_BaseGLB.blend'))
result={'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in [body]+patches),'meshes':3,'bones':len(rig.data.bones),'texture_resolution':res,'fbx':str(MODELS/'FEB_Soldado_BaseGLB.fbx'),'glb':str(OUT/'FEB_Soldado_BaseGLB.glb'),'blend':str(OUT/'FEB_Soldado_BaseGLB.blend'),'source_glb_sha256':hashlib.sha256(Path('C:/Users/yurib/Downloads/call_of_duty_1_soldier_rig.glb').read_bytes()).hexdigest(),'source_attribution':{'title':'Call Of Duty 1 Soldier rig','author':'_NETHER_FIRE_','license_declared_in_source':'CC-BY-4.0','url':'https://sketchfab.com/3d-models/call-of-duty-1-soldier-rig-2943f5966daf4f0a8f3d7a3eb45050ea'}}
(OUT/'export_report.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
