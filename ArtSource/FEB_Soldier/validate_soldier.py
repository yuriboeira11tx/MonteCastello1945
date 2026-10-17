import bpy, json, math
from pathlib import Path
from mathutils import Vector
SRC=Path(__file__).resolve().parent
ROOT=SRC.parents[1]
bpy.ops.wm.open_mainfile(filepath=str(SRC/'FEB_Soldier_TPose.blend'))
collection=bpy.data.collections['FEB_Soldier']
meshes=[o for o in collection.objects if o.type=='MESH']
rig=next(o for o in collection.objects if o.type=='ARMATURE')
checks={}
checks['three_meshes']=len(meshes)==3
checks['22_bones']=len(rig.data.bones)==22
checks['all_meshes_have_uv']=all(o.data.uv_layers for o in meshes)
checks['all_meshes_bound']=all(any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers) for o in meshes)
checks['no_unweighted_vertices']=all(any(g.weight>0 for g in v.groups) for o in meshes for v in o.data.vertices)
checks['normalized_weights']=all(abs(sum(g.weight for g in v.groups)-1)<.002 for o in meshes for v in o.data.vertices)
checks['finite_geometry']=all(math.isfinite(c) for o in meshes for v in o.data.vertices for c in v.co)
checks['packed_textures']=all(i.packed_file is not None for i in bpy.data.images if i.source=='FILE')
checks['T_pose_horizontal_arms']=all(abs(rig.data.bones[n].head_local.z-rig.data.bones[n].tail_local.z)<.001 for n in ['UpperArm.L','UpperArm.R','Forearm.L','Forearm.R'])
tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes)
checks['under_80k_triangles']=tris<80000
# Verify that skinning actually changes the evaluated mesh.
body=next(o for o in meshes if o.name=='FEB_Uniforme_Corpo_Equipamento')
deps=bpy.context.evaluated_depsgraph_get();before=body.evaluated_get(deps).to_mesh()
before_pos=[v.co.copy() for v in before.vertices];body.evaluated_get(deps).to_mesh_clear()
rig.pose.bones['Forearm.L'].rotation_mode='XYZ';rig.pose.bones['Forearm.L'].rotation_euler.y=.4
bpy.context.view_layer.update();after=body.evaluated_get(deps).to_mesh()
checks['armature_deforms_mesh']=any((v.co-before_pos[i]).length>.01 for i,v in enumerate(after.vertices))
body.evaluated_get(deps).to_mesh_clear()
rig.pose.bones['Forearm.L'].rotation_euler.y=0
# Independently reimport exported FBX, validate geometry, scale and skeleton.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Art/Characters/FEB_Soldier/Models/FEB_Soldier_TPose.fbx'))
fbx_meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
fbx_rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
checks['fbx_three_meshes']=len(fbx_meshes)==3
checks['fbx_skeleton']=len(fbx_rigs)==1 and len(fbx_rigs[0].data.bones)==22
zs=[(o.matrix_world@v.co).z for o in fbx_meshes for v in o.data.vertices]
checks['fbx_metres']=1.8<max(zs)-min(zs)<1.9
report={'passed':all(checks.values()),'checks':checks,'triangles':tris,'fbx_height_metres':max(zs)-min(zs)}
(SRC/'validation.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
assert report['passed'], 'Character validation failed'
