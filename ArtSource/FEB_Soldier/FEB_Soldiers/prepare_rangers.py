import bpy, json, shutil
from pathlib import Path
from mathutils import Vector, Matrix
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
source=bpy.data.scenes['FEB_Soldiers']
scene=bpy.data.scenes.new('FEB_Soldiers')
bpy.context.window.scene=scene
assets=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldiers')
tex=assets/'Textures'; tex.mkdir(parents=True,exist_ok=True)
src=Path('C:/Users/yurib/Downloads/us-ranger-infantry-call-of-duty-2-hd/textures')
image_map={}
for p in src.glob('*.png'):
    target=tex/p.name.replace('#','')
    shutil.copy2(p,target)
    key=p.stem.replace('_4X','')
    image_map[key]=bpy.data.images.load(str(target),check_existing=True)
materials={}
def mat_for(old):
    ims=[n.image for n in old.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
    key=ims[0].name.split('.dds')[0] if ims else 'untextured'
    if key in materials:return materials[key]
    m=bpy.data.materials.new('FEB_Soldiers_'+key.replace('#',''));m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.84
    bs.inputs['Specular IOR Level'].default_value=.25
    if key in image_map:
        n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=image_map[key]
        m.node_tree.links.new(n.outputs['Color'],bs.inputs['Base Color'])
        if key=='ranger_markings_#0':m.node_tree.links.new(n.outputs['Alpha'],bs.inputs['Alpha'])
    materials[key]=m
    return m
centers=[0,5.59,11.09,16.53,21.98,27.55]
cols=[]
for i in range(6):
    c=bpy.data.collections.new('FEB_%02d'%(i+1));scene.collection.children.link(c);cols.append(c)
records=[]
for original in source.objects:
    if original.type!='MESH':continue
    world=[original.matrix_world@Vector(v) for v in original.bound_box]
    cx=sum(p.x for p in world)/8
    idx=min(range(6),key=lambda i:abs(centers[i]-cx))
    o=original.copy();o.data=original.data.copy();o.name='FEB_%02d_%s'%(idx+1,original.name)
    o['source_object']=original.name;o['variant']=idx+1
    cols[idx].objects.link(o)
    o.data.transform(Matrix.Scale(.247,4)@Matrix.Translation(Vector((-centers[idx],0,0)))@o.matrix_world)
    o.matrix_world=Matrix.Translation(Vector(((idx-2.5)*1.4,0,0)))
    for j,m in enumerate(o.data.materials):o.data.materials[j]=mat_for(m)
    records.append({'name':o.name,'variant':idx+1,'source':original.name})
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
world=bpy.data.worlds.new('FEB_Soldiers_Studio');world.use_nodes=True
world.node_tree.nodes.get('Background').inputs[0].default_value=(.1,.12,.16,1)
world.node_tree.nodes.get('Background').inputs[1].default_value=.45;scene.world=world
studio=bpy.data.collections.new('Studio_Rangers');scene.collection.children.link(studio)
def area(name,loc,power,size):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
area('Key_Rangers',(-3,-4,6),1500,7)
area('Fill_Rangers',(4,-1,4),1000,6)
area('Rim_Rangers',(0,3,5),1600,6)
d=bpy.data.cameras.new('Camera_Rangers');cam=bpy.data.objects.new('Camera_Rangers',d);studio.objects.link(cam);scene.camera=cam
cam.location=(0,-12,3.5);target=Vector((0,0,.9));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=8.7
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=2700;scene.render.resolution_y=850;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(out/'Rangers_antes_frente.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
bpy.ops.render.render(write_still=True)
cam.location=(0,12,3.5);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(out/'Rangers_antes_costas.png');bpy.ops.render.render(write_still=True)
(out/'variants.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
result={'variants':6,'meshes':len(records),'armatures':sum(o.type=='ARMATURE' for o in source.objects),'textures':list(image_map)}
