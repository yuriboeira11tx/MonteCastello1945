import bpy,json,numpy as np,hashlib,struct,shutil
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
assets=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldiers')
s=bpy.data.scenes['FEB_Soldiers'];bpy.context.window.scene=s
backup=out/'FEB_Soldiers_antes_cores_PDF.blend'
if not backup.exists():bpy.ops.wm.save_as_mainfile(filepath=str(backup),copy=True)
selection=json.loads((out/'color_selection.json').read_text())
objects=[o for i in range(1,7) for o in bpy.data.collections['FEB_%02d'%i].objects if o.type=='MESH']
def mesh_hash(o):
 h=hashlib.sha256();me=o.data
 for v in me.vertices:h.update(struct.pack('3f',*v.co))
 for p in me.polygons:h.update(struct.pack('%di'%len(p.vertices),*p.vertices))
 for uv in me.uv_layers:
  for d in uv.data:h.update(struct.pack('2f',*d.uv))
 for row in o.matrix_world:h.update(struct.pack('4f',*row))
 return h.hexdigest()
baseline={o.name:dict(geometry_uv_transform=mesh_hash(o),materials=[m.name if m else None for m in o.data.materials],polygon_materials=[p.material_index for p in o.data.polygons]) for o in objects}
files={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (assets/'Textures').glob('*.png')}
(out/'colors_baseline.json').write_text(json.dumps(dict(objects=baseline,textures=files),indent=2))
src=bpy.data.materials['FEB_Soldiers_Uniforme'];old_image=next(n.image for n in src.node_tree.nodes if n.type=='TEX_IMAGE' and n.image)
w,h=old_image.size
px=np.empty(w*h*4,dtype=np.float32);old_image.pixels.foreach_get(px);px=px.reshape(h,w,4)
masks={k:np.zeros((h,w),dtype=bool) for k in ('calca','botas','suspensorios','ferragens')}
for name,cats in selection.items():
 ob=bpy.data.objects[name];me=ob.data;me.calc_loop_triangles();uv=me.uv_layers.active.data
 classify={p:cat for cat,ids in cats.items() for p in ids}
 for tri in me.loop_triangles:
  cat=classify.get(tri.polygon_index)
  if not cat:continue
  pts=np.array([[uv[l].uv.x*w,uv[l].uv.y*h] for l in tri.loops])
  lo=np.maximum(np.floor(pts.min(axis=0)).astype(int),0);hi=np.minimum(np.ceil(pts.max(axis=0)).astype(int),[w-1,h-1])
  if np.any(hi<lo):continue
  xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
  cross=[]
  for j in range(3):
   a,b=pts[j],pts[(j+1)%3];cross.append((b[0]-a[0])*(yy-a[1])-(b[1]-a[1])*(xx-a[0]))
  inside=(np.minimum.reduce(cross)>=-.001)|(np.maximum.reduce(cross)<=.001)
  masks[cat][lo[1]:hi[1]+1,lo[0]:hi[0]+1]|=inside
overlap={a+'_'+b:int(np.count_nonzero(masks[a]&masks[b])) for a in masks for b in masks if a<b}
assert not any(overlap.values()),overlap
new=px.copy();stats={}
# Colors sampled visually from the PDF, reduced to diffuse albedo for lit surfaces.
targets={'calca':np.array([.440,.447,.386]),'suspensorios':np.array([.503,.520,.357]),'botas':np.array([.048,.050,.048]),'ferragens':np.array([.035,.037,.035])}
for cat,mask in masks.items():
 rgb=px[mask,:3];luma=rgb@np.array([.2126,.7152,.0722]);nonblack=luma[luma>.02];median=float(np.median(nonblack))
 dilated=mask.copy()
 for _ in range(5):
  expanded=dilated.copy()
  for dy,dx in ((-1,0),(1,0),(0,-1),(0,1),(-1,-1),(-1,1),(1,-1),(1,1)):
   expanded|=np.roll(np.roll(dilated,dy,axis=0),dx,axis=1)
  dilated=expanded
 for other,othermask in masks.items():
  if other!=cat:dilated&=~othermask
 rgb=px[dilated,:3];lum=rgb@np.array([.2126,.7152,.0722])
 ratio=np.clip(lum/max(median,.001),0,3)**(.92 if cat in ('botas','ferragens') else .90)
 colored=targets[cat][None,:]*ratio[:,None]
 # Retain a small amount of original stained color variation in the cloth.
 if cat in ('calca','suspensorios'):
  chroma=rgb/np.maximum(lum[:,None],.01)
  centered=chroma-np.median(chroma,axis=0)
  colored*=np.clip(1+.15*centered,.90,1.10)
 new[dilated,:3]=np.clip(colored,0,1)
 stats[cat]=dict(pixels=int(mask.sum()),source_luma_median=median,target_srgb=targets[cat].tolist())
