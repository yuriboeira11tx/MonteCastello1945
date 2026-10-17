"""Bake editable Blender materials into portable PBR maps and export the rig."""
import bpy, json, math
from pathlib import Path
import numpy as np
SRC=Path(__file__).resolve().parent
OUT=SRC.parents[1]/'Assets/Art/Characters/FEB_Soldier/Realista'
TEX=OUT/'Textures';MODELS=OUT/'Models'
bpy.ops.wm.open_mainfile(filepath=str(SRC/'FEB_Soldier_Realista.blend'))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16
scene.render.bake.margin=12;scene.render.bake.use_clear=True;scene.render.bake.use_selected_to_active=False
rig=bpy.data.objects['FEB_Rig']
chars=[o for o in bpy.data.collections['FEB_Soldier'].objects if o.type=='MESH']
head=next(o for o in chars if 'Pele facial' in o.name)
eyes=next(o for o in chars if 'Olhos castanhos' in o.name)
patches=[o for o in chars if 'Distintivo_' in o.name]
bodyparts=[o for o in chars if o not in [head,eyes]+patches]
bpy.ops.object.select_all(action='DESELECT')
for o in bodyparts:o.select_set(True)
bpy.context.view_layer.objects.active=bodyparts[0];bpy.ops.object.join();body=bpy.context.object;body.name='FEB_Uniforme_Equipamento_URP'
# Retain a reasonably detailed game mesh and the original sculpt in the source blend.
tris=sum(len(p.vertices)-2 for p in body.data.polygons)
if tris>72000:
    dec=body.modifiers.new('Reducao_exportacao','DECIMATE');dec.ratio=72000/tris;bpy.ops.object.modifier_apply(modifier=dec.name)
while body.data.uv_layers:body.data.uv_layers.remove(body.data.uv_layers[0])
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(65),island_margin=.0015);bpy.ops.object.mode_set(mode='OBJECT')

def bake(obj,prefix,res):
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    materials=list(obj.data.materials)
    result={};original={}
    for m in materials:
        out=next(n for n in m.node_tree.nodes if n.type=='OUTPUT_MATERIAL')
        original[m.name]=out.inputs['Surface'].links[0].from_socket
    for channel in ['BaseColor','Normal','Roughness','Metallic']:
        image=bpy.data.images.new(prefix+'_'+channel,width=res,height=res,alpha=False)
        image.colorspace_settings.name='sRGB' if channel=='BaseColor' else 'Non-Color'
        for m in materials:
            nodes=m.node_tree.nodes;links=m.node_tree.links
            b=nodes.get('Principled BSDF');out=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
            for n in nodes:n.select=False
            target=nodes.new('ShaderNodeTexImage');target.image=image;target.select=True;nodes.active=target
            if channel=='Normal':links.new(original[m.name],out.inputs['Surface'])
            else:
                emission=nodes.new('ShaderNodeEmission');inp=b.inputs[{'BaseColor':'Base Color','Roughness':'Roughness','Metallic':'Metallic'}[channel]]
                if inp.is_linked:links.new(inp.links[0].from_socket,emission.inputs['Color'])
                elif channel=='BaseColor':emission.inputs['Color'].default_value=inp.default_value
                else:emission.inputs['Color'].default_value=(*[inp.default_value]*3,1)
                links.new(emission.outputs[0],out.inputs['Surface'])
        bpy.ops.object.bake(type='NORMAL' if channel=='Normal' else 'EMIT')
        image.filepath_raw=str(TEX/(prefix+'_'+channel+'.png'));image.file_format='PNG';image.save()
        result[channel]=image
        print('BAKED',prefix,channel,flush=True)
    for m in materials:
        out=next(n for n in m.node_tree.nodes if n.type=='OUTPUT_MATERIAL');m.node_tree.links.new(original[m.name],out.inputs['Surface'])
    m=bpy.data.materials.new(prefix+'_PBR');m.use_nodes=True;nodes=m.node_tree.nodes;links=m.node_tree.links;b=nodes.get('Principled BSDF')
    for channel,im in result.items():
        n=nodes.new('ShaderNodeTexImage');n.image=im
        if channel=='Normal':
            nm=nodes.new('ShaderNodeNormalMap');links.new(n.outputs[0],nm.inputs['Color']);links.new(nm.outputs[0],b.inputs['Normal'])
        else:links.new(n.outputs[0],b.inputs[{'BaseColor':'Base Color','Roughness':'Roughness','Metallic':'Metallic'}[channel]])
    obj.data.materials.clear();obj.data.materials.append(m)
    for p in obj.data.polygons:p.material_index=0
    # URP metallic RGB with smoothness in alpha, in linear data space.
    metal=np.empty(res*res*4,dtype=np.float32);rough=np.empty_like(metal)
    result['Metallic'].pixels.foreach_get(metal);result['Roughness'].pixels.foreach_get(rough)
    combined=metal.reshape(-1,4);combined[:,3]=1-rough.reshape(-1,4)[:,0]
    packed=bpy.data.images.new(prefix+'_MetallicSmoothness_URP',width=res,height=res,alpha=True);packed.colorspace_settings.name='Non-Color';packed.pixels.foreach_set(combined.ravel());packed.filepath_raw=str(TEX/(prefix+'_MetallicSmoothness_URP.png'));packed.file_format='PNG';packed.save()
    return result

bake(body,'FEB_Uniforme',4096)
bake(head,'FEB_Rosto',2048)
bake(eyes,'FEB_Olhos',512)
bpy.ops.object.select_all(action='DESELECT')
objects=[body,head,eyes]+patches
for ob in objects:ob.select_set(True)
rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(MODELS/'FEB_Soldier_Realista.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,mesh_smooth_type='FACE',path_mode='RELATIVE')
bpy.ops.export_scene.gltf(filepath=str(SRC/'FEB_Soldier_Realista.glb'),export_format='GLB',use_selection=True,export_animations=False)
report={'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),'meshes':len(objects),'bones':len(rig.data.bones),'textures':'4K uniform/equipment, 2K face, 512 eyes, original PDF patches'}
(SRC/'export_report_realista.json').write_text(json.dumps(report,indent=2))
print('EXPORT_COMPLETE',json.dumps(report),flush=True)
