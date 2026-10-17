import bpy,json,math,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
O=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers/UnityDelivery')
for d in ['Models','Animations','Textures','Previews']:(O/d).mkdir(parents=True,exist_ok=True)
assert 'FEB_Unity_Export' not in bpy.data.scenes,'Export scene already exists; inspect before replacing.'
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
source=bpy.data.scenes['FEB_Soldiers_Rig'];s=bpy.data.scenes.new('FEB_Unity_Export');s.world=source.world;bpy.context.window.scene=s;s.render.fps=30
def select(o):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
def direct(a,name,direction):
 p=a.pose.bones[name];bpy.context.view_layer.update();q=p.matrix.to_quaternion();old=q@Vector((0,1,0));turn=old.rotation_difference(Vector(direction).normalized());p.matrix=Matrix.Translation(p.head)@(turn@q).to_matrix().to_4x4();bpy.context.view_layer.update()
def export(path,anim=False):
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,use_armature_deform_only=True,armature_nodetype='NULL',bake_anim=anim,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=True,mesh_smooth_type='FACE',path_mode='STRIP')
rigs=[];materials={};finger_rest={};rows=[]
for idx in range(1,7):
 src=bpy.data.objects[f'FEB_Soldado_{idx:02}_Rig'];coll=bpy.data.collections.new(f'FEB_Unity_{idx:02}');s.collection.children.link(coll)
 a=src.copy();a.data=src.data.copy();a.name=f'FEB_Soldado_{idx:02}_Armature';coll.objects.link(a);a.animation_data_clear();a.matrix_world=Matrix.Identity(4)
 for p in a.pose.bones:
  p.matrix_basis.identity();p.custom_shape=None
  for c in list(p.constraints):p.constraints.remove(c)
 finger_rest[str(idx)]={b.name:list((b.parent.matrix_local.inverted()@b.matrix_local).to_quaternion()) for b in src.data.bones if b.name.startswith(('thumb','index','middle','ring','pinky'))}
 copies=[]
 for ob in src.children:
  if ob.type!='MESH':continue
  n=ob.copy();n.data=ob.data.copy();n.name=f'Unity_{idx:02}_{ob.name}';coll.objects.link(n);n.parent=a;n.matrix_parent_inverse=Matrix.Identity(4);n.matrix_basis=Matrix.Identity(4)
  for m in n.modifiers:
   if m.type=='ARMATURE':m.object=a
  copies.append(n)
  for m in n.data.materials:
   if m.name in materials:continue
   bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');im=next(n.image for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image)
   filename=Path(bpy.path.abspath(im.filepath)).name
   if im.packed_file:data=bytes(im.packed_file.data)
   else:data=Path(bpy.path.abspath(im.filepath)).read_bytes()
   (O/'Textures'/filename).write_bytes(data)
   materials[m.name]={'name':m.name,'texture':filename,'roughness':float(bs.inputs['Roughness'].default_value),'metallic':float(bs.inputs['Metallic'].default_value),'alphaClip':bool(bs.inputs['Alpha'].is_linked),'sha256':hashlib.sha256(data).hexdigest()}
 bpy.context.view_layer.update()
 for side,sign in [('L',1),('R',-1)]:
  for name in ['thigh','shin']:direct(a,f'{name}.{side}',(0,0,-1))
  for name in ['upper_arm','forearm','hand']:direct(a,f'{name}.{side}',(sign,0,0))
  for digit in ['index','middle','ring','pinky','thumb']:
   for k in range(1,4):direct(a,f'{digit}_{k:02}.{side}',(sign,0,0) if digit!='thumb' else (sign*.8,-.6,0))
 for ob in copies:
  select(ob)
  for m in list(ob.modifiers):
   if m.type=='ARMATURE':bpy.ops.object.modifier_apply(modifier=m.name)
 select(a);bpy.ops.object.mode_set(mode='POSE');bpy.ops.pose.armature_apply(selected=False);bpy.ops.object.mode_set(mode='OBJECT')
 # Bake the T-pose into a dedicated export copy, keeping the authored source rig.
 minimum=min(v.co.z for ob in copies for v in ob.data.vertices);dz=-minimum
 for ob in copies:
  for v in ob.data.vertices:v.co.z+=dz
 select(a);bpy.ops.object.mode_set(mode='EDIT')
 for b in list(a.data.edit_bones):
  if not b.use_deform and b.name!='CTRL_root':a.data.edit_bones.remove(b)
 for b in a.data.edit_bones:b.head.z+=dz;b.tail.z+=dz
 a.data.edit_bones['CTRL_root'].name='Root';a.data.edit_bones['Root'].use_deform=True
 bpy.ops.object.mode_set(mode='OBJECT')
 for c in a.data.collections:c.is_visible=True
 for b in a.data.bones:b.hide=False
 bpy.ops.object.select_all(action='DESELECT')
 for ob in copies:ob.select_set(True)
 bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();mesh=bpy.context.object;mesh.name=f'FEB_Soldado_{idx:02}_Mesh';mesh.data.name=mesh.name
 for g in list(mesh.vertex_groups):
  if g.name not in a.data.bones:mesh.vertex_groups.remove(g)
 mod=mesh.modifiers.new('FEB_Skin','ARMATURE');mod.object=a;mod.use_deform_preserve_volume=False
 select(a);mesh.select_set(True);bpy.context.view_layer.update();export(O/'Models'/f'FEB_Soldado_{idx:02}.fbx')
 a['export_model']=True;a['soldier_index']=idx
 rows.append({'id':idx,'armature':a.name,'mesh':mesh.name,'bones':len(a.data.bones),'vertices':len(mesh.data.vertices),'triangles':sum(len(p.vertices)-2 for p in mesh.data.polygons),'materials':[m.name for m in mesh.data.materials],'height':max(v.co.z for v in mesh.data.vertices),'ground_offset':dz})
 rigs.append(a);a.location.x=(idx-3.5)*1.85
(O/'materials_manifest.json').write_text(json.dumps({'materials':list(materials.values())},indent=2))
(O/'finger_rest.json').write_text(json.dumps(finger_rest,indent=2))
(O/'models_manifest.json').write_text(json.dumps({'models':rows},indent=2))
for ob in source.objects:
 if ob.type=='LIGHT':s.collection.objects.link(ob)
cam=source.camera.copy();cam.data=source.camera.data.copy();cam.name='Camera_Unity_Export';s.collection.objects.link(cam);s.camera=cam;cam.location=(0,-15,2.4);target=Vector((0,0,.95));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=11.7
s.render.engine='CYCLES';s.cycles.samples=24;s.cycles.use_denoising=True;s.view_settings.view_transform='AgX';s.render.resolution_x=2600;s.render.resolution_y=700;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.render.filepath=str(O/'Previews'/'FEB_TPose_Export.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(O/'FEB_Soldiers_Unity.blend'))
result={'models':rows,'material_count':len(materials),'output':str(O)}
