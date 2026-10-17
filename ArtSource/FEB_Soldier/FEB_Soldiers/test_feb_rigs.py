import bpy, json, math
import numpy as np
from pathlib import Path
from mathutils import Vector, Quaternion
OUT=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
s=bpy.data.scenes['FEB_Soldiers_Rig'];bpy.context.window.scene=s
rigs=[bpy.data.objects[f'FEB_Soldado_{i:02}_Rig'] for i in range(1,7)]
for arm in rigs:
    # Blend the lower neck into the jacket attachment while the head turns.
    for ob in arm.children:
        if ob.type!='MESH' or not any('Pele_Rosto' in m.name for m in ob.data.materials if m):continue
        for v in ob.data.vertices:
            if v.co.z>=1.51:continue
            t=max(0,min(1,(v.co.z-1.445)/.065));t=t*t*(3-2*t)
            for g in ob.vertex_groups:g.remove([v.index])
            if t>0:ob.vertex_groups['neck'].add([v.index],t,'REPLACE')
            if t<1:ob.vertex_groups['upper_chest'].add([v.index],1-t,'REPLACE')
    if arm.animation_data:arm.animation_data.action=None
    def rotation(name,axis,angle):
        pb=arm.pose.bones[name];local=pb.bone.matrix_local.to_3x3().inverted()@Vector(axis)
        pb.rotation_euler=Quaternion(local,math.radians(angle)).to_euler('XYZ')
    for frame in [1,15,30,45,60]:
        for pb in arm.pose.bones:pb.matrix_basis.identity()
        if frame==15:
            rotation('upper_arm.L',(0,1,0),-45);rotation('upper_arm.R',(0,1,0),45)
            for side in ['L','R']:
                rotation(f'forearm.{side}',(1,0,0),-55)
                for digit in ['index','middle','ring','pinky']:
                    for n in [1,2,3]:rotation(f'{digit}_{n:02}.{side}',(0,1,0),8 if side=='L' else -8)
            rotation('head',(0,0,1),22)
        elif frame==30:
            rotation('thigh.L',(1,0,0),-30);rotation('shin.L',(1,0,0),55);rotation('foot.L',(1,0,0),-25)
            rotation('thigh.R',(1,0,0),12);rotation('shin.R',(1,0,0),15)
            rotation('forearm.L',(1,0,0),-65);rotation('upper_arm.R',(1,0,0),20)
            rotation('chest',(0,0,1),12);rotation('head',(0,0,1),-18)
        elif frame==45:
            arm.pose.bones['hips'].location=arm.data.bones['hips'].matrix_local.to_3x3().inverted()@Vector((0,0,-.13))
            for side in ['L','R']:
                rotation(f'thigh.{side}',(1,0,0),-35);rotation(f'shin.{side}',(1,0,0),65);rotation(f'foot.{side}',(1,0,0),-30)
                rotation(f'forearm.{side}',(1,0,0),-60)
            rotation('spine',(1,0,0),12);rotation('neck',(1,0,0),-10)
        for pb in arm.pose.bones:
            if pb.bone.use_deform:
                pb.keyframe_insert(data_path='rotation_euler',frame=frame,group=pb.name)
                pb.keyframe_insert(data_path='location',frame=frame,group=pb.name)
    arm.animation_data.action.name=f'FEB_{arm["personagem"]:02}_Teste_Articulacoes'
    arm.animation_data.action.use_fake_user=True

validation=[]
for frame in [1,15,30,45]:
    s.frame_set(frame);bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
    per=[]
    for arm in rigs:
        worst=[];maxerr=0
        for ob in arm.children:
            if ob.type!='MESH':continue
            ev=ob.evaluated_get(dg);me=ev.to_mesh()
            P=np.array([v.co[:] for v in ob.data.vertices]);Q=np.array([v.co[:] for v in me.vertices])
            assert np.isfinite(Q).all()
            if frame==1:maxerr=max(maxerr,float(np.linalg.norm(Q-P,axis=1).max()))
            for e in ob.data.edges:
                a,b=e.vertices;before=np.linalg.norm(P[a]-P[b]);after=np.linalg.norm(Q[a]-Q[b])
                if before>.004 and after/before>2.5:worst.append((float(after/before),ob.name,int(a),int(b)))
            ev.to_mesh_clear()
        per.append(dict(soldier=arm['personagem'],rest_error=maxerr,stretched_edges=sorted(worst,reverse=True)[:12]))
    validation.append(dict(frame=frame,rigs=per))
    if frame in [15,30,45]:
        s.camera.location=(0,-12,3.3);target=Vector((0,-.05,.9));s.camera.rotation_euler=(target-s.camera.location).to_track_quat('-Z','Y').to_euler();s.camera.data.ortho_scale=8.6
        s.render.resolution_x=2400;s.render.resolution_y=850;s.render.filepath=str(OUT/f'FEB_Soldiers_Rig_Teste_{frame:02}.png');bpy.ops.render.render(write_still=True)

# Detail render with all six meshes still present; crop the camera to soldier 01.
s.frame_set(15);s.camera.location=(-1.5,-4,2.1);target=Vector((-3.5,-.1,1.25));s.camera.rotation_euler=(target-s.camera.location).to_track_quat('-Z','Y').to_euler();s.camera.data.ortho_scale=1.65
s.render.resolution_x=1200;s.render.resolution_y=1200;s.render.filepath=str(OUT/'FEB_Soldado_01_Rig_Teste_Detalhe.png');bpy.ops.render.render(write_still=True)
s.frame_set(1)
(OUT/'rig_deformation_tests.json').write_text(json.dumps(validation,indent=2))
result={'tests':validation}
