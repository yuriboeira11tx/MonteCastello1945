import bpy,json
from pathlib import Path
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
info=dict(file=bpy.data.filepath,version=bpy.app.version_string,scene=bpy.context.scene.name,addons=list(bpy.context.preferences.addons.keys()),objects=[])
for idx in range(1,7):
 for ob in bpy.data.collections['FEB_%02d'%idx].objects:
  if ob.type!='MESH':continue
  info['objects'].append(dict(name=ob.name,location=list(ob.location),bounds=[[min(v.co[a] for v in ob.data.vertices) for a in range(3)],[max(v.co[a] for v in ob.data.vertices) for a in range(3)]],vertices=len(ob.data.vertices),vertex_groups=[g.name for g in ob.vertex_groups],modifiers=[dict(type=m.type,name=m.name) for m in ob.modifiers],materials=[m.name for m in ob.data.materials]))
(out/'rig_source_inspection.json').write_text(json.dumps(info,indent=2))
result={k:v for k,v in info.items() if k!='objects'}|{'mesh_count':len(info['objects'])}
