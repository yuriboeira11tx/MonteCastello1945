import bpy,bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from pathlib import Path
OUT=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/BaseGLB')
scene=bpy.data.scenes['FEB_BaseGLB'];bpy.context.window.scene=scene
shirt=bpy.data.objects['FEB_Uniforme_superior'];char=bpy.data.collections['FEB_Personagem_BaseGLB'];rig=bpy.data.objects['FEB_Rig_BaseGLB']
bvh=BVHTree.FromPolygons([v.co.copy() for v in shirt.data.vertices],[tuple(p.vertices) for p in shirt.data.polygons])
for side,s in [('L',1),('R',-1)]:
    old=bpy.data.objects['FEB_Suspensorio.'+side];mat=old.data.materials[0];bpy.data.objects.remove(old,do_unlink=True)
    points=[Vector(p) for p in [(s*.115,-.18,1.085),(s*.116,-.185,1.17),(s*.122,-.184,1.28),(s*.16,-.121,1.42),(s*.175,-.05,1.515),(s*.175,.035,1.525),(s*.16,.13,1.48),(s*.12,.227,1.40),(s*.10,.224,1.30),(-s*.078,.19,1.09)]]
    samples=[a.lerp(b,k/6) for a,b in zip(points[:-1],points[1:]) for k in range(6)]+[points[-1]]
    vs=[]
    for p in samples:
        for dx in [-.0145,.0145]:
            q=p+Vector((dx,0,0));hit,n,_,_=bvh.find_nearest(q)
            # Pocket face clearance on the front; the back follows the actual cloth.
            gap=.020 if p.y<-.08 and 1.19<p.z<1.35 else .005
            vs.append(hit+n*gap if hit else q)
    fs=[(i*2,i*2+1,i*2+3,i*2+2) for i in range(len(samples)-1)]
    me=bpy.data.meshes.new('Lona_conformada.'+side);me.from_pydata(vs,[],fs);me.update();ob=bpy.data.objects.new('FEB_Suspensorio.'+side,me);char.objects.link(ob);me.materials.append(mat)
    for v in me.vertices:
        t=max(0,min(1,(v.co.z-1.08)/.30))
        for name,w in [('Spine',1-t),('UpperChest',t)]:
            if w:(ob.vertex_groups.get(name) or ob.vertex_groups.new(name=name)).add([v.index],w,'REPLACE')
    for p in me.polygons:p.use_smooth=True
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    mod=ob.modifiers.new('Espessura_lona','SOLIDIFY');mod.thickness=.002;mod.offset=0;bpy.ops.object.modifier_apply(modifier=mod.name)
    ob.parent=rig;mod=ob.modifiers.new('FEB_Rig','ARMATURE');mod.object=rig
cam=scene.camera
for name,loc in [('FEB_costas',(-2.3,4,2.1)),('FEB_tres_quartos',(2.6,-4,2.5))]:
    cam.location=loc;cam.rotation_euler=(Vector((0,0,.94))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.09;scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.object.select_all(action='DESELECT');bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FEB_Soldado_BaseGLB.blend'))
result={'saved':bpy.data.filepath}
