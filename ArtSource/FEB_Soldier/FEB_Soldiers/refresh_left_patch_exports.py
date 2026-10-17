import bpy,json,struct,hashlib,shutil
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
report=json.loads((out/'export_report.json').read_text())
fix=json.loads((out/'left_patch_fix_validation.json').read_text())
s=bpy.data.scenes['FEB_Soldiers'];ex=bpy.data.scenes['FEB_Exportacao_FBX']
bpy.context.window.scene=ex
for idx in range(1,7):
    old=bpy.data.objects['FEB_Soldado_%02d'%idx]
    copies=[]
    for o in bpy.data.collections['FEB_%02d'%idx].objects:
        if o.type!='MESH':continue
        cp=o.copy();cp.data=o.data.copy();ex.collection.objects.link(cp);copies.append(cp)
    bpy.ops.object.select_all(action='DESELECT')
    for cp in copies:cp.select_set(True)
    bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join()
    joined=bpy.context.object
    old.data.use_fake_user=True
    old.data=joined.data
    # Remove only the temporary joined copy made above; source pieces are untouched.
    bpy.data.objects.remove(joined,do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT');old.select_set(True);bpy.context.view_layer.objects.active=old
    original_location=old.location.copy();old.location=(0,0,0)
    path=report['variants'][idx-1]['file']
    bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='OFF',use_triangles=True,bake_anim=False,path_mode='RELATIVE',embed_textures=False,add_leaf_bones=False,use_custom_props=True)
    old.location=original_location;old.data.calc_loop_triangles()
    report['variants'][idx-1]['triangles']=len(old.data.loop_triangles)
    report['variants'][idx-1]['vertices']=len(old.data.vertices)
report['triangles_total']=sum(x['triangles'] for x in report['variants'])
bpy.ops.object.select_all(action='DESELECT')
for idx in range(1,7):bpy.data.objects['FEB_Soldado_%02d'%idx].select_set(True)
bpy.ops.export_scene.fbx(filepath=report['combined_file'],use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',mesh_smooth_type='OFF',use_triangles=True,bake_anim=False,path_mode='RELATIVE',embed_textures=False,add_leaf_bones=False,use_custom_props=True)
(out/'export_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
test=bpy.data.scenes.new('Verificacao_emblemas_FBX_corrigidos');bpy.context.window.scene=test
bpy.ops.import_scene.fbx(filepath=report['combined_file'])
meshes=[o for o in test.objects if o.type=='MESH'];n=Vector((.86,-.12,.5)).normalized()
checks=[]
for ob in meshes:
    ob.data.calc_loop_triangles();folds=0;deg=0;triangles=0
    for t in ob.data.loop_triangles:
        mat=ob.data.materials[ob.data.polygons[t.polygon_index].material_index]
        if not mat.name.startswith('FEB_Soldiers_FEB_Emblema'):continue
        a,b,c=[ob.matrix_world@ob.data.vertices[i].co for i in t.vertices]
        cross=(b-a).cross(c-a);triangles+=1
        if cross.length<1e-12:deg+=1
        if cross.dot(n)<=0:folds+=1
    checks.append({'name':ob.name,'FEB_patch_triangles':triangles,'degenerate':deg,'folded':folds})
assert len(meshes)==6 and all(x['FEB_patch_triangles']==1370 and x['degenerate']==0 and x['folded']==0 for x in checks)
fix['fbx_roundtrip']=checks;fix['fbx_roundtrip_passed']=True
fix['total_triangles']=report['triangles_total']
view=bpy.data.scenes['Conferencia_emblema_esquerdo']
test.world=view.world;test.render.engine='CYCLES';test.cycles.samples=32;test.cycles.use_denoising=True;test.view_settings.view_transform='AgX'
for ob in view.objects:
    if ob.type=='LIGHT':test.collection.objects.link(ob)
cam=view.camera.copy();cam.data=view.camera.data.copy();test.collection.objects.link(cam);test.camera=cam
test.render.resolution_x=1000;test.render.resolution_y=1000;test.render.resolution_percentage=100;test.render.image_settings.file_format='PNG';test.render.filepath=str(out/'Emblema_FEB_FBX_corrigido.png')
bpy.ops.render.render(write_still=True)
cam.matrix_world=s.camera.matrix_world.copy();cam.data.ortho_scale=8.7
test.render.resolution_x=2700;test.render.resolution_y=850;test.render.filepath=str(out/'FEB_Soldiers_exportado_conferido.png');bpy.ops.render.render(write_still=True)
(out/'left_patch_fix_validation.json').write_text(json.dumps(fix,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.context.window.scene=s
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result={'left_patches_verified':len(checks),'other_pieces_unchanged':len(fix['protected_objects']),'total_triangles':report['triangles_total'],'triangles':[v['triangles'] for v in report['variants']]}
