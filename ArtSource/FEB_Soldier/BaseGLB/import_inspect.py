import bpy
import json
from pathlib import Path
from mathutils import Vector

out = Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/BaseGLB')
out.mkdir(exist_ok=True)
backup = out / 'Sessao_anterior_preservada.blend'
if not backup.exists():
    bpy.ops.wm.save_as_mainfile(filepath=str(backup), copy=True)
scene = bpy.data.scenes.new('FEB_BaseGLB')
bpy.context.window.scene = scene
bpy.ops.import_scene.gltf(filepath='C:/Users/yurib/Downloads/call_of_duty_1_soldier_rig.glb')
bpy.context.view_layer.update()

def bounds(obj):
    pts = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
    return [[min(v[i] for v in pts) for i in range(3)], [max(v[i] for v in pts) for i in range(3)]]

report = {'scene':scene.name, 'objects':[], 'images':[], 'bones':[]}
for obj in scene.objects:
    info = {'name':obj.name,'type':obj.type,'parent':obj.parent.name if obj.parent else None}
    if obj.type == 'MESH':
        obj.data.calc_loop_triangles()
        info.update(bounds=bounds(obj),vertices=len(obj.data.vertices),triangles=len(obj.data.loop_triangles),materials=[m.name for m in obj.data.materials],uvs=[u.name for u in obj.data.uv_layers])
    if obj.type == 'ARMATURE':
        for bone in obj.data.bones:
            report['bones'].append({'name':bone.name,'parent':bone.parent.name if bone.parent else None,'head':list(obj.matrix_world@bone.head_local),'tail':list(obj.matrix_world@bone.tail_local)})
    report['objects'].append(info)
for img in bpy.data.images:
    if img.type == 'IMAGE':
        path = out / 'OriginalTextures' / (img.name.replace('/','_')+'.png')
        path.parent.mkdir(exist_ok=True)
        img.filepath_raw = str(path)
        img.file_format = 'PNG'
        img.save()
        report['images'].append({'name':img.name,'size':list(img.size),'path':str(path)})
(out/'inspection.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Fonte_importada.blend'))
result = report
