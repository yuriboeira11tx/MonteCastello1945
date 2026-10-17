import bpy, json, hashlib
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
out.mkdir(parents=True,exist_ok=True)
backup=out/'Sessao_anterior_preservada.blend'
if not backup.exists(): bpy.ops.wm.save_as_mainfile(filepath=str(backup),copy=True)
scene=bpy.data.scenes.new('FEB_Soldiers')
bpy.context.window.scene=scene
src=Path('C:/Users/yurib/Downloads/us-ranger-infantry-call-of-duty-2-hd/source/Rangers.fbx')
bpy.ops.import_scene.fbx(filepath=str(src))
bpy.context.view_layer.update()
def bounds(o):
    pts=[o.matrix_world@Vector(v) for v in o.bound_box]
    return [[min(p[i] for p in pts) for i in range(3)],[max(p[i] for p in pts) for i in range(3)]]
report={'source_sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'objects':[],'images':[],'materials':[]}
for o in scene.objects:
    r={'name':o.name,'type':o.type,'parent':o.parent.name if o.parent else None,'location':list(o.location),'scale':list(o.scale)}
    if o.type=='MESH':
        o.data.calc_loop_triangles()
        r.update(vertices=len(o.data.vertices),triangles=len(o.data.loop_triangles),bounds=bounds(o),materials=[m.name if m else None for m in o.data.materials],groups=len(o.vertex_groups),uvs=[u.name for u in o.data.uv_layers],modifiers=[(m.type,m.object.name if hasattr(m,'object') and m.object else None) for m in o.modifiers])
    if o.type=='ARMATURE':r['bones']=[b.name for b in o.data.bones]
    report['objects'].append(r)
for im in bpy.data.images: report['images'].append({'name':im.name,'path':im.filepath,'size':list(im.size)})
for m in bpy.data.materials:
    report['materials'].append({'name':m.name,'nodes':[(n.type,n.image.name if n.type=='TEX_IMAGE' and n.image else None) for n in m.node_tree.nodes] if m.use_nodes else []})
(out/'source_inspection.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Rangers_importado.blend'))
result=report
