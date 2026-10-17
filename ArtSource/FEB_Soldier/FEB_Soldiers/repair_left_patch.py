import bpy,json,hashlib,struct
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
s=bpy.data.scenes['FEB_Soldiers'];bpy.context.window.scene=s
backup=out/'FEB_Soldiers_antes_correcao_emblema.blend'
if not backup.exists():bpy.ops.wm.save_as_mainfile(filepath=str(backup),copy=True)
def fingerprint(o):
    h=hashlib.sha256()
    h.update(str([list(row) for row in o.matrix_world]).encode())
    h.update(str([m.name if m else None for m in o.data.materials]).encode())
    for v in o.data.vertices:h.update(struct.pack('fff',*v.co))
    for p in o.data.polygons:h.update(str((tuple(p.vertices),p.material_index,p.use_smooth)).encode())
    for layer in o.data.uv_layers:
        h.update(layer.name.encode())
        for v in layer.data:h.update(struct.pack('ff',*v.uv))
    return h.hexdigest()
protected={o.name:fingerprint(o) for i in range(1,7) for o in bpy.data.collections['FEB_%02d'%i].objects if o.type=='MESH' and o.name!='FEB_%02d_Emblema_FEB'%i}
components={x['object']:x['components'] for x in json.loads((out/'components.json').read_text())}
orig={int(n[4:6]):bpy.data.objects[n] for n in components if 'DrawCall_0_DrawCall_0' in n}
def original_center(i):
    o=orig[i]
    c=next(c for c in components[o.name] if c['uv'] and c['uv'][0][0]>.085 and c['uv'][1][0]<.125 and c['uv'][0][1]>.95)
    ids=[v for p in c['polygon_ids'] for v in o.data.polygons[p].vertices]
    return sum((o.data.vertices[v].co for v in ids),Vector())/len(ids)
outline=[(.211,.003),(.789,.003),(.997,.172),(.997,.828),(.789,.997),(.211,.997),(.003,.828),(.003,.172)]
def clip(poly,a,b):
    def cross(p):return (b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0])
    result=[]
    for k,p in enumerate(poly):
        q=poly[(k+1)%len(poly)];dp,dq=cross(p),cross(q)
        if dp>=-1e-12:result.append(p)
        if (dp>0 and dq<0) or (dp<0 and dq>0):
            t=dp/(dp-dq);result.append((p[0]+t*(q[0]-p[0]),p[1]+t*(q[1]-p[1])))
    return result
uvs=[];faces=[];lookup={}
def vi(uv):
    key=tuple(round(x,8) for x in uv)
    if key not in lookup:lookup[key]=len(uvs);uvs.append(key)
    return lookup[key]
nx,ny=24,28
for y in range(ny):
    for x in range(nx):
        a=(x/nx,y/ny);b=((x+1)/nx,y/ny);c=((x+1)/nx,(y+1)/ny);d=(x/nx,(y+1)/ny)
        for poly in [[a,b,c],[a,c,d]]:
            for j,p in enumerate(outline):
                poly=clip(poly,p,outline[(j+1)%len(outline)])
                if not poly:break
            for j in range(1,len(poly)-1):
                t=[poly[0],poly[j],poly[j+1]]
                area=(t[1][0]-t[0][0])*(t[2][1]-t[0][1])-(t[1][1]-t[0][1])*(t[2][0]-t[0][0])
                if area>1e-10:faces.append(tuple(vi(p) for p in t))
