import bpy, json, math, os, re
from pathlib import Path
from mathutils import Vector
ROOT=Path('E:/Unity/MonteCastello1945');OUT=ROOT/'ArtSource/Itapage/UnityDelivery';ASSET=ROOT/'Assets/Art/Ships/Itapage'
source=bpy.context.scene
ship=[o for c in source.collection.children if c.name.startswith('ITAPAGE') for sc in c.children if sc.name[:2] in ['01','02','03','04','05','06','07'] for o in sc.objects if o.type in {'MESH','FONT'}]
flag=bpy.data.objects['Brasil_Bandeira_Animada']
materials=sorted({m for o in ship for m in o.data.materials if m},key=lambda m:m.name)
bake=bpy.data.scenes.new('ITAPAGE | Bake PBR');bpy.context.window.scene=bake;bake.render.engine='CYCLES';bake.cycles.samples=8;bake.cycles.device='GPU'
bake.render.bake.margin=8;bake.render.bake.use_clear=True
mesh=bpy.data.meshes.new('BakeTile4m');mesh.from_pydata([(0,0,0),(4,0,0),(4,4,0),(0,4,0)],[],[(0,1,2,3)]);mesh.update()
uv=mesh.uv_layers.new(name='UVMap')
for i,co in enumerate([(0,0),(1,0),(1,1),(0,1)]):uv.data[i].uv=co
tile=bpy.data.objects.new('BakeTile4m',mesh);bake.collection.objects.link(tile);tile.select_set(True);bpy.context.view_layer.objects.active=tile
manifest={'materials':[],'staticModels':[],'flagModel':'Assets/Art/Ships/Itapage/Models/Itapage_Bandeira.fbx','source':'ArtSource/Itapage/Itapage.blend','animation':{}}
exportmats={}
for index,original in enumerate(materials):
    name='ITAPAGE_M%02d'%index;isflag=original==flag.data.materials[0]
    m=original.copy();m.name='Bake_'+name;mesh.materials.clear();mesh.materials.append(m)
    n=m.node_tree.nodes;l=m.node_tree.links;principled=next(t for t in n if t.type=='BSDF_PRINCIPLED');output=next(t for t in n if t.type=='OUTPUT_MATERIAL')
    origsurface=output.inputs['Surface'].links[0].from_socket
    base=principled.inputs['Base Color'];em=n.new('ShaderNodeEmission')
    if base.is_linked:l.new(base.links[0].from_socket,em.inputs['Color'])
    else:em.inputs['Color'].default_value=base.default_value
    l.new(em.outputs[0],output.inputs['Surface'])
    image=bpy.data.images.new(name+'_BaseColor',width=1024,height=1024,alpha=False);image.colorspace_settings.name='sRGB'
    target=n.new('ShaderNodeTexImage');target.image=image
    for node in n:node.select=False
    target.select=True;n.active=target
    bpy.ops.object.bake(type='EMIT')
    image.filepath_raw=str(ASSET/'Textures'/(name+'_BaseColor.png'));image.file_format='PNG';image.save()
    l.new(origsurface,output.inputs['Surface'])
    normal=bpy.data.images.new(name+'_Normal',width=1024,height=1024,alpha=False);normal.colorspace_settings.name='Non-Color';target.image=normal
    bpy.ops.object.bake(type='NORMAL')
    normal.filepath_raw=str(ASSET/'Textures'/(name+'_Normal.png'));normal.file_format='PNG';normal.save()
    rough=float(principled.inputs['Roughness'].default_value);metal=float(principled.inputs['Metallic'].default_value)
    exp=bpy.data.materials.new(name);exp.use_nodes=True;pn=next(t for t in exp.node_tree.nodes if t.type=='BSDF_PRINCIPLED')
    pn.inputs['Roughness'].default_value=rough;pn.inputs['Metallic'].default_value=metal
    bc=exp.node_tree.nodes.new('ShaderNodeTexImage');bc.image=image;exp.node_tree.links.new(bc.outputs[0],pn.inputs['Base Color'])
    nm=exp.node_tree.nodes.new('ShaderNodeTexImage');nm.image=normal;nn=exp.node_tree.nodes.new('ShaderNodeNormalMap');exp.node_tree.links.new(nm.outputs[0],nn.inputs['Color']);exp.node_tree.links.new(nn.outputs[0],pn.inputs['Normal'])
    exportmats[original]=exp
    manifest['materials'].append({'name':name,'original':original.name,'albedo':'Assets/Art/Ships/Itapage/Textures/'+name+'_BaseColor.png','normal':'Assets/Art/Ships/Itapage/Textures/'+name+'_Normal.png','metallic':metal,'smoothness':1-rough,'twoSided':isflag})
    (OUT/'bake_progress.json').write_text(json.dumps({'done':index+1,'total':len(materials),'material':original.name}),encoding='utf-8')
    bpy.data.materials.remove(m)
