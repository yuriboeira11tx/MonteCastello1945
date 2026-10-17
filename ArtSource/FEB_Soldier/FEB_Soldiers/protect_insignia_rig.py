import bpy,numpy as np
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
from mathutils import Vector
bpy.context.window.scene=bpy.data.scenes['FEB_Soldiers_Rig'];bpy.context.scene.frame_set(1)
def smooth(a,b,x):
 t=float(np.clip((x-a)/(b-a),0,1));return t*t*(3-2*t)
def get(o,i):return {o.vertex_groups[g.group].name:g.weight for g in o.data.vertices[i].groups}
def put(o,i,w):
 w=dict(sorted(((k,v) for k,v in w.items() if v>1e-5),key=lambda kv:kv[1],reverse=True)[:4]);s=sum(w.values())
 for g in o.vertex_groups:g.remove([i])
 for k,v in w.items():o.vertex_groups[k].add([i],v/s,'REPLACE')
rows=[]
for idx in range(1,7):
 arm=bpy.data.objects[f'FEB_Soldado_{idx:02}_Rig'];obs=[o for o in arm.children if o.type=='MESH'];body=[o for o in obs if any(x in o.name for x in ['Corpo_uniforme','M41_parte_superior'])]
 for patch in [o for o in obs if 'Emblema' in o.name]:
  badgefaces=[p for p in patch.data.polygons if 'Emblema' in patch.data.materials[p.material_index].name]
  ids=set(v for p in badgefaces for v in p.vertices);side='L' if sum(patch.data.vertices[i].co.x for i in ids)>0 else 'R';bn=f'upper_arm.{side}'
  tree=BVHTree.FromPolygons([v.co for v in patch.data.vertices],[tuple(p.vertices) for p in badgefaces],all_triangles=False);count=0
  for ob in body:
   for v in ob.data.vertices:
    if v.co.z<1.15 or v.co.x*(1 if side=='L' else -1)<.16:continue
    q,n,fi,d=tree.find_nearest(v.co)
    if d>=.09:continue
    t=1-smooth(.025,.09,d);w={k:val*(1-t) for k,val in get(ob,v.index).items()};w[bn]=w.get(bn,0)+t;put(ob,v.index,w);count+=1
  for i in ids:put(patch,i,{bn:1})
  # The small cloth closure under the insignia follows the garment surface.
  surf=[];poly=[];ref=[]
  for ob in body:
   off=len(surf);surf.extend([v.co.copy() for v in ob.data.vertices]);ref.extend([(ob,v.index) for v in ob.data.vertices]);poly.extend([tuple(off+i for i in p.vertices) for p in ob.data.polygons])
  sleeve=BVHTree.FromPolygons(surf,poly,all_triangles=True)
  for v in patch.data.vertices:
   if v.index in ids:continue
   near,n,fi,d=sleeve.find_nearest(v.co);inds=poly[fi];a,b,c=[surf[j] for j in inds];bc=barycentric_transform(near,a,b,c,Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)));w={}
   for j,t in zip(inds,bc):
    for k,val in get(*ref[j]).items():w[k]=w.get(k,0)+max(0,t)*val
   put(patch,v.index,w)
  rows.append({'soldier':idx,'patch':patch.name,'sleeve_vertices':count})
bpy.context.view_layer.update();result={'insignia_weights':rows}
