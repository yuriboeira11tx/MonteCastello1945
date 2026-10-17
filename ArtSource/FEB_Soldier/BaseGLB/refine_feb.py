import bpy, math, json
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
OUT=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/BaseGLB')
scene=bpy.data.scenes['FEB_BaseGLB'];bpy.context.window.scene=scene
char=bpy.data.collections['FEB_Personagem_BaseGLB'];rig=bpy.data.objects['FEB_Rig_BaseGLB']
# Reduce coarse colour noise on new cloth/paint while keeping the fine bump.
for mat in bpy.data.materials:
    if not mat.use_nodes:continue
    ramps=[n for n in mat.node_tree.nodes if n.type=='VALTORGB']
    tex=[n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE']
    if ramps and not tex:
        r=ramps[0].color_ramp
        target=[c/1.42 for c in r.elements[1].color[:3]]
        r.elements[0].color=tuple(c*.88 for c in target)+(1,)
        r.elements[1].color=tuple(c*1.10 for c in target)+(1,)

# Existing shoulder tabs cover the same surface needed by the FEB badges.
ob=bpy.data.objects.get('FEB_Detalhes_mangas')
if ob:bpy.data.objects.remove(ob,do_unlink=True)

# Pocket and button meshes follow the retained jacket rather than replacing it.
jacket=bpy.data.objects['FEB_Gandola']
bvh=BVHTree.FromPolygons([v.co.copy() for v in jacket.data.vertices],[tuple(p.vertices) for p in jacket.data.polygons])
def front(x,z,offset=.004):
    p=bvh.ray_cast(Vector((x,-1,z)),Vector((0,1,0)),2)[0]
    return (p.y if p else -.16)-offset
def attach(ob,bone='UpperChest'):
    for c in list(ob.users_collection):c.objects.unlink(ob)
    char.objects.link(ob);ob.parent=rig
    ob.vertex_groups.new(name=bone).add(list(range(len(ob.data.vertices))),1,'REPLACE')
    m=ob.modifiers.new('FEB_Rig','ARMATURE');m.object=rig
def pocket(name,x,z,w,h,flap=False):
    coords=[(-.5,-.5),(.5,-.5),(.5,.5),(-.5,.5)] if not flap else [(-.5,.35),(0,-.5),(.5,.35),(.5,.5),(-.5,.5)]
    vs=[(x+u*w,front(x+u*w,z+v*h,.010 if flap else .007),z+v*h) for u,v in coords]
    me=bpy.data.meshes.new(name);me.from_pydata(vs,[],[tuple(range(len(vs)-1,-1,-1))]);me.update()
    ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);me.materials.append(bpy.data.materials['FEB_Sarja_oliva'])
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    m=ob.modifiers.new('Costura_e_espessura','SOLIDIFY');m.thickness=.002;bpy.ops.object.modifier_apply(modifier=m.name)
    attach(ob)
    return ob
for side,s in [('L',1),('R',-1)]:
    pocket('FEB_Bolso_peito.'+side,s*.081,1.259,.097,.121)
    pocket('FEB_Aba_peito.'+side,s*.081,1.314,.101,.036,True)
    # Move the complete front strap section forward without collapsing thickness.
    ob=bpy.data.objects['FEB_Suspensorio.'+side]
    for v in ob.data.vertices:
        if v.co.y<-.06:
            w=max(0,min(1,(v.co.z-1.12)/.08,(1.43-v.co.z)/.08));v.co.y-=.019*w
    for ob in char.objects:
        if ob.type!='MESH' or not ob.name.endswith('.'+side):continue
        if any(t in ob.name for t in ['Perneira.','Debrum_perneira.','Cadarco_perneira.']):
            for v in ob.data.vertices:
                v.co.x=s*.116+(v.co.x-s*.116)*1.16
                v.co.y=.024+(v.co.y-.024)*1.13
                v.co.z=.128+(v.co.z-.115)*( .180/.172 )

# Discreet central button line with a separate placket.
vs=[]
for z in [.86,1.04,1.18,1.33,1.425]:
    for x in [-.008,.008]:vs.append((x,front(x,z,.004),z))
me=bpy.data.meshes.new('FEB_Vista_botoes');me.from_pydata(vs,[],[(i*2,i*2+1,i*2+3,i*2+2) for i in range(4)]);me.update()
ob=bpy.data.objects.new(me.name,me);scene.collection.objects.link(ob);me.materials.append(bpy.data.materials['FEB_Sarja_oliva']);attach(ob,'Chest')
for i,z in enumerate([.90,1.12,1.22,1.32,1.412]):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8,ring_count=4,location=(0,front(0,z,.007),z));ob=bpy.context.object;ob.name=f'FEB_Botao_gandola_{i+1}'
    ob.scale=(.0038,.002,.0038);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);ob.data.materials.append(bpy.data.materials['FEB_Metal_escurecido']);attach(ob,'Chest')

cam=scene.camera
def render(name,loc,target,scale):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
render('FEB_frente',(0,-4,1.04),(0,0,.9),2.09)
render('FEB_tres_quartos',(2.6,-4,2.5),(0,0,.94),2.09)
render('FEB_costas',(-2.3,4,2.1),(0,0,.94),2.09)
# Preview the existing skeleton with relaxed arms; restore T pose afterwards.
old={p.name:p.matrix_basis.copy() for p in rig.pose.bones}
for side,s in [('L',1),('R',-1)]:
    b=rig.pose.bones['UpperArm.'+side];pivot=b.head.copy()
    b.matrix=Matrix.Translation(pivot)@Matrix.Rotation(s*math.radians(68),4,'Y')@Matrix.Translation(-pivot)@b.matrix
    bpy.context.view_layer.update()
render('FEB_pose_demonstracao',(2.4,-4,1.95),(0,0,.94),2.04)
for name,m in old.items():rig.pose.bones[name].matrix_basis=m
bpy.context.view_layer.update()
render('FEB_emblema_detalhe',(1.0,-.5,2.3),(.31,.02,1.49),.32)
cam.location=(2.6,-4,2.5);cam.rotation_euler=(Vector((0,0,.94))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.09
for win in bpy.context.window_manager.windows:
    win.scene=scene
    for area in win.screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active;space.shading.type='MATERIAL';space.overlay.show_overlays=False
            space.region_3d.view_location=(0,0,.95);space.region_3d.view_distance=3.0;space.region_3d.view_rotation=cam.rotation_euler.to_quaternion();space.region_3d.view_perspective='ORTHO'
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FEB_Soldado_BaseGLB.blend'))
result={'file':bpy.data.filepath,'previews':['FEB_frente.png','FEB_tres_quartos.png','FEB_costas.png','FEB_pose_demonstracao.png','FEB_emblema_detalhe.png'],'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in char.objects if o.type=='MESH')}
