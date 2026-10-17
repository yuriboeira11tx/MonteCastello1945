import bpy,json,sys,hashlib
from pathlib import Path
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers');sys.path.insert(0,str(out))
import skin_utils as su
objects=[o for i in range(1,7) for o in bpy.data.collections['FEB_%02d'%i].objects if o.type=='MESH']
assets=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldiers')
baseline=dict(file=bpy.data.filepath,scene=bpy.context.scene.name,meshes={o.name:su.mesh_hash(o) for o in objects},textures={str(p.relative_to(assets)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (assets/'Textures').rglob('*.png')},unity_metas={str(p.relative_to(assets)):p.read_text() for p in assets.rglob('*.meta')},folder_meta=assets.with_suffix('.meta').read_text(),material_names=sorted({m.name for o in objects for m in o.data.materials}),images=[dict(name=im.name,path=im.filepath) for im in bpy.data.images if im.filepath and ('FEB_Soldiers' in im.filepath or 'FEB_Soldiers' in im.filepath)])
(out/'rename_feb_soldiers_baseline.json').write_text(json.dumps(baseline,indent=2))
result={'current_file':bpy.data.filepath,'mesh_count':len(objects),'textures':len(baseline['textures']),'unity_meta_count':len(baseline['unity_metas'])}