n=Vector((.86,-.12,.5)).normalized();up=(Vector((0,0,1))-n*n.z).normalized();right=up.cross(n).normalized()
stats=[]
for idx in range(1,7):
    o=bpy.data.objects['FEB_%02d_Emblema_FEB'%idx]
    body=bpy.data.objects.get('FEB_%02d_M41_parte_superior'%idx) or bpy.data.objects.get('FEB_%02d_Corpo_uniforme'%idx)
    donor={3:4,5:6}.get(idx,idx);center=original_center(donor)
    shift=Vector(body['collar_offset']) if idx in [3,5] else Vector()
    center+=shift
    source=orig[donor]
    tag=next(c for c in components[source.name] if c['uv'] and c['uv'][0][0]>.085 and c['uv'][1][0]<.125 and c['uv'][0][1]>.95)
    tag_indices=sorted(set(v for p in tag['polygon_ids'] for v in source.data.polygons[p].vertices))
    tag_map={v:j for j,v in enumerate(tag_indices)}
    backing=[source.data.vertices[v].co.copy()+shift for v in tag_indices]
    backing_faces=[tuple(tag_map[v] for v in source.data.polygons[p].vertices) for p in tag['polygon_ids']]
    vertices=[v.co.copy() for v in body.data.vertices]
    polygons=[]
    for p in body.data.polygons:
        ctr=p.center
        if ctr.x>.16 and 1.20<ctr.z<1.51 and p.normal.dot(n)>.05:polygons.append(list(p.vertices))
    # Restore the cloth-shaped support inside the emblem object itself. The
    # surrounding sleeve object remains byte-for-byte unchanged.
    base=len(vertices);vertices.extend(backing)
    polygons.extend([tuple(base+v for v in face) for face in backing_faces])
    bvh=BVHTree.FromPolygons(vertices,polygons)
    positions=[];misses=0;heights=[];samples=[];rays=[]
    for u,v in uvs:
        p=center+right*((u-.5)*.073)+up*((v-.5)*.089)
        hit,normal,face,dist=bvh.ray_cast(p+n*.25,-n,.55)
        if hit is None:
            misses+=1
        else:
            height=(hit-p).dot(n)
            samples.append([1,u-.5,v-.5,(u-.5)**2,(u-.5)*(v-.5),(v-.5)**2]);heights.append(height)
        rays.append(p)
    assert misses==0, 'Projection on restored cloth must be complete'
    clearance=.0015
    for p,height in zip(rays,heights):positions.append(p+n*(height+clearance))
    # Fit cloth UVs to neighboring sleeve samples; this support uses the original
    # uniform atlas, while the insignia image and its material remain unchanged.
    fit_coords=[];fit_uv=[]
    for p in body.data.polygons:
        for li in p.loop_indices:
            co=body.data.vertices[body.data.loops[li].vertex_index].co
            tex=body.data.uv_layers.active.data[li].uv
            if (co-center).length<.095 and 0<tex.x<.13 and .615<tex.y<.953:
                delta=co-center;fit_coords.append([1,delta.dot(right),delta.dot(up)]);fit_uv.append(list(tex))
    coefficients=np.linalg.lstsq(np.array(fit_coords),np.array(fit_uv),rcond=None)[0]
    back_uv=[tuple(np.array([1,(co-center).dot(right),(co-center).dot(up)])@coefficients) for co in backing]
    all_uv=uvs+back_uv
    count=len(positions);all_faces=faces+[tuple(count+v for v in f) for f in backing_faces]
    positions.extend(backing)
    me=bpy.data.meshes.new('FEB_%02d_Emblema_UV_continua'%idx);me.from_pydata(positions,[],all_faces);me.update()
    layer=me.uv_layers.new(name='UVMap')
    for p in me.polygons:
        p.use_smooth=True
        p.material_index=0 if p.index<len(faces) else 1
        for l in p.loop_indices:layer.data[l].uv=all_uv[me.loops[l].vertex_index]
    me.materials.append(bpy.data.materials['FEB_Soldiers_FEB_Emblema'])
    me.materials.append(body.data.materials[0])
    o.data=me
    o['patch_fix']='Regular continuous UV projection, left FEB patch only, no nearest-point collapse'
    me.calc_loop_triangles()
    degenerate=0;flipped=0
    for t in me.loop_triangles:
        if me.polygons[t.polygon_index].material_index!=0:continue
        a,b,c=[me.vertices[v].co for v in t.vertices]
        cross=(b-a).cross(c-a)
        if cross.length<1e-12:degenerate+=1
        if cross.dot(n)<=0:flipped+=1
    stats.append({'variant':idx,'vertices':len(me.vertices),'triangles':len(me.loop_triangles),'degenerate':degenerate,'folded':flipped,'sleeve_opening_samples':misses,'clearance_m':clearance})
after={name:fingerprint(bpy.data.objects[name]) for name in protected}
assert protected==after,'Unexpected modification outside left FEB patches'
assert all(x['degenerate']==0 and x['folded']==0 for x in stats)
fix={'only_left_FEB_patches_changed':True,'protected_objects':protected,'patches':stats}
(out/'left_patch_fix_validation.json').write_text(json.dumps(fix,indent=2),encoding='utf-8')
view=bpy.data.scenes['Conferencia_emblema_esquerdo'];bpy.context.window.scene=view
view.render.filepath=str(out/'Emblema_esquerdo_corrigido.png');bpy.ops.render.render(write_still=True)
bpy.context.window.scene=s
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result={'only_left_patch_changed':True,'unchanged_other_objects':len(protected),'patches':stats}
