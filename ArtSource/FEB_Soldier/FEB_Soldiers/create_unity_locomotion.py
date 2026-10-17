import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
O=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers/UnityDelivery');s=bpy.data.scenes['FEB_Unity_Export'];bpy.context.window.scene=s
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
F=json.loads((O/'finger_rest.json').read_text());rigs=[bpy.data.objects[f'FEB_Soldado_{i:02}_Armature'] for i in range(1,7)]
actions={};report=[]
def orient(a,name,q):
 p=a.pose.bones[name];p.matrix=Matrix.Translation(p.head)@q.to_matrix().to_4x4();bpy.context.view_layer.update()
def direct(a,name,direction):
 p=a.pose.bones[name];q=p.matrix.to_quaternion();turn=(q@Vector((0,1,0))).rotation_difference(Vector(direction).normalized());orient(a,name,turn@q)
def turn(a,name,axis,angle):
 p=a.pose.bones[name];orient(a,name,Quaternion(Vector(axis),angle)@p.matrix.to_quaternion())
def leg(a,side,target,footrotation):
 thigh=a.pose.bones['thigh.'+side];shin=a.pose.bones['shin.'+side];h=thigh.head.copy();delta=target-h;distance=delta.length;l1=thigh.bone.length;l2=shin.bone.length
 d=min(distance,l1+l2-.0004);direction=delta.normalized();bend=Vector((0,-1,0));bend=(bend-direction*bend.dot(direction)).normalized()
 along=(l1*l1-l2*l2+d*d)/(2*d);height=math.sqrt(max(0,l1*l1-along*along));knee=h+direction*along+bend*height
 direct(a,'thigh.'+side,knee-h);direct(a,'shin.'+side,target-a.pose.bones['shin.'+side].head);orient(a,'foot.'+side,footrotation)
 return max(0,distance-(l1+l2))
for idx,a in enumerate(rigs,1):
 mesh=next(o for o in a.children if o.type=='MESH');actions[idx]={};sole={};restfoot={}
 for side in ['L','R']:
  b=a.data.bones['foot.'+side];restfoot[side]=b.matrix_local.to_quaternion();gi=mesh.vertex_groups['foot.'+side].index
  sole[side]=[b.matrix_local.inverted()@v.co for v in mesh.data.vertices if any(g.group==gi and g.weight>.999 for g in v.groups)]
 for name,frames,duty,stride,lift in [('Idle',60,1,0,0),('Walk',30,.60,.72,.12),('Run',20,.35,.7933,.24)]:
  if a.animation_data:a.animation_data.action=None
  action=None;maxreach=0
  for frame in range(1,frames+2):
   phase=(frame-1)/frames;theta=2*math.pi*phase
   for pb in a.pose.bones:pb.matrix_basis.identity();pb.rotation_mode='QUATERNION'
   bpy.context.view_layer.update()
   if name=='Idle':drop=.024+.004*math.cos(theta)
   elif name=='Walk':drop=.062+.065*math.cos(theta)**2
   else:drop=.135+.025*math.cos(theta*2)
   hip=a.pose.bones['hips'];hip.location=hip.bone.matrix_local.to_3x3().inverted()@Vector((.006*math.sin(theta) if name!='Idle' else 0,0,-drop));bpy.context.view_layer.update()
   turn(a,'spine',(1,0,0),math.radians(1.5 if name=='Idle' else 4 if name=='Walk' else 9))
   turn(a,'chest',(0,0,1),math.radians((1 if name=='Idle' else 3 if name=='Walk' else 5)*math.sin(theta)))
   turn(a,'head',(0,0,1),math.radians(2*math.sin(theta) if name=='Idle' else -2*math.sin(theta)))
   for side,sign,offset in [('L',1,0),('R',-1,.5)]:
    p=(phase+offset)%1;angle=2*math.pi*p
    if name=='Idle':y=0;z=0;pitch=0
    elif p<duty:
     t=p/duty;y=-stride*.5+stride*t;z=0;pitch=math.radians(-8*max(0,1-t/.2)+18*max(0,(t-.75)/.25))
    else:
     t=(p-duty)/(1-duty);ease=t*t*(3-2*t);y=stride*.5-stride*ease;z=lift*math.sin(math.pi*t);pitch=math.radians(10*math.sin(2*math.pi*t))
    q=Quaternion(Vector((1,0,0)),pitch)@restfoot[side]
    ankle=a.data.bones['foot.'+side].head_local.copy();ankle.y+=y;ankle.z=z-min((q@v).z for v in sole[side]);maxreach=max(maxreach,leg(a,side,ankle,q))
    swing=0 if name=='Idle' else math.radians(22 if name=='Walk' else 40)*math.cos(angle)
    base=Vector((sign*.13,.015,-1)).normalized();upper=Quaternion(Vector((1,0,0)),swing)@base;direct(a,'upper_arm.'+side,upper)
    flex=math.radians(12 if name=='Idle' else 24+8*math.sin(angle) if name=='Walk' else 78+10*math.sin(angle))
    fore=Quaternion(Vector((1,0,0)),-flex)@upper;direct(a,'forearm.'+side,fore);direct(a,'hand.'+side,fore)
   for bn,oldq in F[str(idx)].items():
    b=a.data.bones[bn];newlocal=(b.parent.matrix_local.inverted()@b.matrix_local).to_quaternion();a.pose.bones[bn].rotation_quaternion=newlocal.inverted()@Quaternion(oldq)
   bpy.context.view_layer.update()
   for pb in a.pose.bones:
    pb.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=pb.name)
    if pb.name in ['hips','Root']:pb.keyframe_insert(data_path='location',frame=frame,group=pb.name)
  action=a.animation_data.action;action.name=f'FEB_{idx:02}_{name}';action.use_fake_user=True;actions[idx][name]=action
  if idx==1:
   s.frame_start=1;s.frame_end=frames+1;s.frame_set(1);where=a.location.copy();a.location=(0,0,0)
   bpy.ops.object.select_all(action='DESELECT');a.select_set(True);bpy.context.view_layer.objects.active=a
   bpy.ops.export_scene.fbx(filepath=str(O/'Animations'/f'FEB_{name}.fbx'),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,use_armature_deform_only=True,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS')
   a.location=where
  report.append({'soldier':idx,'clip':name,'seconds':frames/30,'frames':frames+1,'max_leg_target_overreach_m':maxreach})
 for pb in a.pose.bones:pb.matrix_basis.identity()
for name,frame in [('Idle',1),('Walk',8),('Run',6)]:
 for idx,a in enumerate(rigs,1):a.animation_data.action=actions[idx][name]
 s.frame_set(frame);s.render.filepath=str(O/'Previews'/f'FEB_{name}_Blender.png');bpy.ops.render.render(write_still=True)
for idx,a in enumerate(rigs,1):a.animation_data.action=actions[idx]['Idle']
s.frame_set(1);s.frame_end=61
(O/'animation_manifest.json').write_text(json.dumps({'clips':[{'name':'Idle','file':'FEB_Idle.fbx','seconds':2,'speed':0},{'name':'Walk','file':'FEB_Walk.fbx','seconds':1,'speed':1.2},{'name':'Run','file':'FEB_Run.fbx','seconds':2/3,'speed':3.4}],'checks':report},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(O/'FEB_Soldiers_Unity.blend'));result={'clips':report}
