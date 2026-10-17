import bpy, math
from mathutils import Vector
g=bpy.app.driver_namespace['ITAPAGE_BUILD'];globals().update(g)
world=bpy.data.worlds.new('Itapage | Ceu maritimo');world.use_nodes=True;scene.world=world
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.35,.43,.55,1);bg.inputs[1].default_value=.45
def camera(name,pos,target,ortho=150):
    data=bpy.data.cameras.new(name);ob=bpy.data.objects.new(name,data);COL['08'].objects.link(ob);ob.location=pos
    ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=ortho;data.clip_end=1500;return ob
cam=camera('Camera | tres quartos',(87,-159,71),(0,0,9),150);scene.camera=cam
camera('Camera | perfil',(0,-180,13),(0,0,12),137)
camera('Camera | conves',(47,-53,47),(-1,0,10),66)
camera('Camera | proa',(116,-90,35),(25,0,8),87)
def light(name,kind,pos,power,color,size=80):
    d=bpy.data.lights.new(name,kind);o=bpy.data.objects.new(name,d);COL['08'].objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,5))-o.location).to_track_quat('-Z','Y').to_euler();d.energy=power;d.color=color
    if kind=='AREA':d.shape='DISK';d.size=size
    if kind=='SUN':d.angle=.12
    return o
light('Sol | luz lateral','SUN',(-35,-60,95),2.0,(1,.87,.71))
light('Ceu | preenchimento','AREA',(15,-45,65),45000,(.68,.8,1),80)
light('Recorte | horizonte','AREA',(-25,50,55),85000,(.88,.93,1),95)
try:scene.render.engine='CYCLES'
except TypeError as e:print(e)
scene.cycles.samples=24;scene.cycles.use_denoising=True
prefs=bpy.context.preferences.addons.get('cycles')
if prefs:
    cp=prefs.preferences
    try:
        cp.compute_device_type='OPTIX';cp.get_devices()
        for d in cp.devices:d.use=d.type!='CPU'
        if any(d.use for d in cp.devices):scene.cycles.device='GPU'
        print('DEVICES',[(d.name,d.type,d.use) for d in cp.devices])
    except Exception as e:print('GPU',str(e))
scene.render.resolution_x=1400;scene.render.resolution_y=850;scene.render.resolution_percentage=70
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(OUT/'Itapage_WIP.png');scene.render.film_transparent=False
scene.view_settings.view_transform='AgX'
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.clip_end=2000;area.spaces.active.region_3d.view_distance=155
        area.spaces.active.region_3d.view_location=(0,0,8)
        area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
        area.spaces.active.overlay.show_floor=False
        area.spaces.active.shading.type='MATERIAL'
bpy.app.driver_namespace['ITAPAGE_BUILD']=globals().copy()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Itapage.blend'))
bpy.ops.render.render(write_still=True)
print('Preview',scene.render.filepath)
