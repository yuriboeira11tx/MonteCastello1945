import bpy
from pathlib import Path
ROOT=Path('E:/Unity/MonteCastello1945');OUT=ROOT/'ArtSource/FEB_Soldier/BaseGLB';ASSET=ROOT/'Assets/Art/Characters/FEB_Soldier/BaseGLB'
scene=bpy.data.scenes['FEB_BaseGLB'];bpy.context.window.scene=scene
body=bpy.data.objects['FEB_GameBody'];rig=bpy.data.objects['FEB_Rig_BaseGLB'];patches=[bpy.data.objects[n] for n in ['FEB_Emblema_esquerdo','FEB_5Exercito_direito']]
for c in ['BaseColor','Normal','Metallic','Roughness','MetallicSmoothness_URP']:
    im=bpy.data.images['FEB_BaseGLB_'+c]
    if im.packed_file:im.unpack(method='REMOVE')
    im.reload();im.pack()
body.hide_set(False);bpy.ops.object.select_all(action='DESELECT')
for ob in [body,rig]+patches:ob.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(ASSET/'Models/FEB_Soldado_BaseGLB.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,mesh_smooth_type='FACE',path_mode='RELATIVE')
bpy.ops.export_scene.gltf(filepath=str(OUT/'FEB_Soldado_BaseGLB.glb'),export_format='GLB',use_selection=True,export_animations=False,export_extras=True)
body.hide_set(True);body.hide_render=True;bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FEB_Soldado_BaseGLB.blend'))
result={'refreshed_embedded_maps':True}
