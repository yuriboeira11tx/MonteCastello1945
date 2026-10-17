import bpy,json,math
import numpy as np
from pathlib import Path
from mathutils import Vector
OUT=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers');s=bpy.data.scenes['FEB_Soldiers_Rig'];bpy.context.window.scene=s;s.frame_set(1)
COMP={r['name']:r['components'] for r in json.loads((OUT/'skin_components.json').read_text())}
def put(ob,vi,w):
 for g in ob.vertex_groups:g.remove([vi])
 for k,v in w.items():
  if v>1e-6:ob.vertex_groups[k].add([vi],v,'REPLACE')
def smooth(a,b,x):
 t=float(np.clip((x-a)/(b-a),0,1));return t*t*(3-2*t)
widgets=bpy.data.collections.new('FEB_Rig_Formas_Controles');s.collection.children.link(widgets);widgets.hide_render=True
def shape(name,points,edges):
 me=bpy.data.meshes.new(name);me.from_pydata(points,edges,[]);ob=bpy.data.objects.new(name,me);widgets.objects.link(ob);ob.hide_render=True;ob.hide_set(True);return ob
circle=shape('FEB_Widget_Alvo',[(math.cos(a),0,math.sin(a)) for a in np.linspace(0,2*math.pi,24,endpoint=False)],[(i,(i+1)%24) for i in range(24)])
diamond=shape('FEB_Widget_Polo',[(0,0,1),(1,0,0),(0,0,-1),(-1,0,0)],[(0,1),(1,2),(2,3),(3,0)])
root=shape('FEB_Widget_Raiz',[(math.cos(a),math.sin(a),0) for a in np.linspace(0,2*math.pi,32,endpoint=False)],[(i,(i+1)%32) for i in range(32)])
for idx in range(1,7):
 arm=bpy.data.objects[f'FEB_Soldado_{idx:02}_Rig']
 for pb in arm.pose.bones:
  if pb.name.startswith(('CTRL_','POLE_')):
   pb.custom_shape=root if pb.name=='CTRL_root' else diamond if pb.name.startswith('POLE') else circle
   pb.use_custom_shape_bone_size=False;size=.30 if pb.name=='CTRL_root' else .045 if pb.name.startswith('POLE') else .075;pb.custom_shape_scale_xyz=(size,size,size)
 for side in ['L','R']:
  for label,chain,terminal in [('Braco','forearm','hand'),('Perna','shin','foot')]:
   prop=f'IK_{label}_{side}';arm[prop]=0.0
   for bn in [f'{chain}.{side}',f'{terminal}.{side}']:
    for con in arm.pose.bones[bn].constraints:
     if con.type not in ['IK','COPY_ROTATION']:continue
     try:con.driver_remove('influence')
     except:pass
     fc=con.driver_add('influence');dr=fc.driver;dr.type='SCRIPTED';v=dr.variables.new();v.name='ik';v.type='SINGLE_PROP';v.targets[0].id=arm;v.targets[0].data_path=f'["{prop}"]';dr.expression='ik'
 # Back strap fasteners are small rigid pieces, kept with the backpack.
 for ob in arm.children:
  if ob.type!='MESH' or ob.get('fonte_original') not in COMP:continue
  if not any(x in ob.name for x in ['Corpo_uniforme','M41_parte_superior']):continue
  for c in COMP[ob['fonte_original']]:
   lo,hi=np.array(c['b']);u0,u1=np.array(c['uv'])
   anchor=(.118<u0[0]<.120 and .52<u0[1]<.53 and u1[1]<.59) or (.133<u0[0]<.135 and .196<u0[1]<.199 and u1[1]<.245)
   if anchor and lo[1]>.015 and hi[2]<1.25:
    for vi in set(v for pi in c['p'] for v in ob.data.polygons[pi].vertices):put(ob,vi,{'chest':1})
 arm.update_tag()
bpy.context.view_layer.update()
result={'drivers_restored':sum(len(bpy.data.objects[f'FEB_Soldado_{i:02}_Rig'].animation_data.drivers) for i in range(1,7)),'controls':6*9}
