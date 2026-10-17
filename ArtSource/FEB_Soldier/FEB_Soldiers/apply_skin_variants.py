import bpy,json,numpy as np,hashlib,sys
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
sys.path.insert(0,str(out))
import skin_utils as su
assets=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldiers');tex=assets/'Textures'/'Pele';tex.mkdir(exist_ok=True)
s=bpy.data.scenes['FEB_Soldiers'];bpy.context.window.scene=s
backup=out/'FEB_Soldiers_antes_variacao_pele.blend'
if not backup.exists():bpy.ops.wm.save_as_mainfile(filepath=str(backup),copy=True)
objects=[o for i in range(1,7) for o in bpy.data.collections['FEB_%02d'%i].objects if o.type=='MESH']
baseline={'objects':{o.name:dict(mesh_hash=su.mesh_hash(o),materials=[m.name for m in o.data.materials],polygon_materials=[p.material_index for p in o.data.polygons]) for o in objects},'textures':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (assets/'Textures').glob('*.png')}}
(out/'skin_baseline.json').write_text(json.dumps(baseline,indent=2))
records=json.loads((out/'skin_components.json').read_text());record_by_name={x['name']:x for x in records}
variants=[
 dict(id=1,tone='clara quente',kind='braebury',target=[.66,.48,.38]),
 dict(id=2,tone='castanha escura quente',kind='david',target=[.255,.145,.090]),
 dict(id=3,tone='media oliva',kind='mo',target=[.455,.315,.215]),
 dict(id=4,tone='clara neutra',kind='mo',target=[.605,.455,.375]),
 dict(id=5,tone='castanha media dourada',kind='randall',target=[.365,.235,.145]),
 dict(id=6,tone='castanha profunda',kind='wolf',target=[.195,.108,.071])]
hand_regions=[(.938,.565,1,.772),(.938,.294,1,.48),(.632,.730,.726,.888),(.811,.234,.889,.288),(.901,.151,.954,.200),(.946,.491,.984,.557),(.461,.308,.500,.457)]
def within(uv,r):return uv[0][0]>=r[0]-.001 and uv[0][1]>=r[1]-.001 and uv[1][0]<=r[2]+.001 and uv[1][1]<=r[3]+.001
hand_selection={};head_objects={};validation=[]
body_mat=bpy.data.materials['FEB_Soldiers_Uniforme'];body_image=next(n.image for n in body_mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image);body_pixels=su.read_pixels(body_image)
for v in variants:
 idx=v['id'];collection=bpy.data.collections['FEB_%02d'%idx];heads=[];original_mat=None
 for ob in collection.objects:
  if ob.type!='MESH':continue
  if any(('head' in m.name or 'braebury' in m.name) for m in ob.data.materials):
   heads.append(ob);original_mat=next(m for m in ob.data.materials if 'head' in m.name or 'braebury' in m.name)
 assert original_mat is not None
 head_objects[str(idx)]=[o.name for o in heads]
 original_image=next(n.image for n in original_mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image)
 pixels=su.read_pixels(original_image);new,mask,anchor=su.recolor_face(pixels,v['kind'],v['target'])
 assert np.array_equal(new[mask==0],pixels[mask==0])
 path=tex/('FEB_%02d_Pele_Rosto.png'%idx);im=su.save_image('FEB_%02d_Pele_Rosto'%idx,path,new)
 mat=original_mat.copy();mat.name='FEB_%02d_Pele_Rosto'%idx
 for n in mat.node_tree.nodes:
  if n.type=='TEX_IMAGE' and n.image==original_image:n.image=im
 for ob in heads:
  for k,m in enumerate(ob.data.materials):
   if m==original_mat:ob.data.materials[k]=mat
 v.update(face_texture=str(path),face_material=mat.name,source_face=original_image.filepath,source_cheek_srgb=anchor.tolist())
 union=np.zeros(body_pixels.shape[:2],bool);selected={}
 for ob in collection.objects:
  if ob.type!='MESH' or ob.name not in record_by_name:continue
  if not any(t in ob.name for t in ('Corpo_uniforme','M41_parte_superior')):continue
  ids=[]
  for c in record_by_name[ob.name]['components']:
   if c['b'][0][2]>.85 and c['b'][1][2]<1.30 and any(within(c['uv'],r) for r in hand_regions):ids+=c['p']
  if ids:
   selected[ob.name]=ids;union|=su.raster(ob,ids,body_image.size[0],body_image.size[1])
 assert selected,idx
 hand_selection.update(selected);padded=su.dilate(union,8)
 rgb=body_pixels[:,:,:3];luma=rgb@np.array([.2126,.7152,.0722]);valid=union&(luma>.15)
 hand_anchor=np.median(rgb[valid],axis=0)
 hands=body_pixels.copy();scaled=rgb*(np.array(v['target'])/hand_anchor)
 # Palms, nails and finger pads are naturally lighter; keep their original variation.
 hands[padded,:3]=np.clip(scaled[padded],0,1)
 hands[~padded]=0;hands[padded,3]=1
 hp=tex/('FEB_%02d_Pele_Maos.png'%idx);hi=su.save_image('FEB_%02d_Pele_Maos'%idx,hp,hands)
 hm=body_mat.copy();hm.name='FEB_%02d_Pele_Maos'%idx
 for n in hm.node_tree.nodes:
  if n.type=='TEX_IMAGE' and n.image==body_image:n.image=hi
 for name,ids in selected.items():
  ob=bpy.data.objects[name];ob.data.materials.append(hm);mi=len(ob.data.materials)-1
  for p in ids:ob.data.polygons[p].material_index=mi
 v.update(hands_texture=str(hp),hands_material=hm.name,hand_faces={name:len(ids) for name,ids in selected.items()})
 validation.append(dict(id=idx,protected_head_pixels_unchanged=True,skin_pixels=int(np.count_nonzero(mask)),hand_pixels=int(union.sum())))
