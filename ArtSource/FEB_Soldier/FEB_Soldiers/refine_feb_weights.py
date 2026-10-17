import bpy,json,heapq
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
OUT=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers');bpy.context.window.scene=bpy.data.scenes['FEB_Soldiers_Rig'];bpy.context.scene.frame_set(1)
def smooth(a,b,x):
 t=float(np.clip((x-a)/(b-a),0,1));return t*t*(3-2*t)
def norm(w):
 w=dict(sorted(((k,v) for k,v in w.items() if v>1e-6),key=lambda kv:kv[1],reverse=True)[:4]);s=sum(w.values());return {k:v/s for k,v in w.items()}
def mix(a,b,t):
 w={k:v*(1-t) for k,v in a.items()}
 for k,v in b.items():w[k]=w.get(k,0)+v*t
 return norm(w)
def put(ob,vi,w):
 for g in ob.vertex_groups:g.remove([vi])
 for k,v in norm(w).items():ob.vertex_groups[k].add([vi],v,'REPLACE')
def get(ob,vi):return {ob.vertex_groups[g.group].name:g.weight for g in ob.data.vertices[vi].groups}
def geo(P,A,seeds):
 D=np.full(len(P),1e5);q=[]
 for i in seeds:D[i]=0;heapq.heappush(q,(0,i))
 while q:
  d,u=heapq.heappop(q)
  if d>D[u]:continue
  for v,w in A[u].items():
   if d+w<D[v]:D[v]=d+w;heapq.heappush(q,(d+w,v))
 return D
stats=[]
for idx in range(1,7):
 arm=bpy.data.objects[f'FEB_Soldado_{idx:02}_Rig'];obs=[o for o in arm.children if o.type=='MESH'];body=[o for o in obs if any(x in o.name for x in ['Corpo_uniforme','M41_parte_superior','Calca_perneiras'])]
 def torso(p):
  z=p[2];c=[.99,1.14,1.28,1.42];n=['hips','spine','chest','upper_chest']
  if z<=c[0]:return {'hips':1}
  if z>=c[-1]:return {'upper_chest':1}
  j=next(j for j in range(3) if c[j]<=z<=c[j+1]);return mix({n[j]:1},{n[j+1]:1},smooth(c[j],c[j+1],z))
 # A continuous shoulder field prevents discontinuities across UV islands.
 changed=0
 for ob in body:
  for v in ob.data.vertices:
   p=np.array(v.co);w=get(ob,v.index)
   if p[2]<1.015 or p[2]>1.56:continue
   if any(k.startswith(('hand','thumb','index','middle','ring','pinky')) for k in w):continue
   if abs(p[0])<.15:continue
   side='L' if p[0]>0 else 'R';upper=arm.data.bones[f'upper_arm.{side}'];fore=arm.data.bones[f'forearm.{side}']
   shoulder=np.array(upper.head_local);elbow=np.array(fore.head_local);wrist=np.array(fore.tail_local);d=wrist-shoulder;d/=np.linalg.norm(d)
   t=smooth(-.06,.06,np.dot(p-elbow,d));aw=mix({f'upper_arm.{side}':1},{f'forearm.{side}':1},t)
   start=.15+.12*(1-smooth(1.10,1.32,p[2]));a=smooth(start,.33,abs(p[0]))
   put(ob,v.index,mix(torso(p),aw,a));changed+=1
 # Determine legs by garment connectivity, including folds crossing the centerline.
 P=[];lookup={};refs=[];faces=[]
 for ob in body:
  for p in ob.data.polygons:
   if max(ob.data.vertices[i].co.z for i in p.vertices)>.99:continue
   # Only the existing leg-weighted fabric belongs to the trousers/boots.
   if not any(any(k.startswith(('thigh','shin','foot')) for k in get(ob,i)) for i in p.vertices):continue
   f=[]
   for vi in p.vertices:
    key=tuple(round(float(x),5) for x in ob.data.vertices[vi].co)
    if key not in lookup:lookup[key]=len(P);P.append(key);refs.append([])
    j=lookup[key];refs[j].append((ob,vi));f.append(j)
   faces.append(f)
 P=np.array(P);A=[{} for _ in P]
 for f in faces:
  for a,b in zip(f,f[1:]+f[:1]):A[a][b]=A[b][a]=float(np.linalg.norm(P[a]-P[b]))
 D={side:geo(P,A,np.where((P[:,2]<.35)&(P[:,0]*sign>.05))[0]) for side,sign in [('L',1),('R',-1)]}
 for i,p in enumerate(P):
  old=get(*refs[i][0]);z=p[2]
  if z<.18:continue
  if min(D['L'][i],D['R'][i])>1e4:side='L' if p[0]>0 else 'R';a=1 if side=='L' else 0
  else:a=smooth(-.10,.10,D['R'][i]-D['L'][i])
  # Central crotch stays supported by the pelvis, with a gradual split to thighs.
  k=smooth(.465,.605,z);left=mix({'shin.L':1},{'thigh.L':1},k);right=mix({'shin.R':1},{'thigh.R':1},k)
  hip=max(smooth(.81,.98,z),smooth(.65,.83,z)*(1-smooth(.035,.16,abs(p[0]))))
  w=mix(mix(right,left,a),{'hips':1},hip)
  for ob,vi in refs[i]:put(ob,vi,w)
 # Weld only in a virtual graph: identify rigid backpack islands without
 # changing the mesh, its UV splits or normals.
 for ob in body:
  pts=[];look={};ids={};adj=[]
  for v in ob.data.vertices:
   key=tuple(round(float(x),5) for x in v.co)
   if key not in look:look[key]=len(pts);pts.append(key);adj.append(set())
   ids[v.index]=look[key]
  for e in ob.data.edges:
   a,b=[ids[i] for i in e.vertices];adj[a].add(b);adj[b].add(a)
  seen=set();rigid=set();P2=np.array(pts)
  for seed in range(len(pts)):
   if seed in seen:continue
   todo=[seed];seen.add(seed);island=[]
   while todo:
    u=todo.pop();island.append(u)
    for v in adj[u]:
     if v not in seen:seen.add(v);todo.append(v)
   low=P2[island].min(0);high=P2[island].max(0)
   if low[1]>.035 and low[2]>.80 and high[2]>1.15 and high[0]-low[0]<.40:rigid.update(island)
  for vi,j in ids.items():
   if j in rigid:put(ob,vi,{'chest':1})
 # Patch weights follow the same deformed sleeve surface.
 surf=[];poly=[];ref=[]
 for ob in body:
  off=len(surf);surf.extend([v.co.copy() for v in ob.data.vertices]);ref.extend([(ob,v.index) for v in ob.data.vertices])
  poly.extend([tuple(off+i for i in p.vertices) for p in ob.data.polygons if all(abs(ob.data.vertices[i].co.x)>.19 for i in p.vertices) and sum(ob.data.vertices[i].co.z for i in p.vertices)/len(p.vertices)>1.15])
 tree=BVHTree.FromPolygons(surf,poly,all_triangles=True)
 for ob in obs:
  if 'Emblema' not in ob.name:continue
  for v in ob.data.vertices:
   near,n,fi,d=tree.find_nearest(v.co);ids=poly[fi];a,b,c=[surf[j] for j in ids]
   bary=barycentric_transform(near,a,b,c,Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)));w={}
   for j,t in zip(ids,bary):
    for k,val in get(*ref[j]).items():w[k]=w.get(k,0)+max(t,0)*val
   put(ob,v.index,w)
 stats.append(dict(soldier=idx,arm_vertices=changed,leg_vertices=len(P)))
bpy.context.view_layer.update()
result={'refinement':stats}
