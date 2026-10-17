"""FEB photo-reference revision. Keeps Realista and TPose deliverables intact."""
from pathlib import Path
CAMPAIGN=True
src=Path(__file__).resolve().parent
code=(src/'build_soldier_realism.py').read_text(encoding='utf-8')
code=code.replace("REAL_TEX=ASSET/'Realista/Textures'","REAL_TEX=ASSET/'Campanha/Textures'")
code=code.replace("REAL_MODELS=ASSET/'Realista/Models'","REAL_MODELS=ASSET/'Campanha/Models'")
code=code.replace("PREVIEW=SRC/'Previews_Realista'","PREVIEW=SRC/'Previews_Campanha'")
old="exec(compile((SRC/'realism_materials.py').read_text(encoding='utf-8'),str(SRC/'realism_materials.py'),'exec'))"
new="""exec(compile((SRC/'campaign_details.py').read_text(encoding='utf-8'),str(SRC/'campaign_details.py'),'exec'))
mat_source=(SRC/'realism_materials.py').read_text(encoding='utf-8')
mat_source=mat_source.replace('return (x*(1-blend)+limited*blend,-(v.z-.47)*.095+.006,z)', 'return campaign_face_transform(Vector((x*(1-blend)+limited*blend,-(v.z-.47)*.095+.006,z)))')
mat_source=mat_source.replace("default_value=.84;hs.inputs['Value'].default_value=.84", "default_value=.90;hs.inputs['Value'].default_value=.72")
mat_source=mat_source.replace('range(80)', 'range(180)').replace('z=1.727+.006*sin(pi*t)', 'z=1.725+.004*sin(pi*t)')
exec(compile(mat_source,str(SRC/'realism_materials.py'),'exec'))
campaign_materials()
"""
assert old in code;code=code.replace(old,new)
code=code.replace("exec(compile(body,str(source),'exec'))", "body=campaign_body_source(body)\nexec(compile(body,str(source),'exec'))\ncampaign_equipment_position()")
start=code.index('# Compression and tension folds');end=code.index('# Fine garment seams',start)
code=code[:start]+"campaign_folds()\n\n"+code[end:]
code=code.replace("rigcode=original[", "campaign_finish_geometry()\n\nrigcode=original[")
code=code.replace("rig['Revisao']='Rosto anatomico com UV e normal dedicados; dobras integradas; materiais fisicos separados.'", "campaign_finish_rig(rig)")
code=code.replace('model_report_realista.json','model_report_campanha.json').replace('FEB_Soldier_Realista.blend','FEB_Soldier_Campanha.blend')
code=code.replace("bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'FEB_Soldier_Campanha.blend'))", "bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'FEB_Soldier_Campanha.blend'))\nprint('CAMPAIGN_SAVED',flush=True)")
start=code.index("for name,loc,target,scale in [('rosto'")
code=code[:start]+"""views=[('rosto',(1,-4,1.84),(0,-.01,1.67),.40),('uniforme',(1.7,-6,2.1),(0,0,1.27),1.14),('corpo',(1.2,-7,2.1),(0,0,.95),2.22),('perfil',(4,0,1.70),(0,0,1.63),.46),('nuca',(1.1,4,1.85),(0,0,1.63),.46),('costas',(1.2,7,2),(0,0,.94),2.20)]
for name,loc,target,scale in views:
    if os.environ.get('FEB_VIEWS') and name not in os.environ['FEB_VIEWS'].split(','):continue
    camdata.ortho_scale=scale;aim(loc,target);scene.render.filepath=str(PREVIEW/(name+'.png'));bpy.ops.render.render(write_still=True)
campaign_manifest(stats)
print('CAMPAIGN_COMPLETE',json.dumps(stats),flush=True)
"""
exec(compile(code,str(src/'build_soldier_realism.py'),'exec'))
