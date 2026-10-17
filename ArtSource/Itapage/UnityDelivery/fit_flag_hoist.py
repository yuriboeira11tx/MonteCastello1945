import bpy
from mathutils import Vector
scene=bpy.context.window.scene
pole=bpy.data.objects['Bandeira | pau de bandeira da popa']
points=[pole.matrix_world@v.co for v in pole.data.vertices]
mid=(max(v.z for v in points)+min(v.z for v in points))*.5
ends=[]
for half in ([v for v in points if v.z<mid],[v for v in points if v.z>mid]):
    ends.append(sum(half,Vector())/len(half))
bottom,top=ends
for name in ['Brasil_Bandeira_Animada','Brasil_Bandeira']:
    ob=bpy.data.objects[name]
    if ob.get('HoistFitted'):continue
    keys=ob.data.shape_keys.key_blocks
    for i,point in enumerate(keys[0].data):
        u=i//45/64
        hoist_z=keys[0].data[i%45].co.z
        x=bottom.x+(top.x-bottom.x)*(hoist_z-bottom.z)/(top.z-bottom.z)
        delta=(x+.005-(-58.47))*(1-u)
        for key in keys:key.data[i].co.x+=delta
    ob['HoistFitted']=True;ob.data.update()
for name in ['Brasil_Bandeira_Animada','Brasil_Bandeira']:
    ob=bpy.data.objects[name]
    for vertex,point in zip(ob.data.vertices,ob.data.shape_keys.key_blocks[0].data):vertex.co=point.co.copy()
    ob.data.update()
if not bpy.data.scenes.get('ITAPAGE | Unity Export'):
    export=bpy.data.scenes.new('ITAPAGE | Unity Export')
    for name in ['Brasil_Bandeira','Itapage_Detalhe0','Itapage_Detalhe1','Itapage_Detalhe2']:
        export.collection.objects.link(bpy.data.objects[name])
exec(compile(open('E:/Unity/MonteCastello1945/ArtSource/Itapage/UnityDelivery/reexport_flag.py',encoding='utf-8').read(),'reexport_flag.py','exec'))
# The delivery file already holds the bake/export scenes; keep the authoring file clean.
bpy.context.window.scene=scene
for name in ['ITAPAGE | Bake PBR','ITAPAGE | Unity Export']:
    temp=bpy.data.scenes.get(name)
    if temp and temp!=scene:bpy.data.scenes.remove(temp)
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath='E:/Unity/MonteCastello1945/ArtSource/Itapage/Itapage.blend')
print('Hoist fitted to the inclined pole in authoring and Unity FBX; authoring saved')
