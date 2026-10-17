import bpy,json,hashlib,math
from pathlib import Path
from mathutils import Vector,Matrix
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
assets=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldiers');models=assets/'Models';models.mkdir(exist_ok=True)
s=bpy.data.scenes['FEB_Soldiers'];bpy.context.window.scene=s
archive=bpy.data.collections['Acessorios_Rangers_retirados']
# Denser surface fit prevents a patch edge bridging a fold of the sleeve.
for idx in range(1,7):
    body=bpy.data.objects.get('FEB_%02d_M41_parte_superior'%idx) or bpy.data.objects.get('FEB_%02d_Corpo_uniforme'%idx)
    for key in ['FEB','FifthArmy']:
        o=bpy.data.objects['FEB_%02d_Emblema_%s'%(idx,key)]
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
        sub=o.modifiers.new('Ajuste fino do bordado','SUBSURF');sub.subdivision_type='SIMPLE';sub.levels=1
        bpy.ops.object.modifier_apply(modifier=sub.name)
        wrap=o.modifiers.new('Acompanha a manga','SHRINKWRAP');wrap.target=body;wrap.wrap_method='NEAREST_SURFACEPOINT';wrap.wrap_mode='ABOVE_SURFACE';wrap.offset=.0008
        bpy.ops.object.modifier_apply(modifier=wrap.name)
visible=[o for i in range(1,7) for o in bpy.data.collections['FEB_%02d'%i].objects if o.type=='MESH']
images=set(n.image for o in visible for m in o.data.materials if m and m.use_nodes for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image)
for im in images:
    if len(im.pixels):im.pack()
# Separate export scene: joined objects leave the editable parts intact.
ex=bpy.data.scenes.new('FEB_Exportacao_FBX');ex.unit_settings.system='METRIC';ex.unit_settings.scale_length=1
bpy.context.window.scene=ex
export_objects=[];report={'variants':[],'source_file':'Rangers.fbx','source_has_rig':False,'source_has_animation':False,'reference_emblems_unchanged':{}}
for idx in range(1,7):
    copies=[]
    for o in bpy.data.collections['FEB_%02d'%idx].objects:
        if o.type!='MESH':continue
        cp=o.copy();cp.data=o.data.copy();ex.collection.objects.link(cp);copies.append(cp)
    bpy.ops.object.select_all(action='DESELECT')
    for cp in copies:cp.select_set(True)
    bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join()
    ob=bpy.context.object;ob.name='FEB_Soldado_%02d'%idx
    # All geometry was normalized in metres; export each soldier at the origin.
    ob.location=(0,0,0)
    ob.data.calc_loop_triangles()
    ob['variant']=idx;ob['origin_asset']='User supplied Rangers.fbx';ob['rig']='Source contains no bones or skin weights'
    p=models/('FEB_Soldado_%02d.fbx'%idx)
    bpy.ops.export_scene.fbx(filepath=str(p),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='OFF',use_triangles=True,bake_anim=False,path_mode='RELATIVE',embed_textures=False,add_leaf_bones=False,use_custom_props=True)
    report['variants'].append({'name':ob.name,'triangles':len(ob.data.loop_triangles),'vertices':len(ob.data.vertices),'materials':[m.name for m in ob.data.materials],'file':str(p),'height_m':float(ob.dimensions.z)})
    ob.location.x=(idx-3.5)*1.4;export_objects.append(ob)
bpy.ops.object.select_all(action='DESELECT')
for o in export_objects:o.select_set(True)
all_file=models/'FEB_Soldiers_6_Soldados.fbx'
bpy.ops.export_scene.fbx(filepath=str(all_file),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',mesh_smooth_type='OFF',use_triangles=True,bake_anim=False,path_mode='RELATIVE',embed_textures=False,add_leaf_bones=False,use_custom_props=True)
report['combined_file']=str(all_file)
for key in ['FEB','FifthArmy']:
    p=assets/'Textures'/(key+'_Patch.png');q=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldier/Textures')/p.name
    report['reference_emblems_unchanged'][key]=hashlib.sha256(p.read_bytes()).hexdigest()==hashlib.sha256(q.read_bytes()).hexdigest()
report['images']=[{'file':str(Path(bpy.path.abspath(im.filepath))),'size':list(im.size)} for im in images]
report['triangles_total']=sum(v['triangles'] for v in report['variants'])
(out/'export_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.context.window.scene=s
for a in bpy.context.screen.areas:
    if a.type=='VIEW_3D':
        a.spaces.active.shading.type='MATERIAL';a.spaces.active.region_3d.view_location=(0,0,.95);a.spaces.active.region_3d.view_distance=10
        a.spaces.active.region_3d.view_rotation=Vector((0,-1,.12)).to_track_quat('Z','Y');a.spaces.active.show_region_ui=False
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result=report
