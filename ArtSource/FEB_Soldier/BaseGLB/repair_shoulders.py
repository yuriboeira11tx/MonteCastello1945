import bpy,bmesh,math,json
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.interpolate import poly_3d_calc
from pathlib import Path
OUT=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/BaseGLB')
scene=bpy.data.scenes['FEB_BaseGLB'];bpy.context.window.scene=scene
char=bpy.data.collections['FEB_Personagem_BaseGLB'];rig=bpy.data.objects['FEB_Rig_BaseGLB']
with bpy.data.libraries.load(str(OUT/'Base_TPose.blend'),link=False) as (src,dst):dst.objects=['FEB_Detalhes_mangas']
closure=dst.objects[0];char.objects.link(closure)
closure.parent=rig;closure.matrix_world=Matrix.Identity(4)
for m in closure.modifiers:
    if m.type=='ARMATURE':m.object=rig
mapping={'Bone.005_23':'Shoulder.L','Bone.006_22':'UpperArm.L','Bone.007_21':'Forearm.L','Bone.024_42':'Shoulder.R','Bone.025_41':'UpperArm.R','Bone.026_40':'Forearm.R','Bone.002_43':'UpperChest','Bone.001_44':'Chest','Bone_45':'Spine','Bone.040_54':'Hips'}
for g in closure.vertex_groups:
    if g.name in mapping:g.name=mapping[g.name]
closure.data.materials.clear();closure.data.materials.append(bpy.data.materials['FEB_Mangas_textura'])
bpy.ops.object.select_all(action='DESELECT')
parts=[closure,bpy.data.objects['FEB_Mangas'],bpy.data.objects['FEB_Gandola']]
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[-1];bpy.ops.object.join();shirt=bpy.context.object;shirt.name='FEB_Uniforme_superior'
bm=bmesh.new();bm.from_mesh(shirt.data);before=len(bm.verts)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00003);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(shirt.data);bm.free()
for v in shirt.data.vertices:
    x=abs(v.co.x)
    if .135<x<.34 and v.co.z>1.25:
        t=max(0,min(1,(x-.145)/.155));t=t*t*(3-2*t)
        for g in shirt.vertex_groups:g.remove([v.index])
        for name,w in [('UpperChest',1-t),('UpperArm.'+('L' if v.co.x>0 else 'R'),t)]:
            if w>0:
                g=shirt.vertex_groups.get(name) or shirt.vertex_groups.new(name=name);g.add([v.index],w,'REPLACE')
shirt['source_object']='Object_7 + Object_16 + Object_18; UV seam vertices welded'
shirt.data.update()
bvh=BVHTree.FromPolygons([v.co.copy() for v in shirt.data.vertices],[tuple(p.vertices) for p in shirt.data.polygons])
hits=[]
for name in ['FEB_Emblema_esquerdo','FEB_5Exercito_direito']:
    ob=bpy.data.objects[name]
    # Recover the original front UV surface, discard only the temporary thickness.
    bm=bmesh.new();bm.from_mesh(ob.data)
    # Project both thin layers coherently; their difference is negligible here.
    for v in ob.data.vertices:
        hit,normal,faceid,dist=bvh.ray_cast(Vector((v.co.x,v.co.y,1.75)),Vector((0,0,-1)),.6)
        if hit:
            v.co=hit+normal*.0022
            poly=shirt.data.polygons[faceid]
            bary=poly_3d_calc([shirt.data.vertices[i].co for i in poly.vertices],hit)
            ws={}
            for idx,w in zip(poly.vertices,bary):
                for g in shirt.data.vertices[idx].groups:
                    bn=shirt.vertex_groups[g.group].name
                    if bn in rig.data.bones:ws[bn]=ws.get(bn,0)+g.weight*max(0,w)
            for g in ob.vertex_groups:g.remove([v.index])
            total=sum(ws.values())
            for bn,w in ws.items():
                if w>0:
                    g=ob.vertex_groups.get(bn) or ob.vertex_groups.new(name=bn);g.add([v.index],w/total,'REPLACE')
            hits.append(v.co.z)
    bm.free();ob.data.update()

# Rebuild the same M1 shell with a welded circular seam before adding thickness.
old=bpy.data.objects['FEB_Capacete_M1'];mat=old.data.materials[0];bpy.data.objects.remove(old,do_unlink=True)
vs=[];fs=[];N=32
for rx,ry,z,fade in [(.122,.145,1.668,1),(.118,.139,1.700,.5),(.107,.124,1.751,.1),(.083,.094,1.787,0),(.045,.05,1.806,0)]:
    for k in range(N):
        a=2*math.pi*k/N;vs.append((rx*math.cos(a),.01+ry*math.sin(a),z+fade*(-.028*math.sin(a)+.003*math.cos(2*a))))
for j in range(4):
    for k in range(N):fs.append((j*N+k,j*N+(k+1)%N,(j+1)*N+(k+1)%N,(j+1)*N+k))
vs.append((0,.01,1.813))
for k in range(N):fs.append((4*N+k,4*N+(k+1)%N,5*N))
me=bpy.data.meshes.new('FEB_M1_continuo');me.from_pydata(vs,[],fs);me.update();ob=bpy.data.objects.new('FEB_Capacete_M1',me);char.objects.link(ob);me.materials.append(mat)
bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
for p in me.polygons:p.use_smooth=True
bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
mod=ob.modifiers.new('Chapa','SOLIDIFY');mod.thickness=.0025;bpy.ops.object.modifier_apply(modifier=mod.name)
ob.vertex_groups.new(name='Head').add(list(range(len(me.vertices))),1,'REPLACE');ob.parent=rig;mod=ob.modifiers.new('FEB_Rig','ARMATURE');mod.object=rig

cam=scene.camera
def render(name,loc,target,scale):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
render('FEB_tres_quartos',(2.6,-4,2.5),(0,0,.94),2.09)
render('FEB_emblema_detalhe',(1.0,-.5,2.3),(.31,.02,1.49),.32)
old={p.name:p.matrix_basis.copy() for p in rig.pose.bones}
for side,s in [('L',1),('R',-1)]:
    b=rig.pose.bones['UpperArm.'+side];pivot=b.head.copy();b.matrix=Matrix.Translation(pivot)@Matrix.Rotation(s*math.radians(68),4,'Y')@Matrix.Translation(-pivot)@b.matrix;bpy.context.view_layer.update()
render('FEB_pose_demonstracao',(2.4,-4,1.95),(0,0,.94),2.04)
for name,m in old.items():rig.pose.bones[name].matrix_basis=m
bpy.context.view_layer.update()
render('FEB_frente',(0,-4,1.04),(0,0,.9),2.09)
render('FEB_costas',(-2.3,4,2.1),(0,0,.94),2.09)
cam.location=(2.6,-4,2.5);cam.rotation_euler=(Vector((0,0,.94))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.09
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FEB_Soldado_BaseGLB.blend'))
result={'shirt_vertices_before_weld':before,'after':len(shirt.data.vertices),'patch_z_range':[min(hits),max(hits)]}
