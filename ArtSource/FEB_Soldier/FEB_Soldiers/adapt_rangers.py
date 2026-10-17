import bpy,bmesh,json,shutil,math
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
scene=bpy.data.scenes['FEB_Soldiers'];bpy.context.window.scene=scene
reports=json.loads((out/'components.json').read_text())
comps={r['object']:r['components'] for r in reports}
main={o['variant']:o for o in scene.objects if o.type=='MESH' and 'DrawCall_0_DrawCall_0' in o.name}
archive=bpy.data.collections.new('Acessorios_Rangers_retirados');scene.collection.children.link(archive)
archive.hide_render=True;archive.hide_viewport=True
changes=[]
def hide(o,reason):
    for c in list(o.users_collection):c.objects.unlink(o)
    archive.objects.link(o);o.hide_render=True
    changes.append({'object':o.name,'action':'archived','reason':reason})
def subset(o,pids,name):
    obj=o.copy();obj.data=o.data.copy();obj.name=name
    for c in o.users_collection:
        if c!=archive:c.objects.link(obj)
    bm=bmesh.new();bm.from_mesh(obj.data);bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.index not in pids],context='FACES')
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    return obj
def torso(c):
    a,b=c['bounds'];return a[2]>.75 and a[0]>-.35 and b[0]<.35
def badge(c):
    return c['uv'] and c['uv'][0][0]>.085 and c['uv'][1][0]<.125 and c['uv'][0][1]>.95
badge_data={}
for i,o in main.items():
    ids=[p for c in comps[o.name] if badge(c) for p in c['polygon_ids']]
    points=[o.data.vertices[v].co.copy() for j in ids for v in o.data.polygons[j].vertices]
    normal=sum((o.data.polygons[j].normal*o.data.polygons[j].area for j in ids),Vector())
    badge_data[i]=(sum(points,Vector())/len(points),normal.normalized())
def uv_pairs(donor,target):
    lookup={}
    for p in target.data.polygons:
        for l in p.loop_indices:
            v=target.data.vertices[target.data.loops[l].vertex_index].co
            if v.z<.25 or abs(v.x)>.38:continue
            uv=target.data.uv_layers.active.data[l].uv
            lookup.setdefault(tuple(round(x,4) for x in uv),[]).append(v.copy())
    pairs=[];seen=set()
    for p in donor.data.polygons:
        for l in p.loop_indices:
            vi=donor.data.loops[l].vertex_index
            if vi in seen:continue
            v=donor.data.vertices[vi].co
            uv=tuple(round(x,4) for x in donor.data.uv_layers.active.data[l].uv)
            if uv not in lookup:continue
            t=min(lookup[uv],key=lambda x:(x-v).length)
            if (t-v).length<.19:
                seen.add(vi);pairs.append((list(v),list(t-v)))
    return np.array([p[0] for p in pairs]),np.array([p[1] for p in pairs])
def warp(obj,pts,delta):
    for v in obj.data.vertices:
        dist=np.sum((pts-np.array(v.co))**2,axis=1)
        ix=np.argsort(dist)[:18];w=1/(dist[ix]+.00006)**1.7
        v.co+=Vector(np.sum(delta[ix]*w[:,None],axis=0)/sum(w))
    obj.data.update()
originals=dict(main)
for idx,donor_id in [(3,4),(5,6)]:
    target=originals[idx];donor=originals[donor_id]
    removed=set(p for c in comps[target.name] if torso(c) for p in c['polygon_ids'])
    keep=set(range(len(target.data.polygons)))-removed
    body=subset(target,keep,'FEB_%02d_Corpo_preservado'%idx)
    donor_ids=set(p for c in comps[donor.name] if torso(c) and not badge(c) for p in c['polygon_ids'])
    jacket=subset(donor,donor_ids,'FEB_%02d_Jaqueta_M41'%idx)
    for c in list(jacket.users_collection):c.objects.unlink(jacket)
    bpy.data.collections['FEB_%02d'%idx].objects.link(jacket)
    jacket['variant']=idx;jacket['source_object']=donor['source_object'];jacket.location=target.location
    pts,deltas=uv_pairs(donor,target);warp(jacket,pts,deltas)
    # Fit and reuse a cartridge belt from a compatible variant of the same supplied file.
    belt_source=next(o for o in scene.objects if o.get('variant')==donor_id and 'DrawCall_2_DrawCall_2' in o.get('source_object','')) if donor_id==4 else next(o for o in scene.objects if o.get('variant')==6 and 'DrawCall_1_DrawCall_1' in o.get('source_object',''))
    belt=subset(belt_source,set(range(len(belt_source.data.polygons))),'FEB_%02d_Cinturao_lona'%idx)
    for c in list(belt.users_collection):c.objects.unlink(belt)
    bpy.data.collections['FEB_%02d'%idx].objects.link(belt);belt.location=target.location;belt['variant']=idx
    warp(belt,pts,deltas)
    hide(target,'Colete de assalto integrado substituido por jaqueta M41 do proprio conjunto; cabeca, membros e pose preservados.')
    main[idx]=body
    for o in list(bpy.data.collections['FEB_%02d'%idx].objects):
        src=o.get('source_object','')
        if (idx==3 and src=='DrawCall_1_DrawCall_1.024') or (idx==5 and src in ['DrawCall_1_DrawCall_1.026','DrawCall_2_DrawCall_2.026','DrawCall_8_DrawCall_8.007','DrawCall_9_DrawCall_9.006']):
            hide(o,'Acessorio associado ao conjunto com colete de assalto; substituido pelo equipamento de infantaria de referencia.')
    changes.append({'variant':idx,'action':'M41_torso_replacement','donor_variant':donor_id,'uv_controls':len(pts)})
