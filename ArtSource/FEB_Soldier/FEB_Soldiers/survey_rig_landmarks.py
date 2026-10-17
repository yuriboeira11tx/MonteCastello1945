import bpy,json,sys,heapq,numpy as np
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
data=json.loads((out/'skin_variants.json').read_text(encoding='utf-8'));records=json.loads((out/'skin_components.json').read_text())
survey={}
for idx in range(1,7):
 hands={}
 for side,sign in [('L',1),('R',-1)]:
  points=[];faces=[];lookup={}
  for name,ids in data['hands_selection'].items():
   if not name.startswith('FEB_%02d_'%idx):continue
   ob=bpy.data.objects[name]
   for pi in ids:
    p=ob.data.polygons[pi]
    if sum(ob.data.vertices[v].co.x for v in p.vertices)*sign<=0:continue
    f=[]
    for vi in p.vertices:
     co=ob.data.vertices[vi].co;key=tuple(round(float(x),5) for x in co)
     if key not in lookup:lookup[key]=len(points);points.append(list(co))
     f.append(lookup[key])
    faces.append(f)
  P=np.array(points);edges={};adj=[{} for _ in points]
  for f in faces:
   for a,b in zip(f,f[1:]+f[:1]):
    key=tuple(sorted((a,b)));edges[key]=edges.get(key,0)+1
    d=float(np.linalg.norm(P[a]-P[b]));adj[a][b]=d;adj[b][a]=d
  # Cuff boundary consists of the boundary ring nearest the shoulder.
  boundary=set(v for e,c in edges.items() if c==1 for v in e)
  shoulder=np.array([.19*sign,-.02,1.41]);dist=np.linalg.norm(P-shoulder,axis=1)
  cuff=[i for i in boundary if dist[i]<np.min(dist)+.033]
  if len(cuff)<4:cuff=np.argsort(dist)[:12].tolist()
  wrist=P[cuff].mean(axis=0)
  D=np.full(len(P),np.inf);queue=[]
  for i in cuff:D[i]=0;heapq.heappush(queue,(0,i))
  while queue:
   d,u=heapq.heappop(queue)
   if d>D[u]:continue
   for v,w in adj[u].items():
    nd=d+w
    if nd<D[v]:D[v]=nd;heapq.heappush(queue,(nd,v))
  finite=np.isfinite(D)
  # Record peaks; cluster by geodesic separation instead of Euclidean distance.
  remaining=finite.copy();peaks=[]
  for _ in range(5):
   score=np.where(remaining,D,-1);seed=int(np.argmax(score))
   if score[seed]<0:break
   peaks.append(seed);q=[(0,seed)];seen={seed:0}
   while q:
    d,u=heapq.heappop(q)
    if d>.055:continue
    remaining[u]=False
    for v,w in adj[u].items():
     nd=d+w
     if nd<seen.get(v,1e9) and nd<.055:seen[v]=nd;heapq.heappush(q,(nd,v))
  hands[side]=dict(points=points,faces=faces,cuff=cuff,wrist=wrist.tolist(),distances=[float(x) if np.isfinite(x) else None for x in D],peak_indices=peaks,peaks=[P[i].tolist() for i in peaks],unreachable=int((~finite).sum()))
 survey[str(idx)]=hands
(out/'rig_hands_geometry.json').write_text(json.dumps(survey))
# Inspect one hand in isolation with its original geometry, from two directions.
s=bpy.data.scenes['FEB_Soldiers'];view=bpy.data.scenes.new('Estudo_maos_rig');view.world=s.world
hand=survey['1']['L'];me=bpy.data.meshes.new('Mao_estudo_rig');me.from_pydata(hand['points'],[],hand['faces']);me.update()
ob=bpy.data.objects.new('Mao_estudo_rig',me);view.collection.objects.link(ob)
for p in me.polygons:p.use_smooth=True
mat=bpy.data.materials.new('Cinza_estudo_mao');mat.diffuse_color=(.50,.50,.50,1);mat.use_nodes=True;mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.50,.50,.50,1);mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.7;me.materials.append(mat)
for light in s.objects:
 if light.type=='LIGHT':view.collection.objects.link(light)
cam=s.camera.copy();cam.data=s.camera.data.copy();view.collection.objects.link(cam);view.camera=cam;cam.data.ortho_scale=.24
target=Vector(np.mean(hand['points'],axis=0));view.render.engine='CYCLES';view.cycles.samples=16;view.cycles.use_denoising=True;view.view_settings.view_transform='AgX';view.render.resolution_x=900;view.render.resolution_y=900;view.render.resolution_percentage=100;view.render.image_settings.file_format='PNG'
bpy.context.window.scene=view
for label,direction in [('frente',(0,-1,.2)),('lado',(1,.2,.2)),('cima',(0,0,1))]:
 cam.location=target+Vector(direction);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();view.render.filepath=str(out/('Rig_mao_estudo_'+label+'.png'));bpy.ops.render.render(write_still=True)
bpy.context.window.scene=s
result={idx:{side:{k:v for k,v in h.items() if k in ('wrist','peaks','unreachable')} for side,h in hands.items()} for idx,hands in survey.items()}
