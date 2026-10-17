import bpy,bmesh,math
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from mathutils.interpolate import poly_3d_calc
from pathlib import Path
OUT=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/BaseGLB')
scene=bpy.data.scenes['FEB_BaseGLB'];bpy.context.window.scene=scene
rig=bpy.data.objects['FEB_Rig_BaseGLB'];char=bpy.data.collections['FEB_Personagem_BaseGLB'];shirt=bpy.data.objects['FEB_Uniforme_superior']
bvh=BVHTree.FromPolygons([v.co.copy() for v in shirt.data.vertices],[tuple(p.vertices) for p in shirt.data.polygons])
def attach(ob):
    ob.parent=rig;m=ob.modifiers.new('FEB_Rig','ARMATURE');m.object=rig
def group(ob,n,idx,w):
    if w>0:(ob.vertex_groups.get(n) or ob.vertex_groups.new(name=n)).add([idx],w,'REPLACE')
def activate(ob):
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
for name,side,sign in [('FEB_Emblema_esquerdo','L',1),('FEB_5Exercito_direito','R',-1)]:
    old=bpy.data.objects[name];mat=old.data.materials[0];bpy.data.objects.remove(old,do_unlink=True)
    outline=[(.21,.997),(.79,.997),(.997,.82),(.997,.18),(.79,.003),(.21,.003),(.003,.18),(.003,.82)]
    uv=[(.5,.5)]+outline;vs=[(sign*(.265+(1-v)*.093),.019+(u-.5)*.073,1.5) for u,v in uv]
    me=bpy.data.meshes.new(name);me.from_pydata(vs,[],[(0,1+i,1+(i+1)%8) for i in range(8)]);me.update();layer=me.uv_layers.new(name='UVMap')
    for p in me.polygons:
        for li in p.loop_indices:layer.data[li].uv=uv[me.loops[li].vertex_index]
    ob=bpy.data.objects.new(name,me);char.objects.link(ob);me.materials.append(mat)
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=3,use_grid_fill=True);bm.to_mesh(me);bm.free()
    for v in me.vertices:
        hit,normal,idx,dist=bvh.ray_cast(Vector((v.co.x,v.co.y,1.75)),Vector((0,0,-1)),.6)
        if hit:
            v.co=hit+normal*.0022;p=shirt.data.polygons[idx];bary=poly_3d_calc([shirt.data.vertices[i].co for i in p.vertices],hit);ws={}
            for vi,w in zip(p.vertices,bary):
                for g in shirt.data.vertices[vi].groups:
                    n=shirt.vertex_groups[g.group].name
                    if n in rig.data.bones:ws[n]=ws.get(n,0)+max(0,w)*g.weight
            total=sum(ws.values())
            for n,w in ws.items():group(ob,n,v.index,w/total)
        else:group(ob,'UpperArm.'+side,v.index,1)
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
    if sum(f.normal.z for f in bm.faces)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    bm.to_mesh(me);bm.free()
    for p in me.polygons:p.use_smooth=True
    activate(ob);sol=ob.modifiers.new('Bordado','SOLIDIFY');sol.thickness=.0008;sol.offset=-1;bpy.ops.object.modifier_apply(modifier=sol.name);attach(ob)

for side,s in [('L',1),('R',-1)]:
    old=bpy.data.objects['FEB_Suspensorio.'+side];mat=old.data.materials[0];bpy.data.objects.remove(old,do_unlink=True)
    points=[(s*.115,-.18,1.085),(s*.116,-.185,1.17),(s*.122,-.184,1.28),(s*.136,-.121,1.42),(s*.15,-.05,1.515),(s*.15,.035,1.525),(s*.15,.13,1.48),(s*.12,.227,1.40),(s*.10,.224,1.30),(-s*.078,.19,1.09)]
    vs=[]
    for x,y,z in points:
        for offset in [-.0145,.0145]:
            p=Vector((x+offset,y,z))
            if y<-.06:
                hit=bvh.ray_cast(Vector((p.x,-1,p.z)),Vector((0,1,0)),2)[0]
                if hit:p.y=min(p.y,hit.y-.012)
            elif y>.14:
                hit=bvh.ray_cast(Vector((p.x,1,p.z)),Vector((0,-1,0)),2)[0]
                if hit:p.y=max(p.y,hit.y+.007)
            else:
                hit=bvh.ray_cast(Vector((p.x,p.y,1.8)),Vector((0,0,-1)),1)[0]
                if hit:p.z=max(p.z,hit.z+.006)
            vs.append(p)
    me=bpy.data.meshes.new('Suspensorio_continuo.'+side);me.from_pydata(vs,[],[(i*2,i*2+1,i*2+3,i*2+2) for i in range(len(points)-1)]);me.update()
    ob=bpy.data.objects.new('FEB_Suspensorio.'+side,me);char.objects.link(ob);me.materials.append(mat)
    for v in me.vertices:
        t=max(0,min(1,(v.co.z-1.08)/.30));group(ob,'Spine',v.index,1-t);group(ob,'UpperChest',v.index,t)
    activate(ob);sol=ob.modifiers.new('Lona','SOLIDIFY');sol.thickness=.002;sol.offset=0;bpy.ops.object.modifier_apply(modifier=sol.name);attach(ob)

cam=scene.camera
def render(name,loc,target,scale):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale;scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
render('FEB_tres_quartos',(2.6,-4,2.5),(0,0,.94),2.09)
render('FEB_costas',(-2.3,4,2.1),(0,0,.94),2.09)
render('FEB_emblema_detalhe',(1.0,-.5,2.3),(.31,.02,1.49),.32)
old={p.name:p.matrix_basis.copy() for p in rig.pose.bones}
for side,s in [('L',1),('R',-1)]:
    b=rig.pose.bones['UpperArm.'+side];pivot=b.head.copy();b.matrix=Matrix.Translation(pivot)@Matrix.Rotation(s*math.radians(68),4,'Y')@Matrix.Translation(-pivot)@b.matrix;bpy.context.view_layer.update()
render('FEB_pose_demonstracao',(2.4,-4,1.95),(0,0,.94),2.04)
for n,m in old.items():rig.pose.bones[n].matrix_basis=m
bpy.context.view_layer.update()
render('FEB_frente',(0,-4,1.04),(0,0,.9),2.09)
cam.location=(2.6,-4,2.5);cam.rotation_euler=(Vector((0,0,.94))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.09
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FEB_Soldado_BaseGLB.blend'))
result={'saved':bpy.data.filepath,'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in char.objects if o.type=='MESH')}