image=bpy.data.images.new('FEB_Cores_PDF_4K',width=w,height=h,alpha=True)
image.colorspace_settings.name='sRGB';image.pixels.foreach_set(new.ravel());image.update()
image.filepath_raw=str(assets/'Textures/FEB_Cores_PDF_4K.png');image.file_format='PNG';image.save();image.pack()
mat=src.copy();mat.name='FEB_Soldiers_Cores_PDF'
for node in mat.node_tree.nodes:
 if node.type=='TEX_IMAGE' and node.image==old_image:node.image=image
changed={}
for name,cats in selection.items():
 ob=bpy.data.objects[name];ids=set(p for ps in cats.values() for p in ps)
 if not ids:continue
 ob.data.materials.append(mat);index=len(ob.data.materials)-1
 for p in ids:ob.data.polygons[p].material_index=index
 changed[name]={k:len(v) for k,v in cats.items()}
for ob in objects:
 assert mesh_hash(ob)==baseline[ob.name]['geometry_uv_transform'],ob.name
 allowed=set(p for ps in selection.get(ob.name,{}).values() for p in ps)
 for p,old in zip(ob.data.polygons,baseline[ob.name]['polygon_materials']):
  if p.index not in allowed:assert p.material_index==old,(ob.name,p.index)
for name,digest in files.items():assert hashlib.sha256((assets/'Textures'/name).read_bytes()).hexdigest()==digest,name
validation=dict(objects_geometry_uv_transform_unchanged=len(objects),original_texture_files_unchanged=len(files),changed=changed,masks=stats,uv_overlap_pixels=overlap,new_texture=str(image.filepath_raw))
(out/'colors_validation.json').write_text(json.dumps(validation,indent=2))
# A dedicated review scene uses the same editable meshes and lighting.
review=bpy.data.scenes.new('Conferencia_cores_PDF');review.world=s.world
for coll in [bpy.data.collections['FEB_%02d'%i] for i in range(1,7)]:review.collection.children.link(coll)
for ob in s.objects:
 if ob.type=='LIGHT':review.collection.objects.link(ob)
cam=s.camera.copy();cam.data=s.camera.data.copy();review.collection.objects.link(cam);review.camera=cam
review.render.engine='CYCLES';review.cycles.samples=24;review.cycles.use_denoising=True;review.view_settings.view_transform=s.view_settings.view_transform
cam.matrix_world=s.camera.matrix_world.copy();cam.data.ortho_scale=8.7
review.render.resolution_x=2700;review.render.resolution_y=850;review.render.resolution_percentage=100;review.render.image_settings.file_format='PNG'
bpy.context.window.scene=review
review.render.filepath=str(out/'FEB_Cores_PDF_frente.png');bpy.ops.render.render(write_still=True)
cam.location=(3.5,-4.2,1.9);target=Vector((3.5,0,.92));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.15
review.render.resolution_x=1100;review.render.resolution_y=1400;review.render.filepath=str(out/'FEB_Cores_PDF_detalhe.png');bpy.ops.render.render(write_still=True)
bpy.context.window.scene=s
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result=validation
