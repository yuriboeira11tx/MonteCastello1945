import bpy,json
from pathlib import Path
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
d=json.loads((out/'rig_deformation_tests.json').read_text());res=[]
for t in d:
 if t['frame'] not in [15,30]:continue
 for r in t['rigs']:
  for ratio,name,a,b in r['stretched_edges'][:5]:
   ob=bpy.data.objects[name]
   res.append(dict(frame=t['frame'],ratio=ratio,object=name,vertices=[dict(id=i,co=list(ob.data.vertices[i].co),weights={ob.vertex_groups[g.group].name:g.weight for g in ob.data.vertices[i].groups}) for i in [a,b]]))
result={'errors':res}
