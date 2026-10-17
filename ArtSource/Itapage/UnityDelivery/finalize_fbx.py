import bpy
scene=bpy.context.window.scene
export=bpy.data.scenes['ITAPAGE | Unity Export'];bpy.context.window.scene=export
for lod in range(3):
    ob=bpy.data.objects.get('Itapage_Estrutura_LOD%d'%lod) or bpy.data.objects['Itapage_Detalhe%d'%lod]
    ob.name='Itapage_Detalhe%d'%lod;ob.data.name='Itapage_Malha_Detalhe%d'%lod
    for o in export.objects:o.select_set(False)
    ob.hide_set(False);ob.select_set(True);bpy.context.view_layer.objects.active=ob
    tri=ob.modifiers.new('Triangulacao de entrega','TRIANGULATE');tri.quad_method='BEAUTY';tri.ngon_method='BEAUTY'
    bpy.ops.object.modifier_apply(modifier=tri.name)
    bpy.ops.export_scene.fbx(filepath='E:/Unity/MonteCastello1945/Assets/Art/Ships/Itapage/Models/Itapage_LOD%d.fbx'%lod,use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=False,axis_forward='-Z',axis_up='Y',bake_space_transform=False,bake_anim=False,path_mode='AUTO',embed_textures=False,add_leaf_bones=False)
    ob.hide_set(True)
bpy.context.window.scene=scene
bpy.ops.wm.save_as_mainfile(filepath='E:/Unity/MonteCastello1945/ArtSource/Itapage/UnityDelivery/Itapage_Unity.blend',copy=True)
print('FBX objects renamed: manual Unity LODGroup now owns all transitions')
