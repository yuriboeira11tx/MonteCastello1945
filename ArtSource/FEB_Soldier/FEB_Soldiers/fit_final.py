import bpy
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
s=bpy.data.scenes['FEB_Soldiers'];bpy.context.window.scene=s
for idx in [2,3,5]:
    o=bpy.data.objects['FEB_%02d_Calca_perneiras_botinas'%idx]
    for v in o.data.vertices:
        t=max(0,min(1,(v.co.z-.79)/.095))
        v.co.x*=1-.13*t;v.co.y*=1-.32*t
    o.data.update()
for idx,donor in [(3,4),(5,6)]:
    shift=Vector(bpy.data.objects['FEB_%02d_M41_parte_superior'%idx]['collar_offset'])
    for key in ['FEB','FifthArmy']:
        a=bpy.data.objects['FEB_%02d_Emblema_%s'%(idx,key)]
        b=bpy.data.objects['FEB_%02d_Emblema_%s'%(donor,key)]
        a.data=b.data.copy()
        for v in a.data.vertices:v.co+=shift
        a.data.update()
cam=s.camera
for idx in [3,5]:
    for j in range(1,7):bpy.data.collections['FEB_%02d'%j].hide_render=(j!=idx)
    x=(idx-3.5)*1.4;cam.location=(x+2.4,-4.2,2.15);cam.rotation_euler=(Vector((x,0,1.22))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.45
    s.render.resolution_x=1100;s.render.resolution_y=1100;s.render.filepath=str(out/('FEB_%02d_detalhe.png'%idx));bpy.ops.render.render(write_still=True)
for j in range(1,7):bpy.data.collections['FEB_%02d'%j].hide_render=False
cam.location=(0,-12,3.2);cam.rotation_euler=(Vector((0,0,.92))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=8.7
s.render.resolution_x=2700;s.render.resolution_y=850;s.render.filepath=str(out/'FEB_Soldiers_frente.png');bpy.ops.render.render(write_still=True)
cam.location=(0,12,3.2);cam.rotation_euler=(Vector((0,0,.92))-cam.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=str(out/'FEB_Soldiers_costas.png');bpy.ops.render.render(write_still=True)
cam.location=(0,-12,3.2);cam.rotation_euler=(Vector((0,0,.92))-cam.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result={'fit_complete':True}
