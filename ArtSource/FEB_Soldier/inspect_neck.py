import bpy,json
from pathlib import Path
from mathutils import Vector
S=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(S/'FEB_Soldier_Realista.blend'))
ob=next(o for o in bpy.data.objects if 'Pele facial' in o.name)
bands={}
for a,b in [(1.50,1.55),(1.55,1.58),(1.58,1.60),(1.60,1.62),(1.62,1.65),(1.65,1.70)]:
    vs=[v.co for v in ob.data.vertices if a<=v.co.z<b]
    bands[str((a,b))]={'n':len(vs),'min':[min(v[i] for v in vs) for i in range(3)],'max':[max(v[i] for v in vs) for i in range(3)]} if vs else {}
(S/'neck_before.json').write_text(json.dumps(bands,indent=2))
sc=bpy.context.scene;sc.cycles.samples=24;sc.render.resolution_x=900;sc.render.resolution_y=900
bpy.data.collections['Apresentacao'].hide_viewport=False
cam=sc.camera;cam.data.ortho_scale=.48
for name,loc in [('perfil_antes',(4,0,1.72)),('nuca_antes',(1.3,4,1.85))]:
    cam.location=loc;cam.rotation_euler=(Vector((0,0,1.64))-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=str(S/'Previews_Realista'/(name+'.png'));bpy.ops.render.render(write_still=True)
