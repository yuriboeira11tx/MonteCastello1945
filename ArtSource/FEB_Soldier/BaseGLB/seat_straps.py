import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from pathlib import Path
OUT=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/BaseGLB')
scene=bpy.data.scenes['FEB_BaseGLB'];bpy.context.window.scene=scene
shirt=bpy.data.objects['FEB_Uniforme_superior'];bvh=BVHTree.FromPolygons([v.co.copy() for v in shirt.data.vertices],[tuple(p.vertices) for p in shirt.data.polygons])
shifts=[]
for side in ['L','R']:
    ob=bpy.data.objects['FEB_Suspensorio.'+side]
    count=len(ob.data.vertices)//2
    for row in [4,5,6]:
        ids=[row*2,row*2+1];ps=[ob.data.vertices[i].co.copy() for i in ids]
        hits=[bvh.ray_cast(Vector((p.x,p.y,1.8)),Vector((0,0,-1)),1)[0] for p in ps]
        if all(hits):
            delta=max(h.z+.004-p.z for h,p in zip(hits,ps))
            for i in ids+[i+count for i in ids]:ob.data.vertices[i].co.z+=delta
            shifts.append([side,row,delta])
    ob.data.update()
cam=scene.camera;cam.location=(-2.3,4,2.1);cam.rotation_euler=(Vector((0,0,.94))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.09
scene.render.filepath=str(OUT/'FEB_costas.png');bpy.ops.render.render(write_still=True)
cam.location=(2.6,-4,2.5);cam.rotation_euler=(Vector((0,0,.94))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(OUT/'FEB_tres_quartos.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FEB_Soldado_BaseGLB.blend'))
result={'strap_corrections':shifts}
