import bpy,json
from pathlib import Path
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
s=bpy.data.scenes['FEB_Soldiers']; bpy.context.window.scene=s
report=[]
for o in s.objects:
    if o.type!='MESH':continue
    me=o.data; links={v.index:set() for v in me.vertices}
    for e in me.edges:
        a,b=e.vertices;links[a].add(b);links[b].add(a)
    components=[];remaining=set(links)
    while remaining:
        stack=[remaining.pop()];ids=set(stack)
        while stack:
            for v in links[stack.pop()]:
                if v in remaining:remaining.remove(v);ids.add(v);stack.append(v)
        ps=[p for p in me.polygons if p.vertices[0] in ids]
        co=[me.vertices[i].co for i in ids]
        uv=[me.uv_layers.active.data[l].uv for p in ps for l in p.loop_indices]
        components.append({'id':len(components),'vertices':len(ids),'polygons':len(ps),'tris':sum(len(p.vertices)-2 for p in ps),'bounds':[[round(min(v[i] for v in co),4) for i in range(3)],[round(max(v[i] for v in co),4) for i in range(3)]],'uv':[[round(min(v[i] for v in uv),4) for i in range(2)],[round(max(v[i] for v in uv),4) for i in range(2)]] if uv else None,'polygon_ids':[p.index for p in ps]})
    report.append({'object':o.name,'materials':[m.name for m in me.materials],'components':components})
(out/'components.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
result={'objects':len(report),'component_counts':[(r['object'],len(r['components'])) for r in report]}
