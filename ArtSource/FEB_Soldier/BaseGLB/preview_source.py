import bpy, math, json
from mathutils import Vector
from pathlib import Path
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/BaseGLB')
scene=bpy.data.scenes['FEB_BaseGLB']
for win in bpy.context.window_manager.windows:
    win.scene=scene
deps=bpy.context.evaluated_depsgraph_get()
meshes=[o for o in scene.objects if o.type=='MESH' and o.name.startswith('Object_')]
points=[o.matrix_world@v.co for o in meshes for v in o.evaluated_get(deps).data.vertices]
low=Vector([min(v[i] for v in points) for i in range(3)])
high=Vector([max(v[i] for v in points) for i in range(3)])
center=(low+high)/2
height=high.z-low.z
studio=bpy.data.collections.new('Studio_FEB')
scene.collection.children.link(studio)
def add_light(name,loc,power,size):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);studio.objects.link(ob);ob.location=loc
    ob.rotation_euler=(center-ob.location).to_track_quat('-Z','Y').to_euler()
    return ob
add_light('Key',(center.x-4,-6,7),1100,5)
add_light('Fill',(center.x+5,-3,4),700,4)
add_light('Rim',(0,4,6),1400,3)
cam=bpy.data.objects.new('Camera_FEB',bpy.data.cameras.new('Camera_FEB'));studio.objects.link(cam);scene.camera=cam
cam.data.type='ORTHO';cam.data.ortho_scale=height*1.14
world=bpy.data.worlds.new('World_FEB');world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.15,.17,.2,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.6;scene.world=world
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
cam.location=center+Vector((0,-height*2,.1));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(out/'original_frente.png')
bpy.ops.render.render(write_still=True)
arm=next(o for o in scene.objects if o.type=='ARMATURE')
result={'bounds_evaluated':[list(low),list(high)],'bones':len(arm.data.bones),'meshes':len(meshes),'triangles':sum(len(o.data.polygons) for o in meshes),'pose_bones_sample':[{'name':b.name,'head':list(arm.matrix_world@b.head),'tail':list(arm.matrix_world@b.tail)} for b in arm.pose.bones if b.name in ['Bone_45','Bone.002_43','Bone.003_4','Bone.004_3','Bone.005_23','Bone.006_22','Bone.007_21','Bone.024_42','Bone.025_41','Bone.026_40','Bone.040_54','Bone.044_49','Bone.048_53']]}
(out/'source_pose.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
