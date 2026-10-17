import bpy,json
from pathlib import Path
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
data=[]
for i in range(1,7):
 for o in bpy.data.collections['FEB_%02d'%i].objects:
  if o.type!='MESH':continue
  me=o.data;uv=me.uv_layers.active
  parent=list(range(len(me.vertices)))
  def root(x):
   while parent[x]!=x:
    parent[x]=parent[parent[x]];x=parent[x]
   return x
  for e in me.edges:
   a,b=[root(v) for v in e.vertices];parent[b]=a
  groups={}
  for p in me.polygons:groups.setdefault(root(p.vertices[0]),[]).append(p)
  cs=[]
  for cid,ps in enumerate(groups.values()):
   vs=[me.vertices[v].co for v in set(v for p in ps for v in p.vertices)]
   us=[uv.data[l].uv for p in ps for l in p.loop_indices]
   cs.append(dict(id=cid,p=[p.index for p in ps],b=[[round(f(v[a] for v in vs),5) for a in range(3)] for f in (min,max)],uv=[[round(f(v[a] for v in us),5) for a in range(2)] for f in (min,max)]))
  mats=[]
  for m in me.materials:
   mats.append(dict(name=m.name,images=[dict(name=n.image.name,path=n.image.filepath,size=list(n.image.size)) for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]))
  data.append(dict(name=o.name,mats=mats,components=cs))
(out/'skin_components.json').write_text(json.dumps(data,indent=2))
result=dict(file=bpy.data.filepath,scene=bpy.context.scene.name,objects=len(data),heads=[dict(name=x['name'],mats=x['mats'],components=[{k:v for k,v in c.items() if k!='p'}|{'faces':len(c['p'])} for c in x['components']]) for x in data if any(any(t in m['name'] for t in ['head','braebury']) for m in x['mats'])])
