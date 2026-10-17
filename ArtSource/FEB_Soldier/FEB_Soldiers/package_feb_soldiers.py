from pathlib import Path
import json,shutil,zipfile
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
assets=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldiers')
report=json.loads((out/'export_report.json').read_text())
shutil.copy2(assets/'LEIA-ME.txt',out/'LEIA-ME.txt')
files=list((assets/'Models').glob('*.fbx'))+[Path(im['file']) for im in report['images']]+[assets/'LEIA-ME.txt',assets/'GUIA_TEXTURAS_PELE.txt']
zip_path=out/'FEB_Soldiers_FBX_Texturas.zip'
with zipfile.ZipFile(zip_path,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in files:z.write(p,Path('FEB_Soldiers')/p.relative_to(assets))
    z.write(out/'FEB_Soldiers_exportado_conferido.png','FEB_Soldiers/Previa.png')
    for name in ['FEB_Tons_de_Pele_identificados.png','FEB_06_Pele_costas.png','Ponto_ombro_FBX_corrigido.png']+['FEB_%02d_Pele_retrato.png'%i for i in range(1,7)]:
        if (out/name).exists():z.write(out/name,'FEB_Soldiers/Previa_Pele/'+name)
with zipfile.ZipFile(zip_path) as z:
    assert z.testzip() is None
    print(json.dumps({'zip':str(zip_path),'bytes':zip_path.stat().st_size,'files':len(z.namelist()),'blend_bytes':(out/'FEB_Soldiers.blend').stat().st_size}))
