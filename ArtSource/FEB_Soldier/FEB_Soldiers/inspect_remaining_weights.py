import bpy,json
from pathlib import Path
O=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers');comp={r['name']:r['components'] for r in json.loads((O/'skin_components.json').read_text())};rows=[]
for name,ids in [('FEB_05_M41_parte_superior',[78,106,523,77]),('FEB_06_Corpo_uniforme',[671,94])]:
 ob=bpy.data.objects[name]
 for vi in ids:
  polygons={p.index for p in ob.data.polygons if vi in p.vertices}
  rows.append({'object':name,'vertex':vi,'components':[c for c in comp[name] if polygons.intersection(c['p'])]})
result={'components':rows,'elbows':{str(i):[list(bpy.data.objects[f'FEB_Soldado_{i:02}_Rig'].data.bones['forearm.'+side].head_local) for side in ['L','R']] for i in range(1,7)}}
