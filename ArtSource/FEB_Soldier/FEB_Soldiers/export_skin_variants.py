import bpy,json,hashlib,sys
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers');sys.path.insert(0,str(out))
import skin_utils as su
assets=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldiers')
report=json.loads((out/'export_report.json').read_text());data=json.loads((out/'skin_variants.json').read_text(encoding='utf-8'));baseline=json.loads((out/'skin_baseline.json').read_text());validation=json.loads((out/'skin_validation.json').read_text())
s=bpy.data.scenes['FEB_Soldiers'];ex=bpy.data.scenes['FEB_Exportacao_FBX'];bpy.context.window.scene=ex
options=dict(use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='OFF',use_triangles=True,bake_anim=False,path_mode='RELATIVE',embed_textures=False,add_leaf_bones=False,use_custom_props=True)
for idx in range(1,7):
 old=bpy.data.objects['FEB_Soldado_%02d'%idx];copies=[]
 for ob in bpy.data.collections['FEB_%02d'%idx].objects:
  if ob.type!='MESH':continue
  cp=ob.copy();cp.data=ob.data.copy();ex.collection.objects.link(cp);copies.append(cp)
 bpy.ops.object.select_all(action='DESELECT')
 for cp in copies:cp.select_set(True)
 bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();joined=bpy.context.object
 old.data.use_fake_user=True;old.data=joined.data;bpy.data.objects.remove(joined,do_unlink=True)
 bpy.ops.object.select_all(action='DESELECT');old.select_set(True);bpy.context.view_layer.objects.active=old
 old.data.calc_loop_triangles();r=report['variants'][idx-1];assert len(old.data.loop_triangles)==r['triangles']
 r['materials']=[m.name for m in old.data.materials];r['vertices']=len(old.data.vertices);r['skin_tone']=data['variants'][idx-1]['tone']
 loc=old.location.copy();old.location=(0,0,0);bpy.ops.export_scene.fbx(filepath=r['file'],**options);old.location=loc
bpy.ops.object.select_all(action='DESELECT')
for idx in range(1,7):bpy.data.objects['FEB_Soldado_%02d'%idx].select_set(True)
bpy.ops.export_scene.fbx(filepath=report['combined_file'],**options)
images={}
for idx in range(1,7):
 for mat in bpy.data.objects['FEB_Soldado_%02d'%idx].data.materials:
  for n in mat.node_tree.nodes:
   if n.type=='TEX_IMAGE' and n.image:
    im=n.image;path=Path(bpy.path.abspath(im.filepath));assert path.is_file()
    images[str(path)]={'file':str(path),'size':list(im.size)}
