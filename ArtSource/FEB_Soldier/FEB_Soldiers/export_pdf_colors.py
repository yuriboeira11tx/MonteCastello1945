import bpy,json,hashlib,struct
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
assets=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldiers')
report=json.loads((out/'export_report.json').read_text())
validation=json.loads((out/'colors_validation.json').read_text())
baseline=json.loads((out/'colors_baseline.json').read_text())
selection=json.loads((out/'color_selection.json').read_text())
s=bpy.data.scenes['FEB_Soldiers'];ex=bpy.data.scenes['FEB_Exportacao_FBX']
bpy.context.window.scene=ex
options=dict(use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='OFF',use_triangles=True,bake_anim=False,path_mode='RELATIVE',embed_textures=False,add_leaf_bones=False,use_custom_props=True)
for idx in range(1,7):
 old=bpy.data.objects['FEB_Soldado_%02d'%idx];copies=[]
 for ob in bpy.data.collections['FEB_%02d'%idx].objects:
  if ob.type!='MESH':continue
  cp=ob.copy();cp.data=ob.data.copy();ex.collection.objects.link(cp);copies.append(cp)
 bpy.ops.object.select_all(action='DESELECT')
 for cp in copies:cp.select_set(True)
 bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();joined=bpy.context.object
 old.data.use_fake_user=True;old.data=joined.data
 bpy.data.objects.remove(joined,do_unlink=True)
 bpy.ops.object.select_all(action='DESELECT');old.select_set(True);bpy.context.view_layer.objects.active=old
 old.data.calc_loop_triangles();r=report['variants'][idx-1]
 assert len(old.data.loop_triangles)==r['triangles']
 r['materials']=[m.name for m in old.data.materials];r['vertices']=len(old.data.vertices)
 old_location=old.location.copy();old.location=(0,0,0)
 bpy.ops.export_scene.fbx(filepath=r['file'],**options);old.location=old_location
bpy.ops.object.select_all(action='DESELECT')
for idx in range(1,7):bpy.data.objects['FEB_Soldado_%02d'%idx].select_set(True)
bpy.ops.export_scene.fbx(filepath=report['combined_file'],**options)
images={}
for idx in range(1,7):
 for mat in bpy.data.objects['FEB_Soldado_%02d'%idx].data.materials:
  for node in mat.node_tree.nodes:
   if node.type=='TEX_IMAGE' and node.image:
    im=node.image;path=Path(bpy.path.abspath(im.filepath));assert path.is_file(),str(path)
    images[str(path)]={'file':str(path),'size':list(im.size)}
report['images']=list(images.values());report['color_reference']='feb-textura.pdf: calca verde-FEB, botas pretas, suspensorios caqui-esverdeado com ferragens pretas.'
(out/'export_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
test=bpy.data.scenes.new('Verificacao_FBX_cores_PDF');bpy.context.window.scene=test
bpy.ops.import_scene.fbx(filepath=report['combined_file'])
meshes=[o for o in test.objects if o.type=='MESH'];assert len(meshes)==6
checks=[];n=Vector((.86,-.12,.5)).normalized()
for ob in sorted(meshes,key=lambda o:o.location.x):
 ob.data.calc_loop_triangles();patch=0;folded=0;color_faces=0;texture_paths=[]
 for mat in ob.data.materials:
  for node in mat.node_tree.nodes:
   if node.type=='TEX_IMAGE' and node.image:
    path=Path(bpy.path.abspath(node.image.filepath));assert path.is_file();texture_paths.append(str(path))
 for t in ob.data.loop_triangles:
  mat=ob.data.materials[ob.data.polygons[t.polygon_index].material_index]
  if mat.name.startswith('FEB_Soldiers_Cores_PDF'):color_faces+=1
  if not mat.name.startswith('FEB_Soldiers_FEB_Emblema'):continue
  patch+=1;a,b,c=[ob.matrix_world@ob.data.vertices[i].co for i in t.vertices]
  if (b-a).cross(c-a).dot(n)<=0:folded+=1
 assert color_faces>1000 and patch==1370 and folded==0
 assert any(Path(p).name=='FEB_Cores_PDF_4K.png' for p in texture_paths)
 checks.append(dict(name=ob.name,triangles=len(ob.data.loop_triangles),colored_triangles=color_faces,FEB_patch_triangles=patch,folded_patch_triangles=folded,textures_resolved=True))
assert sum(c['triangles'] for c in checks)==59138
for name,entry in baseline['objects'].items():
 ob=bpy.data.objects[name];me=ob.data;digest=hashlib.sha256()
 for v in me.vertices:digest.update(struct.pack('3f',*v.co))
 for p in me.polygons:digest.update(struct.pack('%di'%len(p.vertices),*p.vertices))
 for uv in me.uv_layers:
  for d in uv.data:digest.update(struct.pack('2f',*d.uv))
 for row in ob.matrix_world:digest.update(struct.pack('4f',*row))
 assert digest.hexdigest()==entry['geometry_uv_transform'],name
 allowed=set(p for ids in selection.get(name,{}).values() for p in ids)
 for p,old in zip(me.polygons,entry['polygon_materials']):
  if p.index not in allowed:assert p.material_index==old
for name,digest in baseline['textures'].items():assert hashlib.sha256((assets/'Textures'/name).read_bytes()).hexdigest()==digest,name
validation['fbx_roundtrip']=checks;validation['total_triangles_unchanged']=59138;validation['export_files']=7
test.world=s.world;test.render.engine='CYCLES';test.cycles.samples=32;test.cycles.use_denoising=True;test.view_settings.view_transform=s.view_settings.view_transform
for ob in s.objects:
 if ob.type=='LIGHT':test.collection.objects.link(ob)
cam=s.camera.copy();cam.data=s.camera.data.copy();test.collection.objects.link(cam);test.camera=cam;cam.data.ortho_scale=8.7
test.render.resolution_x=2700;test.render.resolution_y=850;test.render.resolution_percentage=100;test.render.image_settings.file_format='PNG'
test.render.filepath=str(out/'FEB_Soldiers_exportado_conferido.png');bpy.ops.render.render(write_still=True)
(out/'colors_validation.json').write_text(json.dumps(validation,indent=2))
bpy.context.window.scene=s
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result={'files_exported':7,'models_verified':6,'geometry_uv_transforms_unchanged':len(baseline['objects']),'original_textures_unchanged':len(baseline['textures']),'triangles':59138,'new_texture_resolved':True}
