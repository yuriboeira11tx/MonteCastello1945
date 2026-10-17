import bpy,json
from pathlib import Path
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
s=bpy.data.scenes['FEB_Soldiers']
records=[]
for idx in range(1,7):
 for ob in bpy.data.collections['FEB_%02d'%idx].objects:
  if ob.type!='MESH':continue
  me=ob.data; uv=me.uv_layers.active
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
   vi=set(v for p in ps for v in p.vertices);xyz=[me.vertices[v].co for v in vi]
   uvs=[uv.data[l].uv for p in ps for l in p.loop_indices] if uv else []
   cs.append(dict(id=cid,polygons=[p.index for p in ps],bounds=[[round(min(v[a] for v in xyz),5) for a in range(3)],[round(max(v[a] for v in xyz),5) for a in range(3)]],uv=[[round(min(v[a] for v in uvs),5) for a in range(2)],[round(max(v[a] for v in uvs),5) for a in range(2)]] if uvs else None))
  records.append(dict(name=ob.name,materials=[m.name if m else None for m in me.materials],components=cs))
(out/'color_components.json').write_text(json.dumps(records,indent=2))
m=bpy.data.materials['FEB_Soldiers_Uniforme']
result=dict(file=bpy.data.filepath,scene=bpy.context.scene.name,meshes=len(records),body_images=[dict(name=n.image.name,file=n.image.filepath,size=list(n.image.size),packed=bool(n.image.packed_file)) for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image],cameras=[dict(scene=sc.name,camera=sc.camera.name if sc.camera else None) for sc in bpy.data.scenes])
