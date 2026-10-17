import bpy,json,numpy as np
from pathlib import Path
from mathutils import Vector
OUT=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers');s=bpy.data.scenes['FEB_Soldiers_Rig'];bpy.context.window.scene=s
rigs=[bpy.data.objects[f'FEB_Soldado_{i:02}_Rig'] for i in range(1,7)];checks=[]
for frame in [1,15,30,45]:
 s.frame_set(frame);bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();rows=[]
 for arm in rigs:
  worst=[];resterror=0
  for ob in arm.children:
   if ob.type!='MESH':continue
   ev=ob.evaluated_get(dg);me=ev.to_mesh();P=np.array([v.co[:] for v in ob.data.vertices]);Q=np.array([v.co[:] for v in me.vertices])
   assert np.isfinite(Q).all()
   if frame==1:resterror=max(resterror,float(np.linalg.norm(Q-P,axis=1).max()))
   for e in ob.data.edges:
    a,b=e.vertices;d=np.linalg.norm(P[a]-P[b]);new=np.linalg.norm(Q[a]-Q[b])
    if d>.004 and new/d>2.5:worst.append((float(new/d),ob.name,int(a),int(b)))
   ev.to_mesh_clear()
  rows.append(dict(soldier=arm['personagem'],rest_error=resterror,stretched_edges=sorted(worst,reverse=True)[:12]))
 checks.append(dict(frame=frame,rigs=rows))
s.frame_set(1);ik=[];appearance=[]
for arm in rigs:
 saved=arm.animation_data.action;arm.animation_data.action=None
 for pb in arm.pose.bones:pb.matrix_basis.identity()
 row={'soldier':arm['personagem'],'targets':[]}
 for side in ['L','R']:
  for label,chain,target,delta in [('Braco','forearm','mao',(0,-.10,.10)),('Perna','shin','pe',(0,-.12,.12))]:
   prop=f'IK_{label}_{side}';arm[prop]=1.0;ctrl=arm.pose.bones[f'CTRL_{target}.{side}'];ctrl.location=ctrl.bone.matrix_local.to_3x3().inverted()@Vector(delta)
   arm.update_tag();bpy.context.view_layer.update()
   error=(arm.pose.bones[f'{chain}.{side}'].tail-ctrl.head).length
   row['targets'].append({'control':ctrl.name,'error_m':error});ctrl.location=(0,0,0);arm[prop]=0.0;arm.update_tag();bpy.context.view_layer.update()
 arm.animation_data.action=saved;ik.append(row)
 for ob in arm.children:
  if ob.type!='MESH':continue
  src=bpy.data.objects[ob['fonte_original']]
  assert len(ob.data.vertices)==len(src.data.vertices)
  assert all(a.co==b.co for a,b in zip(ob.data.vertices,src.data.vertices))
  assert all(tuple(a.vertices)==tuple(b.vertices) and a.material_index==b.material_index for a,b in zip(ob.data.polygons,src.data.polygons))
  assert [m.name for m in ob.data.materials]==[m.name for m in src.data.materials]
  for la,lb in zip(ob.data.uv_layers,src.data.uv_layers):assert all(a.uv==b.uv for a,b in zip(la.data,lb.data))
  for v in ob.data.vertices:
   assert 1<=len(v.groups)<=4
   assert abs(sum(g.weight for g in v.groups)-1)<1e-5
  appearance.append(ob.name)
s.frame_set(1)
report={'deformation':checks,'ik':ik,'unchanged_geometry_uv_materials':len(appearance)}
# Explicit absolute path: never infer a Unity Assets path from a source hierarchy.
report['unity_characters_directory_empty']=not any(Path('E:/Unity/MonteCastello1945/Assets/Art/Characters').iterdir())
assert report['unity_characters_directory_empty']
(OUT/'rig_final_validation.json').write_text(json.dumps(report,indent=2));result=report
