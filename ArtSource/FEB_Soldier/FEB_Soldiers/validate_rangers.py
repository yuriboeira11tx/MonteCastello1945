import bpy,json,hashlib,math
from pathlib import Path
from mathutils import Vector,Matrix
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
report=json.loads((out/'export_report.json').read_text())
source_report=json.loads((out/'source_inspection.json').read_text())
adapt=bpy.data.scenes['FEB_Soldiers'];original=bpy.data.scenes['FEB_Soldiers']
checks={}
src=Path('C:/Users/yurib/Downloads/us-ranger-infantry-call-of-duty-2-hd/source/Rangers.fbx')
checks['original_fbx_unchanged']=hashlib.sha256(src.read_bytes()).hexdigest()==source_report['source_sha256']
checks['emblems_original_pngs']=all(report['reference_emblems_unchanged'].values())
checks['six_editable_variants']=all(bpy.data.collections.get('FEB_%02d'%i) for i in range(1,7))
checks['no_rig_in_supplied_fbx']=not any(o.type=='ARMATURE' for o in original.objects)
centers=[0,5.59,11.09,16.53,21.98,27.55];face_errors={}
for idx in range(1,7):
    for o in bpy.data.collections['FEB_%02d'%idx].objects:
        if o.type!='MESH':continue
        if not any(('head' in m.name or 'braebury' in m.name) for m in o.data.materials if m):continue
        source=bpy.data.objects[o['source_object']]
        transform=Matrix.Scale(.247,4)@Matrix.Translation(Vector((-centers[idx-1],0,0)))@source.matrix_world
        face_errors[o.name]=max((v.co-transform@sv.co).length for v,sv in zip(o.data.vertices,source.data.vertices)) if len(o.data.vertices)==len(source.data.vertices) else 999
checks['all_source_faces_preserved']=len(face_errors)>5 and max(face_errors.values())<.00001
# Import exported FBX in a dedicated scene, checking the actual delivered result.
test=bpy.data.scenes.new('Verificacao_FBX_Rangers');bpy.context.window.scene=test
bpy.ops.import_scene.fbx(filepath=report['combined_file'])
meshes=[o for o in test.objects if o.type=='MESH']
checks['fbx_has_six_soldiers']=len(meshes)==6
info=[]
for o in meshes:
    o.data.calc_loop_triangles()
    info.append({'name':o.name,'triangles':len(o.data.loop_triangles),'height_m':o.dimensions.z,'uv':bool(o.data.uv_layers),'finite':all(math.isfinite(c) for v in o.data.vertices for c in v.co),'materials':[m.name for m in o.data.materials]})
checks['fbx_triangle_count']=sum(x['triangles'] for x in info)==report['triangles_total']
checks['fbx_metre_scale']=all(1.7<x['height_m']<1.9 for x in info)
checks['fbx_has_UVs']=all(x['uv'] for x in info)
checks['fbx_no_invalid_coordinates']=all(x['finite'] for x in info)
checks['fbx_no_ranger_decals']=not any('ranger_markings' in n for x in info for n in x['materials'])
ims=set(n.image for o in meshes for m in o.data.materials if m and m.use_nodes for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image)
checks['fbx_textures_resolve']=all(len(im.pixels)>0 for im in ims)
checks['seven_export_files_exist']=all(Path(v['file']).exists() and Path(v['file']).stat().st_size>100000 for v in report['variants']) and Path(report['combined_file']).stat().st_size>100000
test.world=adapt.world;test.render.engine='CYCLES';test.cycles.samples=32;test.cycles.use_denoising=True
test.view_settings.view_transform='AgX'
for o in adapt.objects:
    if o.type=='LIGHT':test.collection.objects.link(o)
cam=adapt.camera.copy();cam.data=adapt.camera.data.copy();test.collection.objects.link(cam);test.camera=cam
cam.location=(0,-12,3.2);cam.rotation_euler=(Vector((0,0,.92))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=8.7
test.render.resolution_x=2700;test.render.resolution_y=850;test.render.resolution_percentage=100
test.render.image_settings.file_format='PNG';test.render.filepath=str(out/'FEB_Soldiers_exportado_conferido.png')
bpy.ops.render.render(write_still=True)
# Also confirm one individual FBX opens centered, independently of the group export.
single=bpy.data.scenes.new('Verificacao_FBX_individual');bpy.context.window.scene=single
bpy.ops.import_scene.fbx(filepath=report['variants'][2]['file'])
individual=[o for o in single.objects if o.type=='MESH']
checks['individual_fbx_centered']=len(individual)==1 and individual[0].location.length<.001
checks['individual_fbx_matches']=len(individual)==1 and len(individual[0].data.polygons)==report['variants'][2]['triangles']
validation={'passed':all(bool(v) for v in checks.values()),'checks':checks,'faces_max_coordinate_errors':face_errors,'roundtrip':info}
(out/'validation.json').write_text(json.dumps(validation,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.context.window.scene=adapt
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result=validation
