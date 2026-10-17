import bpy,json,sys,numpy as np
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers');sys.path.insert(0,str(out))
import skin_utils as su
s=bpy.data.scenes['FEB_Soldiers']
baseline=json.loads((out/'skin_baseline.json').read_text());skin=json.loads((out/'skin_variants.json').read_text(encoding='utf-8'));records=json.loads((out/'color_components.json').read_text())
found=[]
for record in records:
 for c in record['components']:
  uv=c['uv']
  if uv and abs(uv[0][0]-.1432)<.0002 and abs(uv[0][1]-.5985)<.0002 and abs(uv[1][0]-.1475)<.0002:
   ob=bpy.data.objects[record['name']];ids=c['polygons'];p=ob.data.polygons[ids[0]];mat=ob.data.materials[p.material_index]
   im=next(n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image);px=su.read_pixels(im);a=px[int(.5985*im.size[1]):int(.6099*im.size[1]),int(.1432*im.size[0]):int(.1475*im.size[0])]
   node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');alpha=node.inputs.get('Alpha')
   found.append(dict(object=ob.name,polygons=ids,bounds=c['bounds'],material=mat.name,image=im.filepath,rgba_median=np.median(a,axis=(0,1)).tolist(),alpha_default=alpha.default_value,alpha_links=[l.from_socket.name for l in alpha.links],unchanged_since_before_skin=su.mesh_hash(ob)==baseline['objects'][ob.name]['mesh_hash'],all_same_material_before_skin=all(baseline['objects'][ob.name]['polygon_materials'][pidx]==ob.data.polygons[pidx].material_index for pidx in ids)))
assert found
(out/'shoulder_spots_inspection.json').write_text(json.dumps(found,indent=2))
# Close view of the spot from the same side as the user's screenshot.
review=bpy.data.scenes.new('Conferencia_ponto_ombro');review.world=s.world
review.collection.children.link(bpy.data.collections['FEB_01'])
for ob in s.objects:
 if ob.type=='LIGHT':review.collection.objects.link(ob)
cam=s.camera.copy();cam.data=s.camera.data.copy();review.collection.objects.link(cam);review.camera=cam
first=found[0];ob=bpy.data.objects[first['object']];verts=set(v for pi in first['polygons'] for v in ob.data.polygons[pi].vertices)
target=sum((ob.matrix_world@ob.data.vertices[i].co for i in verts),Vector())/len(verts)
cam.location=target+Vector((-1.2,-.95,.65));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.27
review.render.engine='CYCLES';review.cycles.samples=24;review.cycles.use_denoising=True;review.view_settings.view_transform=s.view_settings.view_transform
review.render.resolution_x=950;review.render.resolution_y=950;review.render.resolution_percentage=100;review.render.image_settings.file_format='PNG'
bpy.context.window.scene=review;review.render.filepath=str(out/'Ponto_ombro_antes.png');bpy.ops.render.render(write_still=True)
bpy.context.window.scene=s
result={'current_file':bpy.data.filepath,'spots':found}
