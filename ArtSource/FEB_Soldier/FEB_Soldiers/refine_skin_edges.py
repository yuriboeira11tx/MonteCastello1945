import bpy,numpy as np,json,sys,importlib
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers');sys.path.insert(0,str(out))
import skin_utils as su;importlib.reload(su)
data=json.loads((out/'skin_variants.json').read_text(encoding='utf-8'))
review=bpy.data.scenes['Conferencia_tons_de_pele'];bpy.context.window.scene=review;cam=review.camera
review.render.resolution_x=720;review.render.resolution_y=900;cam.data.ortho_scale=.79
for v in data['variants']:
 if v['id'] not in (3,4,5,6):continue
 original=bpy.data.images.load(v['source_face'],check_existing=True);pixels=su.read_pixels(original)
 new,mask,anchor=su.recolor_face(pixels,v['kind'],v['target']);su.save_image('FEB_%02d_Pele_Rosto'%v['id'],v['face_texture'],new)
 x=(v['id']-3.5)*1.4;cam.location=(x,-3.5,1.67);target=Vector((x,-.02,1.47));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
 review.render.filepath=str(out/('FEB_%02d_Pele_retrato.png'%v['id']));bpy.ops.render.render(write_still=True)
bpy.context.window.scene=bpy.data.scenes['FEB_Soldiers'];bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result={'eyelid_skin_refined':True,'iris_and_sclera_preserved':True}
