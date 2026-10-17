import bpy,json
from pathlib import Path
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
archive=Path('E:/Unity/MonteCastello1945/ArtSource/Characters_Archive_20261016')
changed=[]
for im in bpy.data.images:
 path=bpy.path.abspath(im.filepath).replace('\\','/') if im.filepath else ''
 if '/Assets/Art/Characters/' in path:
  new=path.replace('/Assets/Art/Characters/','/ArtSource/Characters_Archive_20261016/')
  if Path(new).is_file():im.filepath=new;changed.append(im.name)
  elif not im.packed_file:raise RuntimeError('Image missing: '+new)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FEB_Soldiers.blend'))
result={'images_relocated':len(changed),'archive':str(archive),'unity_assets_remain_empty':not any(Path('E:/Unity/MonteCastello1945/Assets/Art/Characters').iterdir())}
