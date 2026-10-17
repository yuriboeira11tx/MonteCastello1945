import bpy,json,math
from pathlib import Path
from mathutils import Vector
SRC=Path(__file__).resolve().parent
ASSET=SRC.parents[1]/'Assets/Art/Characters/FEB_Soldier/Realista'
checks={};report={}
bpy.ops.wm.open_mainfile(filepath=str(SRC/'FEB_Soldier_Realista.blend'))
rig=bpy.data.objects['FEB_Rig'];meshes=[o for o in rig.children if o.type=='MESH']
checks['source_22_bones']=len(rig.data.bones)==22
checks['source_uvs']=all(len(o.data.uv_layers)>0 for o in meshes)
checks['source_finite_geometry']=all(math.isfinite(c) for o in meshes for v in o.data.vertices for c in v.co)
checks['source_weighted']=all(v.groups for o in meshes for v in o.data.vertices)
checks['source_normalized_weights']=all(abs(sum(g.weight for g in v.groups)-1)<.005 for o in meshes for v in o.data.vertices)
checks['source_textures_packed']=all(im.packed_file for im in bpy.data.images if im.source=='FILE' and im.users>0)
checks['source_armature_bindings']=all(any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers) for o in meshes)
checks['horizontal_T_pose']=all(abs(rig.data.bones[n].head_local.z-rig.data.bones[n].tail_local.z)<.001 for n in ['UpperArm.L','UpperArm.R','Forearm.L','Forearm.R'])
body=next(o for o in meshes if 'Sarja' in o.name)
deps=bpy.context.evaluated_depsgraph_get();before=[v.co.copy() for v in body.evaluated_get(deps).data.vertices]
pb=rig.pose.bones['Forearm.L'];pb.rotation_mode='XYZ';pb.rotation_euler.z=.3;bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
after=[v.co.copy() for v in body.evaluated_get(deps).data.vertices]
checks['arm_deforms']=max((a-b).length for a,b in zip(after,before))>.015
pb.rotation_euler.z=0
for prefix,size in [('FEB_Uniforme',4096),('FEB_Rosto',2048),('FEB_Olhos',512)]:
    for channel in ['BaseColor','Normal','Roughness','Metallic','MetallicSmoothness_URP']:
        p=ASSET/'Textures'/(prefix+'_'+channel+'.png')
        if p.exists():
            im=bpy.data.images.load(str(p));checks[p.stem+'_size']=tuple(im.size)==(size,size)
        else:checks[p.stem+'_size']=False
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ASSET/'Models/FEB_Soldier_Realista.fbx'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
checks['fbx_five_meshes']=len(meshes)==5
checks['fbx_rig_22_bones']=len(rigs)==1 and len(rigs[0].data.bones)==22
checks['fbx_uvs']=all(o.data.uv_layers for o in meshes)
checks['fbx_weights']=all(v.groups for o in meshes for v in o.data.vertices)
coords=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
height=max(v.z for v in coords)-min(v.z for v in coords)
checks['fbx_scale_metres']=1.80<height<1.90
tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes)
checks['fbx_under_120k_triangles']=tris<120000
image_details=[]
for im in bpy.data.images:
    if im.source=='FILE' and im.users>0:
        im.reload()
        image_details.append({'name':im.name,'path':bpy.path.abspath(im.filepath),'size':list(im.size),'loaded':im.has_data})
checks['export_images_resolve']=all(x['size'][0]>0 and x['loaded'] for x in image_details)
report={'passed':all(checks.values()),'checks':checks,'fbx_triangles':tris,'fbx_height_m':height,'images':image_details}
(SRC/'validation_realista.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2));assert report['passed'],'Asset validation failed'
