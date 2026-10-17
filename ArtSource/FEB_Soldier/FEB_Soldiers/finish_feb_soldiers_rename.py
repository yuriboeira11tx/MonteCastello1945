import bpy,json,hashlib,sys,re
from pathlib import Path
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers');sys.path.insert(0,str(out))
import skin_utils as su
assets=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldiers')
baseline=json.loads((out/'rename_feb_soldiers_baseline.json').read_text())
def mapped(value):
 return value.replace('FEB_Rangers_Adaptados','FEB_Soldiers').replace('RangersFEB','FEB_Soldiers').replace('FEB_Rangers_american_rangers_0','FEB_Soldiers_Uniforme').replace('american_rangers_0_4X.png','FEB_Soldiers_Uniforme_4K.png').replace('FEB_Rangers','FEB_Soldiers').replace('FEB_Adaptados','FEB_Soldiers')
s=bpy.data.scenes.get('FEB_Adaptados') or bpy.data.scenes['FEB_Soldiers'];s.name='FEB_Soldiers'
material_changes={}
for mat in bpy.data.materials:
 if mat.name.startswith('FEB_Rangers'):
  old=mat.name;mat.name=mapped(old);material_changes[old]=mat.name
image_paths_changed=0
for im in bpy.data.images:
 old=im.filepath
 if 'FEB_Rangers' in old or 'RangersFEB' in old:
  absolute=bpy.path.abspath(old);new=mapped(absolute)
  if Path(new).is_file():im.filepath=new;image_paths_changed+=1
  elif im.source not in {'GENERATED','VIEWER'}:raise RuntimeError('Missing relocated image: '+new)
 if im.name.startswith('american_rangers_0_4X.png') and 'FEB_Soldiers/Textures' in im.filepath.replace('\\','/'):
  im.name=im.name.replace('american_rangers_0_4X.png','FEB_Soldiers_Uniforme_4K.png')
for scene in bpy.data.scenes:
 if scene.render.filepath:scene.render.filepath=mapped(scene.render.filepath)
for name,digest in baseline['meshes'].items():assert su.mesh_hash(bpy.data.objects[name])==digest,name
for rel,digest in baseline['textures'].items():assert hashlib.sha256((assets/mapped(rel)).read_bytes()).hexdigest()==digest,rel
def guid(text):return re.search(r'^guid: (.+)$',text,re.M).group(1)
assert guid(assets.with_suffix('.meta').read_text())==guid(baseline['folder_meta'])
for rel,content in baseline['unity_metas'].items():
 path=assets/mapped(rel);assert path.is_file(),str(path);assert guid(path.read_text())==guid(content),rel
report=json.loads((out/'export_report.json').read_text());ex=bpy.data.scenes['FEB_Exportacao_FBX'];bpy.context.window.scene=ex
opts=dict(use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='OFF',use_triangles=True,bake_anim=False,path_mode='RELATIVE',embed_textures=False,add_leaf_bones=False,use_custom_props=True)
for i in range(1,7):
 ob=bpy.data.objects['FEB_Soldado_%02d'%i];ob.data.calc_loop_triangles();assert len(ob.data.loop_triangles)==report['variants'][i-1]['triangles']
 bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
 loc=ob.location.copy();ob.location=(0,0,0);bpy.ops.export_scene.fbx(filepath=report['variants'][i-1]['file'],**opts);ob.location=loc
 report['variants'][i-1]['materials']=[m.name for m in ob.data.materials]
bpy.ops.object.select_all(action='DESELECT')
for i in range(1,7):bpy.data.objects['FEB_Soldado_%02d'%i].select_set(True)
bpy.ops.export_scene.fbx(filepath=report['combined_file'],**opts)
images={}
for i in range(1,7):
 for mat in bpy.data.objects['FEB_Soldado_%02d'%i].data.materials:
  for n in mat.node_tree.nodes:
   if n.type=='TEX_IMAGE' and n.image:
    im=n.image;p=Path(bpy.path.abspath(im.filepath));assert p.is_file(),str(p);assert 'FEB_Rangers' not in str(p)
    images[str(p)]={'file':str(p),'size':list(im.size)}
report['images']=list(images.values());report['name']='FEB_Soldiers'
(out/'export_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
# Verify the relocated texture links in the exported FBX.
test=bpy.data.scenes.new('Verificacao_nome_FEB_Soldiers');bpy.context.window.scene=test;bpy.ops.import_scene.fbx(filepath=report['combined_file'])
meshes=[o for o in test.objects if o.type=='MESH'];assert len(meshes)==6;texture_paths=set()
for ob in meshes:
 for mat in ob.data.materials:
  assert not mat.name.startswith('FEB_Rangers')
  for n in mat.node_tree.nodes:
   if n.type=='TEX_IMAGE' and n.image:
    path=Path(bpy.path.abspath(n.image.filepath));assert path.is_file();assert 'FEB_Rangers' not in str(path);texture_paths.add(str(path))
validation=dict(name='FEB_Soldiers',asset_folder=str(assets),blend=str(out/'FEB_Soldiers.blend'),meshes_unchanged=50,textures_unchanged=len(baseline['textures']),unity_guids_preserved=len(baseline['unity_metas'])+1,fbx_files_updated=7,reimported_models=6,resolved_texture_paths=len(texture_paths),materials_renamed=material_changes,image_paths_updated=image_paths_changed)
(out/'rename_feb_soldiers_validation.json').write_text(json.dumps(validation,indent=2))
bpy.context.window.scene=s;bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result={k:v for k,v in validation.items() if k!='materials_renamed'}
