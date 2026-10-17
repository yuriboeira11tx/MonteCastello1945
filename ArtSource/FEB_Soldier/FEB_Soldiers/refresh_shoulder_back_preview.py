import bpy
from pathlib import Path
from mathutils import Vector
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
s=bpy.context.window.scene;test=bpy.data.scenes['Verificacao_material_ombro_corrigido'];bpy.context.window.scene=test;cam=test.camera
cam.location=(3.5,4.0,1.95);target=Vector((3.5,0,1.05));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.15
test.render.resolution_x=900;test.render.resolution_y=1150;test.render.filepath=str(out/'FEB_06_Pele_costas.png');bpy.ops.render.render(write_still=True)
bpy.context.window.scene=s;bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result={'back_preview_updated':True}
