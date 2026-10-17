import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
s=bpy.data.scenes['FEB_Soldiers'];bpy.context.window.scene=s
archive=bpy.data.collections['Acessorios_Rangers_retirados']
comps={r['object']:r['components'] for r in json.loads((out/'components.json').read_text())}
orig={int(n[4:6]):bpy.data.objects[n] for n in comps if 'DrawCall_0_DrawCall_0' in n}
def lower(c):
    if c['bounds'][1][2]>=.95 or not c['uv']:return False
    a,b=c['uv']
    return (b[0]<.312 and b[1]<.265) or (.215<a[0] and b[0]<.29 and b[1]<.85) or (.29<a[0] and b[0]<.405 and a[1]>.63 and b[1]<.825)
for idx in [2,3,5]:
    old=bpy.data.objects['FEB_%02d_Calca_perneiras_botinas'%idx]
    for c in list(old.users_collection):c.objects.unlink(old)
    archive.objects.link(old);old.hide_render=True;old.name+='__anterior'
    source=orig[6] if idx in (2,3) else orig[5]
    ids=set(p for c in comps[source.name] if lower(c) for p in c['polygon_ids'])
    o=source.copy();o.data=source.data.copy();o.name='FEB_%02d_Calca_perneiras_botinas'%idx;o.hide_render=False;o.hide_viewport=False
    bpy.data.collections['FEB_%02d'%idx].objects.link(o);o.location=((idx-3.5)*1.4,0,0);o['variant']=idx
    bm=bmesh.new();bm.from_mesh(o.data);bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.index not in ids],context='FACES')
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
    bm.to_mesh(o.data);bm.free();o.data.update()
# Neutralize any empty material left by the FBX without changing original texture pixels.
for o in s.objects:
    if o.type=='MESH' and archive not in o.users_collection:
        for p in o.data.polygons:p.use_smooth=True
cam=s.camera
cam.location=(0,-12,3.2);cam.rotation_euler=(Vector((0,0,.92))-cam.location).to_track_quat('-Z','Y').to_euler()
s.render.resolution_x=2700;s.render.resolution_y=850;cam.data.ortho_scale=8.7
s.render.filepath=str(out/'FEB_Soldiers_frente.png');bpy.ops.render.render(write_still=True)
cam.location=(0,12,3.2);cam.rotation_euler=(Vector((0,0,.92))-cam.location).to_track_quat('-Z','Y').to_euler()
s.render.filepath=str(out/'FEB_Soldiers_costas.png');bpy.ops.render.render(write_still=True)
# Close-up of a replaced jacket and the left FEB emblem.
for i in [1,3,5]:
    for j in range(1,7):bpy.data.collections['FEB_%02d'%j].hide_render=(j!=i)
    x=(i-3.5)*1.4
    cam.location=(x+2.4,-4.2,2.15);target=Vector((x,0,1.22))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.45
    s.render.resolution_x=1100;s.render.resolution_y=1100;s.render.filepath=str(out/('FEB_%02d_detalhe.png'%i));bpy.ops.render.render(write_still=True)
for j in range(1,7):bpy.data.collections['FEB_%02d'%j].hide_render=False
cam.location=(0,-12,3.2);cam.rotation_euler=(Vector((0,0,.92))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=8.7
s.render.resolution_x=2700;s.render.resolution_y=850
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result={'finished':True}
