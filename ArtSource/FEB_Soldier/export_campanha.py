"""Portable copy of the photo-reference revision, with the same verified bake path."""
from pathlib import Path
import hashlib,json
src=Path(__file__).resolve().parent
source_digest=hashlib.sha256((src/'FEB_Soldier_Campanha.blend').read_bytes()).hexdigest()
code=(src/'export_realista.py').read_text(encoding='utf-8')
code=code.replace("FEB_Soldier/Realista'", "FEB_Soldier/Campanha'")
code=code.replace('FEB_Soldier_Realista','FEB_Soldier_Campanha')
code=code.replace('export_report_realista.json','export_report_campanha.json')
exec(compile(code,str(src/'export_realista.py'),'exec'))
assert source_digest==hashlib.sha256((src/'FEB_Soldier_Campanha.blend').read_bytes()).hexdigest(),'Source changed while exporting; rerun export.'
report=json.loads((src/'export_report_campanha.json').read_text());report['source_blend_sha256']=source_digest
(src/'export_report_campanha.json').write_text(json.dumps(report,indent=2))