for ob in objects:
 entry=baseline['objects'][ob.name];assert su.mesh_hash(ob)==entry['mesh_hash'],ob.name
 allowed=set(hand_selection.get(ob.name,[]))
 for p,old in zip(ob.data.polygons,entry['polygon_materials']):
  if p.index not in allowed:assert p.material_index==old
for path,digest in baseline['textures'].items():assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,path
(out/'skin_variants.json').write_text(json.dumps(dict(variants=variants,hands_selection=hand_selection,head_objects=head_objects),ensure_ascii=False,indent=2),encoding='utf-8')
(out/'skin_validation.json').write_text(json.dumps(dict(objects_unchanged=50,original_textures_unchanged=len(baseline['textures']),variants=validation),indent=2))
review=bpy.data.scenes.new('Conferencia_tons_de_pele');review.world=s.world
for i in range(1,7):review.collection.children.link(bpy.data.collections['FEB_%02d'%i])
for ob in s.objects:
 if ob.type=='LIGHT':review.collection.objects.link(ob)
cam=s.camera.copy();cam.data=s.camera.data.copy();review.collection.objects.link(cam);review.camera=cam
review.render.engine='CYCLES';review.cycles.samples=32;review.cycles.use_denoising=True;review.view_settings.view_transform=s.view_settings.view_transform
review.render.resolution_percentage=100;review.render.image_settings.file_format='PNG';bpy.context.window.scene=review
cam.data.ortho_scale=8.7;review.render.resolution_x=2700;review.render.resolution_y=850;review.render.filepath=str(out/'FEB_Tons_de_Pele_conjunto.png');bpy.ops.render.render(write_still=True)
review.render.resolution_x=720;review.render.resolution_y=900;cam.data.ortho_scale=.79
for v in variants:
 x=(v['id']-3.5)*1.4;cam.location=(x,-3.5,1.67);target=Vector((x,-.02,1.47));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
 review.render.filepath=str(out/('FEB_%02d_Pele_retrato.png'%v['id']));bpy.ops.render.render(write_still=True)
bpy.context.window.scene=s;bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result={'variants':[{'id':v['id'],'tone':v['tone'],'hands':v['hand_faces']} for v in variants],'new_textures':12,'geometry_unchanged':50,'original_textures_unchanged':len(baseline['textures'])}
