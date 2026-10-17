import bpy,json,numpy as np
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
selection=json.loads((out/'color_selection.json').read_text())
src=bpy.data.materials['FEB_Soldiers_Uniforme']
old_image=next(n.image for n in src.node_tree.nodes if n.type=='TEX_IMAGE' and n.image)
# Reuse the same UV rasterizer; no scene, material or geometry operations in this segment.
code=(out/'apply_pdf_colors.py').read_text()
segment=code[code.index('w,h=old_image.size'):code.index("image=bpy.data.images.new")]
segment=segment.replace('[.440,.447,.386]','[.365,.375,.290]').replace('[.503,.520,.357]','[.410,.430,.245]')
exec(segment)
im=bpy.data.images['FEB_Cores_PDF_4K']
if im.packed_file:im.unpack(method='REMOVE')
im.pixels.foreach_set(new.ravel());im.update();im.save();im.pack()
validation=json.loads((out/'colors_validation.json').read_text());validation['masks']=stats
(out/'colors_validation.json').write_text(json.dumps(validation,indent=2))
review=bpy.data.scenes['Conferencia_cores_PDF'];bpy.context.window.scene=review;cam=review.camera
review.render.filepath=str(out/'FEB_Cores_PDF_detalhe.png');bpy.ops.render.render(write_still=True)
cam.location=(3.5,4.2,1.9);target=Vector((3.5,0,.92));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
review.render.filepath=str(out/'FEB_Cores_PDF_costas.png');bpy.ops.render.render(write_still=True)
s=bpy.data.scenes['FEB_Soldiers'];cam.matrix_world=s.camera.matrix_world.copy();cam.data.ortho_scale=8.7
review.render.resolution_x=2700;review.render.resolution_y=850;review.render.filepath=str(out/'FEB_Cores_PDF_frente.png');bpy.ops.render.render(write_still=True)
bpy.context.window.scene=s
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result={'refined':True,'texture':im.filepath,'geometry_changed':False}