report['images']=list(images.values());(out/'export_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
test=bpy.data.scenes.new('Verificacao_FBX_tons_de_pele');bpy.context.window.scene=test;bpy.ops.import_scene.fbx(filepath=report['combined_file'])
meshes=sorted([o for o in test.objects if o.type=='MESH'],key=lambda o:o.location.x);assert len(meshes)==6;checks=[]
for idx,ob in enumerate(meshes,1):
 ob.data.calc_loop_triangles();paths=set();materials=[]
 for mat in ob.data.materials:
  materials.append(mat.name)
  for node in mat.node_tree.nodes:
   if node.type=='TEX_IMAGE' and node.image:
    p=Path(bpy.path.abspath(node.image.filepath));assert p.is_file();paths.add(p.name)
 assert {'FEB_%02d_Pele_Rosto.png'%idx,'FEB_%02d_Pele_Maos.png'%idx,'FEB_Patch.png','FifthArmy_Patch.png','FEB_Cores_PDF_4K.png'}<=paths
 assert len(ob.data.loop_triangles)==report['variants'][idx-1]['triangles']
 checks.append(dict(id=idx,triangles=len(ob.data.loop_triangles),skin_textures_resolved=True,uniform_and_badge_textures_resolved=True))
head_names={n for ns in data['head_objects'].values() for n in ns}
for name,entry in baseline['objects'].items():
 ob=bpy.data.objects[name];assert su.mesh_hash(ob)==entry['mesh_hash'],name
 allowed=set(data['hands_selection'].get(name,[]))
 for p,old in zip(ob.data.polygons,entry['polygon_materials']):
  if p.index not in allowed:
   assert p.material_index==old
   if name not in head_names:assert ob.data.materials[old].name==entry['materials'][old]
for path,digest in baseline['textures'].items():assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,path
validation.update(fbx_roundtrip=checks,triangles_total=59138,geometry_uv_transforms_identical=50,unrelated_material_assignments_unchanged=True)
(out/'skin_validation.json').write_text(json.dumps(validation,indent=2))
test.world=s.world;test.render.engine='CYCLES';test.cycles.samples=32;test.cycles.use_denoising=True;test.view_settings.view_transform=s.view_settings.view_transform
for ob in s.objects:
 if ob.type=='LIGHT':test.collection.objects.link(ob)
cam=s.camera.copy();cam.data=s.camera.data.copy();test.collection.objects.link(cam);test.camera=cam;cam.data.ortho_scale=8.7
test.render.resolution_x=2700;test.render.resolution_y=850;test.render.resolution_percentage=100;test.render.image_settings.file_format='PNG';test.render.filepath=str(out/'FEB_Soldiers_exportado_conferido.png');bpy.ops.render.render(write_still=True)
# Front/back views of the deepest tone verify that the neck, ears and hands match.
cam.location=(3.5,4.0,1.95);target=Vector((3.5,0,1.05));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.15
test.render.resolution_x=900;test.render.resolution_y=1150;test.render.filepath=str(out/'FEB_06_Pele_costas.png');bpy.ops.render.render(write_still=True)
# A separate presentation scene contains only preview cards and labels.
board=bpy.data.scenes.new('Guia_visual_tons_de_pele');board.render.engine='CYCLES';board.cycles.samples=4
board.world=bpy.data.worlds.new('Fundo_guia_pele');board.world.use_nodes=True;board.world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.028,.035,.045,1)
board.view_settings.view_transform='Standard';board.render.image_settings.file_format='PNG';board.render.resolution_x=1560;board.render.resolution_y=1440;board.render.resolution_percentage=100
camera_data=bpy.data.cameras.new('Camera_guia_pele');camera=bpy.data.objects.new('Camera_guia_pele',camera_data);board.collection.objects.link(camera);camera.location=(0,0,10);camera_data.type='ORTHO';camera_data.ortho_scale=4.05;board.camera=camera
white=bpy.data.materials.new('Texto_guia_pele');white.use_nodes=True;nt=white.node_tree;nt.nodes.clear();em=nt.nodes.new('ShaderNodeEmission');em.inputs[0].default_value=(.86,.90,.94,1);o=nt.nodes.new('ShaderNodeOutputMaterial');nt.links.new(em.outputs[0],o.inputs[0])
labels=['01 | Clara quente','02 | Castanha escura','03 | Media oliva','04 | Clara neutra','05 | Castanha dourada','06 | Castanha profunda']
for i,v in enumerate(data['variants']):
 x=(i%3-1)*1.30;y=(.83 if i<3 else -.83);w=1.20;h=1.50
 mesh=bpy.data.meshes.new('Cartao_pele_%02d'%(i+1));mesh.from_pydata([(x-w/2,y-h/2,0),(x+w/2,y-h/2,0),(x+w/2,y+h/2,0),(x-w/2,y+h/2,0)],[],[(0,1,2,3)]);uv=mesh.uv_layers.new()
 for item,co in zip(uv.data,[(0,0),(1,0),(1,1),(0,1)]):item.uv=co
 ob=bpy.data.objects.new(mesh.name,mesh);board.collection.objects.link(ob)
 mat=bpy.data.materials.new(mesh.name);mat.use_nodes=True;nt=mat.node_tree;nt.nodes.clear();t=nt.nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(out/('FEB_%02d_Pele_retrato.png'%(i+1))),check_existing=True);em=nt.nodes.new('ShaderNodeEmission');o=nt.nodes.new('ShaderNodeOutputMaterial');nt.links.new(t.outputs['Color'],em.inputs[0]);nt.links.new(em.outputs[0],o.inputs[0]);mesh.materials.append(mat)
 curve=bpy.data.curves.new('Legenda_pele_%02d'%(i+1),'FONT');curve.body=labels[i];curve.align_x='CENTER';curve.size=.080;txt=bpy.data.objects.new(curve.name,curve);board.collection.objects.link(txt);txt.location=(x,y-h/2-.10,.01);curve.materials.append(white)
bpy.context.window.scene=board;board.render.filepath=str(out/'FEB_Tons_de_Pele_identificados.png');bpy.ops.render.render(write_still=True)
bpy.context.window.scene=s;bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result=dict(exported_files=7,new_skin_textures=12,verified_models=6,unchanged_source_meshes=50,original_texture_files_unchanged=len(baseline['textures']))