# Build an isolated Unity export scene; the editable authoring scene is untouched.
export=bpy.data.scenes.new('ITAPAGE | Unity Export');bpy.context.window.scene=source;source.frame_set(1)
deps=bpy.context.evaluated_depsgraph_get();copies=[]
for ob in ship:
    if ob==flag:continue
    ev=ob.evaluated_get(deps);data=bpy.data.meshes.new_from_object(ev,preserve_all_data_layers=True,depsgraph=deps)
    cp=bpy.data.objects.new(re.sub(r'[^a-zA-Z0-9_]','_',ob.name),data);cp.matrix_world=ob.matrix_world.copy();export.collection.objects.link(cp)
    for i,mat in enumerate(list(data.materials)):data.materials[i]=exportmats[mat]
    copies.append(cp)
bpy.context.window.scene=export
for o in export.objects:o.select_set(False)
for o in copies:o.select_set(True)
bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();static=bpy.context.object;static.name='Itapage_Estrutura_LOD0'
static.location=(0,0,0)
# One shared material slot per surface type keeps draw calls bounded.
oldslots=list(static.data.materials);unique=list(dict.fromkeys(oldslots));indices=[unique.index(oldslots[p.material_index]) for p in static.data.polygons]
static.data.materials.clear()
for m in unique:static.data.materials.append(m)
for p,idx in zip(static.data.polygons,indices):p.material_index=idx
def export_fbx(path,objects,animation=False):
    for o in export.objects:o.select_set(False)
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=not any(o.data.shape_keys for o in objects),mesh_smooth_type='FACE',use_tspace=True,axis_forward='-Z',axis_up='Y',bake_space_transform=False,bake_anim=animation,path_mode='AUTO',embed_textures=False,add_leaf_bones=False)
for lod,ratio in [(0,1),(1,.48),(2,.18)]:
    cp=static if lod==0 else static.copy()
    if lod:
        cp.data=static.data.copy();cp.name='Itapage_Estrutura_LOD%d'%lod;export.collection.objects.link(cp)
        bpy.context.view_layer.objects.active=cp;cp.select_set(True)
        mod=cp.modifiers.new('LOD geometrico','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    filename='Itapage_LOD%d.fbx'%lod;export_fbx(ASSET/'Models'/filename,[cp])
    manifest['staticModels'].append({'lod':lod,'path':'Assets/Art/Ships/Itapage/Models/'+filename,'vertices':len(cp.data.vertices),'polygons':len(cp.data.polygons)})
    cp.hide_set(True)
fc=flag.copy();fc.data=flag.data.copy();fc.name='Brasil_Bandeira';export.collection.objects.link(fc)
fc.data.materials.clear();fc.data.materials.append(exportmats[flag.data.materials[0]])
# Unity material uses Cull Off; the original front UV is retained as real printed cloth.
export_fbx(ASSET/'Models'/'Itapage_Bandeira.fbx',[fc],False)
keys=flag.data.shape_keys.key_blocks
frames=[]
bpy.context.window.scene=source
for f in range(1,242):
    source.frame_set(f);frames.append({'time':(f-1)/30,'weights':[float(k.value)*100 for k in list(keys)[1:]]})
manifest['animation']={'name':'Brasil_Vento_Loop','length':8,'frameRate':30,'shapes':[k.name for k in list(keys)[1:]],'frames':frames}
(OUT/'unity_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
source.frame_set(1)
# Export scene remains separate in the delivery blend; authoring file preserves its view.
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Itapage_Unity.blend'),copy=True)
bpy.context.window.scene=source
print(json.dumps({'materials':len(materials),'lods':manifest['staticModels'],'flagShapes':len(keys)-1,'loopSeamMaxError':max(abs(a-b) for a,b in zip(frames[0]['weights'],frames[-1]['weights']))}))
