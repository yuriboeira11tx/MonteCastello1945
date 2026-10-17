import bpy,bmesh,json
import numpy as np
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
scene=bpy.data.scenes['FEB_Soldiers'];bpy.context.window.scene=scene
archive=bpy.data.collections['Acessorios_Rangers_retirados']
comps={r['object']:r['components'] for r in json.loads((out/'components.json').read_text())}
orig={int(n[4:6]):bpy.data.objects[n] for n in comps if 'DrawCall_0_DrawCall_0' in n}
def hide(o):
    for c in list(o.users_collection):c.objects.unlink(o)
    archive.objects.link(o);o.hide_render=True
def subset(source,pids,idx,name):
    o=source.copy();o.data=source.data.copy();o.name=name;o.hide_render=False;o.hide_viewport=False
    bpy.data.collections['FEB_%02d'%idx].objects.link(o);o.location=((idx-3.5)*1.4,0,0);o['variant']=idx
    bm=bmesh.new();bm.from_mesh(o.data);bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.index not in pids],context='FACES')
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
    bm.to_mesh(o.data);bm.free();o.data.update();return o
def badge(c):return c['uv'] and c['uv'][0][0]>.085 and c['uv'][1][0]<.125 and c['uv'][0][1]>.95
def offset(donor,target):
    # Collar position uses the shared shirt/neck UV islands, without equipment correspondence.
    def center(o):
        ids=set(v for c in comps[o.name] if c['uv'] and .44<c['uv'][0][0]<.5 and c['bounds'][0][2]>1.4 for p in c['polygon_ids'] for v in o.data.polygons[p].vertices)
        return sum((o.data.vertices[i].co for i in ids),Vector())/len(ids)
    return center(target)-center(donor)
for idx,donor_idx in [(3,4),(5,6)]:
    target=orig[idx];donor=orig[donor_idx]
    for o in list(bpy.data.collections['FEB_%02d'%idx].objects):
        if o.name in ['FEB_%02d_Corpo_preservado'%idx,'FEB_%02d_Jaqueta_M41'%idx,'FEB_%02d_Cinturao_lona'%idx]:hide(o)
    ids=set(p for c in comps[donor.name] if c['bounds'][0][2]>.75 and not badge(c) for p in c['polygon_ids'])
    top=subset(donor,ids,idx,'FEB_%02d_M41_parte_superior'%idx)
    delta=offset(donor,target)
    for v in top.data.vertices:v.co+=delta
    # Reuse complete matching sleeves: no open armholes and no interpolation distortion.
    low_donor=orig[6] if idx==3 else target
    ids=set(p for c in comps[low_donor.name] if c['bounds'][1][2]<.95 for p in c['polygon_ids'])
    subset(low_donor,ids,idx,'FEB_%02d_Calca_perneiras_botinas'%idx)
    belt_source=next(o for o in scene.objects if o.get('variant')==donor_idx and o.get('source_object')==('DrawCall_2_DrawCall_2.025' if donor_idx==4 else 'DrawCall_1_DrawCall_1.027') and archive not in o.users_collection)
    belt=subset(belt_source,set(range(len(belt_source.data.polygons))),idx,'FEB_%02d_Cinturao_M1923'%idx)
    for v in belt.data.vertices:v.co+=delta
    # Refitting each patch to its new sleeve is done below.
    top['collar_offset']=list(delta)
# The waterproof gas-mask carrier and its leg fastening are not part of the selected campaign loadout.
target=orig[2];current=bpy.data.objects['FEB_02_Corpo_uniforme'];hide(current)
ids=set(p for c in comps[target.name] if c['bounds'][0][2]>.75 and not badge(c) for p in c['polygon_ids'])
subset(target,ids,2,'FEB_02_M41_parte_superior')
donor=orig[6];ids=set(p for c in comps[donor.name] if c['bounds'][1][2]<.95 for p in c['polygon_ids'])
subset(donor,ids,2,'FEB_02_Calca_perneiras_botinas')
# Project replacement-variant emblems directly onto the final sleeves.
from mathutils.bvhtree import BVHTree
for idx in [2,3,5]:
    top=bpy.data.objects['FEB_%02d_M41_parte_superior'%idx]
    bvh=BVHTree.FromPolygons([v.co.copy() for v in top.data.vertices],[list(p.vertices) for p in top.data.polygons])
    for key,side in [('FEB',1),('FifthArmy',-1)]:
        o=bpy.data.objects['FEB_%02d_Emblema_%s'%(idx,key)]
        n=Vector((side*.86,-.12,.5)).normalized()
        for v in o.data.vertices:
            hit,normal,face,d=bvh.ray_cast(v.co+n*.15,-n,.4)
            if hit is None:hit,normal,face,d=bvh.find_nearest(v.co)
            v.co=hit+normal*.0014
        o.data.update()
scene.render.resolution_x=2700;scene.render.resolution_y=850
cam=scene.camera;cam.data.ortho_scale=8.7
cam.location=(0,-12,3.2);cam.rotation_euler=(Vector((0,0,.92))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(out/'FEB_Soldiers_frente.png');bpy.ops.render.render(write_still=True)
cam.location=(0,12,3.2);cam.rotation_euler=(Vector((0,0,.92))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(out/'FEB_Soldiers_costas.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result={'refined_variants':[2,3,5],'note':'Complete upper M41 assemblies and clean trousers from original variants, faces unchanged.'}