# Remove original American rank/unit decal meshes and the integrated Ranger diamond on sleeves.
for o in list(scene.objects):
    if o.type!='MESH' or archive in o.users_collection:continue
    if any('ranger_markings' in m.name for m in o.data.materials if m):hide(o,'Divisas americanas e identificacao do 2nd Rangers no capacete.')
for idx,o in list(main.items()):
    # Vest variants already lost the integrated shoulder tag during torso removal.
    if idx in (3,5):continue
    remove=set(p for c in comps[o.name] if badge(c) for p in c['polygon_ids'])
    new=subset(o,set(range(len(o.data.polygons)))-remove,'FEB_%02d_Corpo_uniforme'%idx)
    hide(o,'Copia anterior conservada; pequeno emblema Rangers da manga removido.')
    main[idx]=new
tex=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldiers/Textures')
patchmats={}
for key in ['FEB','FifthArmy']:
    p=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldier/Textures')/(key+'_Patch.png')
    dest=tex/p.name;shutil.copy2(p,dest)
    im=bpy.data.images.load(str(dest),check_existing=True)
    m=bpy.data.materials.new('FEB_Soldiers_'+key+'_Emblema');m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.92;bs.inputs['Specular IOR Level'].default_value=.16
    n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=im;m.node_tree.links.new(n.outputs['Color'],bs.inputs['Base Color']);patchmats[key]=m
def make_patch(idx,side,key):
    body=main[idx];center,n=badge_data[idx];center=center.copy();n=n.copy()
    if n.x<0:n=-n
    # Position on the upper sleeve, following the original sleeve surface.
    if side<0:
        center.x=-center.x;n.x=-n.x
    n=Vector((side*.86,-.12,.5)).normalized()
    up=(Vector((0,0,1))-n*n.z).normalized();right=up.cross(n).normalized()
    width=.073 if key=='FEB' else .073;height=.089 if key=='FEB' else .072
    verts=[v.co.copy() for v in body.data.vertices];polys=[list(p.vertices) for p in body.data.polygons]
    bvh=BVHTree.FromPolygons(verts,polys)
    # Concentric tessellation keeps the silhouette of the unchanged reference PNG.
    outline=[(.21,0),(.79,0),(1,.17),(1,.83),(.79,1),(.21,1),(0,.83),(0,.17)] if key=='FEB' else [(.01,.01),(.99,.01),(.995,.74),(.855,.995),(.145,.995),(.005,.74)]
    dense=[]
    for j,a in enumerate(outline):
        b=outline[(j+1)%len(outline)]
        for k in range(4):dense.append((a[0]+(b[0]-a[0])*k/4,a[1]+(b[1]-a[1])*k/4))
    uvs=[(.5,.5)];faces=[];N=len(dense)
    for ring in range(1,5):
        t=ring/4
        uvs.extend([(.5+(u-.5)*t,.5+(v-.5)*t) for u,v in dense])
    for j in range(N):faces.append((0,1+j,1+(j+1)%N))
    for ring in range(3):
        a=1+ring*N;b=a+N
        for j in range(N):faces.append((a+j,b+j,b+(j+1)%N,a+(j+1)%N))
    positions=[]
    for u,v in uvs:
        p=center+right*((u-.5)*width)+up*((v-.5)*height)
        hit,normal,face,dist=bvh.ray_cast(p+n*.13,-n,.3)
        if hit is None:hit,normal,face,dist=bvh.find_nearest(p)
        positions.append(hit+normal*.0014)
    me=bpy.data.meshes.new('FEB_%02d_%s_patch_mesh'%(idx,key));me.from_pydata(positions,[],faces);me.update()
    ob=bpy.data.objects.new('FEB_%02d_Emblema_%s'%(idx,key),me);bpy.data.collections['FEB_%02d'%idx].objects.link(ob);ob.location=body.location;ob['variant']=idx
    me.materials.append(patchmats[key]);uv=me.uv_layers.new(name='UVMap')
    for p in me.polygons:
        p.use_smooth=True
        for l in p.loop_indices:uv.data[l].uv=uvs[me.loops[l].vertex_index]
    # Ensure winding faces outward.
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    if sum(f.normal.dot(n) for f in bm.faces)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    bm.to_mesh(me);bm.free()
    ob['reference']='Emblema original do PDF feb-textura; imagem inalterada'
for idx in range(1,7):
    make_patch(idx,1,'FEB');make_patch(idx,-1,'FifthArmy')
for o in scene.objects:
    if o.type=='MESH' and archive not in o.users_collection:o['adaptation']='FEB, campanha da Italia, 1944-1945; base Rangers fornecida pelo usuario'
(out/'adaptation_report.json').write_text(json.dumps(changes,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
cam=scene.camera;cam.location=(0,-12,3.2);cam.rotation_euler=(Vector((0,0,.92))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(out/'FEB_Soldiers_frente.png');bpy.ops.render.render(write_still=True)
cam.location=(0,12,3.2);cam.rotation_euler=(Vector((0,0,.92))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(out/'FEB_Soldiers_costas.png');bpy.ops.render.render(write_still=True)
result={'changes':len(changes),'patches':12,'output':str(out/'FEB_Soldiers.blend')}
