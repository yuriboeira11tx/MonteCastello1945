import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
s=bpy.data.scenes['FEB_Soldiers']
checks={}
for idx in range(1,7):
    o=bpy.data.objects['FEB_%02d_Emblema_FEB'%idx];me=o.data;me.calc_loop_triangles()
    uv=me.uv_layers.active
    deg=0;ratios=[];normals=[]
    for t in me.loop_triangles:
        a,b,c=[me.vertices[i].co for i in t.vertices]
        u,v,w=[uv.data[l].uv for l in t.loops]
        ua=abs((v-u).cross(w-u))/2;area=(b-a).cross(c-a).length/2
        if area<1e-10:deg+=1
        if ua>1e-10:ratios.append(area/ua)
        normals.append(t.normal.x)
    checks[o.name]={'vertices':len(me.vertices),'degenerate_triangles':deg,'area_scale_range':[min(ratios),max(ratios)],'bounds':[list(v) for v in o.bound_box],'uv_range':[[min(x.uv[k] for x in uv.data),max(x.uv[k] for x in uv.data)] for k in range(2)]}
view=bpy.data.scenes.new('Conferencia_emblema_esquerdo');bpy.context.window.scene=view
idx=3
for ob in bpy.data.collections['FEB_%02d'%idx].objects:view.collection.objects.link(ob)
view.world=s.world
for ob in s.objects:
    if ob.type=='LIGHT':view.collection.objects.link(ob)
o=bpy.data.objects['FEB_%02d_Emblema_FEB'%idx]
center=sum((o.matrix_world@v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
normal=Vector((.86,-.12,.5)).normalized()
d=bpy.data.cameras.new('Camera_Emblema_FEB');cam=bpy.data.objects.new(d.name,d);view.collection.objects.link(cam);view.camera=cam
cam.location=center+normal*.6;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=.14
view.render.engine='CYCLES';view.cycles.samples=24;view.cycles.use_denoising=True;view.view_settings.view_transform='AgX'
view.render.resolution_x=1000;view.render.resolution_y=1000;view.render.resolution_percentage=100;view.render.image_settings.file_format='PNG';view.render.filepath=str(out/'Emblema_esquerdo_antes.png')
bpy.ops.render.render(write_still=True)
bpy.context.window.scene=s
(out/'patch_inspection.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
result=checks
