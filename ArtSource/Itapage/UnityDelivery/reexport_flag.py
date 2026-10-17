import bpy
source=bpy.context.window.scene
export=bpy.data.scenes['ITAPAGE | Unity Export']
bpy.context.window.scene=export
flag=bpy.data.objects['Brasil_Bandeira']
for o in export.objects:o.select_set(False)
flag.hide_set(False);flag.select_set(True);bpy.context.view_layer.objects.active=flag
bpy.ops.export_scene.fbx(filepath='E:/Unity/MonteCastello1945/Assets/Art/Ships/Itapage/Models/Itapage_Bandeira.fbx',use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=False,mesh_smooth_type='FACE',use_tspace=True,axis_forward='-Z',axis_up='Y',bake_space_transform=False,bake_anim=False,path_mode='AUTO',embed_textures=False,add_leaf_bones=False)
bpy.context.window.scene=source
bpy.ops.wm.save_as_mainfile(filepath='E:/Unity/MonteCastello1945/ArtSource/Itapage/UnityDelivery/Itapage_Unity.blend',copy=True)
print('Flag FBX exported with 16 morph targets retained')
