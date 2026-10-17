"""Check the neck correction, immutable reference emblems, UVs and exported rig."""
from pathlib import Path
src=Path(__file__).resolve().parent
code=(src/'validate_realista.py').read_text(encoding='utf-8')
code=code.replace("FEB_Soldier/Realista'", "FEB_Soldier/Campanha'")
code=code.replace('FEB_Soldier_Realista','FEB_Soldier_Campanha').replace('validation_realista.json','validation_campanha.json')
code=code.replace("if 'Sarja' in o.name", "if 'Jaqueta' in o.name")
extra="""
import hashlib
export_metadata=json.loads((SRC/'export_report_campanha.json').read_text())
checks['exportacao_da_revisao_atual']=export_metadata.get('source_blend_sha256')==hashlib.sha256((SRC/'FEB_Soldier_Campanha.blend').read_bytes()).hexdigest()
expected={'FEB_Patch.png':'470f563973b03489b9d5180185d887e78eca2c79ee3da1c5f8749a91c6d1086b','FifthArmy_Patch.png':'f0270096be66371b085c8afce31171406ff06b059e0910a911c225faa0ad2861'}
for filename,digest in expected.items():
    checks['emblema_original_'+filename]=hashlib.sha256((ASSET.parent/'Textures'/filename).read_bytes()).hexdigest()==digest
head=next(o for o in meshes if 'Pele facial' in o.name)
nape=[v.co for v in head.data.vertices if 1.515<v.co.z<1.58 and v.co.y>0]
checks['nuca_contida_na_gola']=bool(nape) and max(p.y for p in nape)<.060
checks['maos_anatomicas']=any('Pele anatomica maos' in o.name for o in meshes)
for side in ['esquerdo','direito']:
    patch=next(o for o in meshes if 'Distintivo_' in o.name and side in o.name)
    checks['emblema_conformado_'+side]=len(patch.data.polygons)>100 and len(patch.data.materials)==1
"""
code=code.replace("body=next(o for o in meshes", extra+"\nbody=next(o for o in meshes")
exec(compile(code,str(src/'validate_realista.py'),'exec'))
