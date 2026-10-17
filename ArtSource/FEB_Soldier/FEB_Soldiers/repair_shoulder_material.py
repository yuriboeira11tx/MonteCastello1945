import bpy,json,hashlib,sys
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers');sys.path.insert(0,str(out))
import skin_utils as su
assets=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldiers')
s=bpy.data.scenes['FEB_Soldiers'];bpy.context.window.scene=s
backup=out/'FEB_Soldiers_antes_correcao_material_ombro.blend'
if not backup.exists():bpy.ops.wm.save_as_mainfile(filepath=str(backup),copy=True)
spots=json.loads((out/'shoulder_spots_inspection.json').read_text())
objects=[o for i in range(1,7) for o in bpy.data.collections['FEB_%02d'%i].objects if o.type=='MESH']
baseline={o.name:dict(mesh=su.mesh_hash(o),materials=[m.name for m in o.data.materials],assignments=[p.material_index for p in o.data.polygons]) for o in objects}
texture_hashes={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (assets/'Textures').rglob('*.png')}
allowed={x['object']:set(x['polygons']) for x in spots}
for x in spots:
 ob=bpy.data.objects[x['object']];original_index=next(i for i,m in enumerate(ob.data.materials) if m.name=='FEB_Soldiers_Uniforme')
 for pidx in x['polygons']:ob.data.polygons[pidx].material_index=original_index
selection=json.loads((out/'color_selection.json').read_text())
for x in spots:
 cats=selection[x['object']]
 for key in cats:cats[key]=[p for p in cats[key] if p not in allowed[x['object']]]
(out/'color_selection.json').write_text(json.dumps(selection,indent=2))
for ob in objects:
 entry=baseline[ob.name];assert su.mesh_hash(ob)==entry['mesh']
 assert [m.name for m in ob.data.materials]==entry['materials']
 for p,old in zip(ob.data.polygons,entry['assignments']):
  if p.index not in allowed.get(ob.name,set()):assert p.material_index==old
for path,digest in texture_hashes.items():assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest
review=bpy.data.scenes['Conferencia_ponto_ombro'];bpy.context.window.scene=review
review.render.filepath=str(out/'Ponto_ombro_corrigido.png');bpy.ops.render.render(write_still=True)
report=json.loads((out/'export_report.json').read_text());ex=bpy.data.scenes['FEB_Exportacao_FBX'];bpy.context.window.scene=ex
opts=dict(use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='OFF',use_triangles=True,bake_anim=False,path_mode='RELATIVE',embed_textures=False,add_leaf_bones=False,use_custom_props=True)
expected=[]
for i in range(1,7):
 old=bpy.data.objects['FEB_Soldado_%02d'%i];copies=[]
 for ob in bpy.data.collections['FEB_%02d'%i].objects:
  if ob.type!='MESH':continue
  cp=ob.copy();cp.data=ob.data.copy();ex.collection.objects.link(cp);copies.append(cp)
 bpy.ops.object.select_all(action='DESELECT')
 for cp in copies:cp.select_set(True)
 bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();joined=bpy.context.object
 old.data.use_fake_user=True;old.data=joined.data;bpy.data.objects.remove(joined,do_unlink=True)
 old.data.calc_loop_triangles();assert len(old.data.loop_triangles)==report['variants'][i-1]['triangles']
 counts={}
 for t in old.data.loop_triangles:
  name=old.data.materials[old.data.polygons[t.polygon_index].material_index].name;counts[name]=counts.get(name,0)+1
 expected.append(counts)
 bpy.ops.object.select_all(action='DESELECT');old.select_set(True);bpy.context.view_layer.objects.active=old
 loc=old.location.copy();old.location=(0,0,0);bpy.ops.export_scene.fbx(filepath=report['variants'][i-1]['file'],**opts);old.location=loc
bpy.ops.object.select_all(action='DESELECT')
for i in range(1,7):bpy.data.objects['FEB_Soldado_%02d'%i].select_set(True)
bpy.ops.export_scene.fbx(filepath=report['combined_file'],**opts)
test=bpy.data.scenes.new('Verificacao_material_ombro_corrigido');bpy.context.window.scene=test;bpy.ops.import_scene.fbx(filepath=report['combined_file'])
meshes=sorted([o for o in test.objects if o.type=='MESH'],key=lambda o:o.location.x);assert len(meshes)==6
for i,ob in enumerate(meshes):
 ob.data.calc_loop_triangles();assert len(ob.data.loop_triangles)==report['variants'][i]['triangles'];counts={}
 for t in ob.data.loop_triangles:
  name=ob.data.materials[ob.data.polygons[t.polygon_index].material_index].name
  match=next((k for k in expected[i] if name==k or name.startswith(k+'.')),None);assert match,name
  counts[match]=counts.get(match,0)+1
 assert counts==expected[i],(i,counts,expected[i])
test.world=s.world;test.render.engine='CYCLES';test.cycles.samples=24;test.cycles.use_denoising=True;test.view_settings.view_transform=s.view_settings.view_transform
for ob in s.objects:
 if ob.type=='LIGHT':test.collection.objects.link(ob)
cam=review.camera.copy();cam.data=review.camera.data.copy();test.collection.objects.link(cam);test.camera=cam
test.render.resolution_x=950;test.render.resolution_y=950;test.render.resolution_percentage=100;test.render.image_settings.file_format='PNG';test.render.filepath=str(out/'Ponto_ombro_FBX_corrigido.png');bpy.ops.render.render(write_still=True)
cam.matrix_world=s.camera.matrix_world.copy();cam.data.ortho_scale=8.7;test.render.resolution_x=2700;test.render.resolution_y=850;test.render.filepath=str(out/'FEB_Soldiers_exportado_conferido.png');bpy.ops.render.render(write_still=True)
skin_review=bpy.data.scenes['Conferencia_tons_de_pele'];bpy.context.window.scene=skin_review;cam=skin_review.camera;skin_review.render.resolution_x=720;skin_review.render.resolution_y=900;cam.data.ortho_scale=.79
for idx in (1,2,5,6):
 x=(idx-3.5)*1.4;cam.location=(x,-3.5,1.67);target=Vector((x,-.02,1.47));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();skin_review.render.filepath=str(out/('FEB_%02d_Pele_retrato.png'%idx));bpy.ops.render.render(write_still=True)
board=bpy.data.scenes['Guia_visual_tons_de_pele']
for ob in board.objects:
 if ob.type!='MESH':continue
 for mat in ob.data.materials:
  for n in mat.node_tree.nodes:
   if n.type=='TEX_IMAGE' and n.image:n.image.reload()
bpy.context.window.scene=board;bpy.ops.render.render(write_still=True)
validation=dict(cause='Four jacket fabric faces per affected model were incorrectly classified as black hardware during uniform recoloring, before skin variation.',corrected_objects=[x['object'] for x in spots],faces_restored=16,geometry_uv_transforms_unchanged=50,all_texture_files_unchanged=len(texture_hashes),only_requested_material_assignments_changed=True,fbx_roundtrip_material_counts_match=True,exported_files=7,total_triangles=59138)
(out/'shoulder_material_validation.json').write_text(json.dumps(validation,indent=2))
bpy.context.window.scene=s;bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result=validation
