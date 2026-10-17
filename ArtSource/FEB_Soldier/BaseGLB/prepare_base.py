import bpy, json
from mathutils import Matrix, Vector
from pathlib import Path
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/BaseGLB')
scene=bpy.data.scenes['FEB_BaseGLB']
bpy.context.window.scene=scene
rig=next(o for o in scene.objects if o.type=='ARMATURE')
meshes=[o for o in scene.objects if o.type=='MESH' and o.name.startswith('Object_')]
for pb in rig.pose.bones: pb.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
deps=bpy.context.evaluated_depsgraph_get()
points=[o.matrix_world@v.co for o in meshes for v in o.evaluated_get(deps).data.vertices]
lo=Vector([min(v[i] for v in points) for i in range(3)]);hi=Vector([max(v[i] for v in points) for i in range(3)])
scale=1.80/(hi.z-lo.z)
hip=rig.matrix_world@rig.pose.bones['Bone_45'].head
normal=Matrix.Diagonal((scale,scale,scale,1))@Matrix.Translation(Vector((-hip.x,-hip.y,-lo.z)))
saved={o:o.matrix_world.copy() for o in meshes+[rig]}
for ob in meshes+[rig]:
    ob.parent=None;ob.matrix_world=saved[ob]
for ob in meshes+[rig]:
    mat=normal@saved[ob]
    ob.data.transform(mat)
    ob.matrix_world=Matrix.Identity(4)
collection=bpy.data.collections.new('FEB_Personagem_BaseGLB');scene.collection.children.link(collection)
for ob in meshes+[rig]:
    for c in list(ob.users_collection):c.objects.unlink(ob)
    collection.objects.link(ob)
for ob in meshes:ob.parent=rig
rig.name='FEB_Rig_BaseGLB';rig.show_in_front=True
labels={7:'Mangas',8:'Capacete_original',9:'Olhos',10:'Maos',11:'Jugular_original',12:'Quadril',13:'Calca',14:'Botas',15:'Equipamento_original',16:'Gandola',17:'Rosto',18:'Detalhes_mangas',19:'Gola_interna'}
for ob in meshes:
    idx=int(ob.name.split('_')[1]);ob.name='FEB_'+labels[idx]
    ob['source_object']='Object_'+str(idx)
    for p in ob.data.polygons:p.use_smooth=True
    for m in ob.data.materials:
        m.use_backface_culling=False
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
# Restore frontal camera and lights at character scale.
center=Vector((0,0,.9));cam=scene.camera;cam.data.ortho_scale=2.1
cam.location=(0,-4,.95);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size in [('Key',(-3,-4,4),500,4),('Fill',(3,-2,2.7),300,3),('Rim',(1,3,3),650,3)]:
    ob=bpy.data.objects[name];ob.location=loc;ob.rotation_euler=(center-ob.location).to_track_quat('-Z','Y').to_euler();ob.data.energy=power;ob.data.size=size
scene.render.filepath=str(out/'base_tpose.png');bpy.ops.render.render(write_still=True)
def bounds(ob):
    vs=[ob.matrix_world@v.co for v in ob.data.vertices]
    return [[round(min(v[i] for v in vs),4) for i in range(3)],[round(max(v[i] for v in vs),4) for i in range(3)]]
result={'scale':scale,'objects':[{'name':o.name,'bounds':bounds(o),'material':o.data.materials[0].name} for o in meshes], 'bones':[{'name':b.name,'parent':b.parent.name if b.parent else None,'head':[round(v,4) for v in b.head_local],'tail':[round(v,4) for v in b.tail_local]} for b in rig.data.bones]}
(out/'normalized.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Base_TPose.blend'))
