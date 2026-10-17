"""Realism revision of the FEB T pose, retaining the earlier version."""
from pathlib import Path
import os
source=Path(__file__).resolve().parent/'build_soldier.py'
original=source.read_text(encoding='utf-8')
exec(compile(original[:original.index('# Jacket and trousers')],str(source),'exec'))
REAL_TEX=ASSET/'Realista/Textures';REAL_TEX.mkdir(parents=True,exist_ok=True)
REAL_MODELS=ASSET/'Realista/Models';REAL_MODELS.mkdir(parents=True,exist_ok=True)
PREVIEW=SRC/'Previews_Realista';PREVIEW.mkdir(exist_ok=True)
exec(compile((SRC/'realism_materials.py').read_text(encoding='utf-8'),str(SRC/'realism_materials.py'),'exec'))

body=original[original.index('# Jacket and trousers'):original.index('# Head and facial features')]
body=body.replace('(1.42,.213,.114),(1.48,.211,.101),(1.515,.163,.087),(1.55,.075,.062)', '(1.42,.213,.114),(1.48,.235,.109),(1.515,.225,.098),(1.54,.125,.073),(1.56,.059,.051)')
body=body.replace('(.855,.145,.085),(.91,.161,.105),(.99,.164,.108),(1.045,.158,.102)', '(.87,.116,.064),(.92,.152,.101),(.99,.164,.108),(1.045,.158,.102)')
body=body.replace('(.165,.089,.083),(.21,.091,.087),(.28,.086,.082),(.36,.079,.075)', '(.145,.085,.081),(.20,.092,.082),(.28,.084,.077),(.36,.073,.068)')
body=body.replace('(.475,.077,.071),(.50,.067,.063),(.53,.070,.065)', '(.475,.066,.061),(.50,.065,.061),(.53,.064,.060)')
body=body.replace('wrinkle=.025','wrinkle=0').replace('wrinkle=.035','wrinkle=0').replace('wrinkle=.04','wrinkle=0')
body='\n'.join(line for line in body.splitlines() if "cord('Costura_lateral_calca." not in line)
start=body.index('    for z in [.445,.49,.565]:');end=body.index('    # Boots:',start)
body=body[:start]+body[end:]
# Less rounded boots with a continuous vamp and flatter soles.
body=body.replace('(.057,.104,.047)','(.056,.098,.037)').replace('(footx,-.108,.069)','(footx,-.103,.071)')
body=body.replace('(.052,.085,.072)','(.05,.088,.062)').replace('(footx,-.025,.104)','(footx,-.029,.099)')
body=body.replace(".0011,8,'Foot.'", ".0007,2,'Foot.'")
# Rebuild the collar as shaped panels, eliminating flat zig-zag ribbons.
start=body.index("    ribbon('Gola_M41'");end=body.index("    box('Ajuste_cintura'",start)
body=body[:start]+'''    cv=[(sign*.032,-.043,1.57),(sign*.060,-.04,1.568),(sign*.102,-.083,1.53),(sign*.066,-.127,1.488),(sign*.025,-.118,1.532)]
    collar=mesh_obj('Gola_M41_estruturada',cv,[(0,1,2,3,4)],0)
    bpy.context.view_layer.objects.active=collar
    so=collar.modifiers.new('Tecido duplo','SOLIDIFY');so.thickness=.002;bpy.ops.object.modifier_apply(modifier=so.name)
    be=collar.modifiers.new('Borda arredondada','BEVEL');be.width=.002;be.segments=3;bpy.ops.object.modifier_apply(modifier=be.name)
    stitched('Pesponto_gola',[(x,y-.001,z) for x,y,z in cv+[cv[0]]])
''' +body[end:]
body=body.replace("(sign*.13,-.084,1.515),(sign*.13,0,1.54),(sign*.125,.087,1.49)","(sign*.13,-.085,1.531),(sign*.13,0,1.558),(sign*.125,.087,1.524)")
body=body.replace("(sign*.13,.025,1.51),(sign*.19,.075,1.30),(sign*.18,.088,1.16)","(sign*.13,.025,1.55),(sign*.172,.072,1.40),(sign*.18,.088,1.16)")
exec(compile(body,str(source),'exec'))
head=anatomical_head()

mid=original[original.index('# M1 shell'):original.index('# Consistent outward normals')]
mid=mid.replace('range(13)','range(25)').replace('i/12','i/24').replace("helmet,3,'Head',segments=48", "helmet,3,'Head',segments=96")
mid=mid.replace('1.549-.004*(2*u-1)**2','1.544-.004*(2*u-1)**2')
mid=mid.replace("mod.iterations=4", "mod.iterations=6")
mid=mid.replace("mod.ratio=.08", "mod.ratio=.18 if tile==0 else .12")
exec(compile(mid,str(source),'exec'))

# Compression and tension folds are part of the surface, with varied widths.
from math import exp,atan2
def gaussian(x,w):return exp(-(x/w)**2)
for ob in pieces:
    if ob.name not in ['Gandola_M41_continua','Calca_M43_continua']:continue
    for v in ob.data.vertices:
        p=v.co;x=abs(p.x);d=0
        if ob.name.startswith('Gandola'):
            if x>.235:
                t=atan2(p.z-1.465,p.y)
                for pos,amp,w,phase in [(.435,.003,.014,.3),(.475,.004,.012,2),(.515,.0035,.015,4),(.605,.0025,.022,1),(.674,.003,.013,3)]:
                    centre=pos+.016*sin(t+phase)
                    d+=amp*(gaussian(x-centre,w)-.55*gaussian(x-centre-w*1.25,w*.8))
                d+=.0015*sin(t*5+x*30)*gaussian(x-.40,.19)
                direction=Vector((0,p.y,p.z-1.465)).normalized()
            else:
                direction=Vector((p.x,p.y,0)).normalized()
                front=max(0,min(1,-p.y/.09));back=max(0,min(1,p.y/.09))
                for pos,amp in [(.073,.003),(.135,.004),(.184,.003)]:
                    d+=amp*gaussian(x-(pos+.018*sin(p.z*20)),.011)*gaussian(p.z-1.26,.17)*(front+back)
                # Diagonal pulling below the raised upper arms.
                q=p.z-(1.35+.48*(x-.1))
                d+=.004*(gaussian(q,.014)-.6*gaussian(q-.023,.013))*gaussian(x-.15,.11)
                # Gathered material over the ammunition belt.
                d+=.003*sin(x*140+.5)*gaussian(p.z-1.168,.034)
        else:
            s=1 if p.x>0 else -1;cx=s*(.092 if p.z>.7 else .105)
            direction=Vector((p.x-cx,p.y-.006,0)).normalized();t=atan2(p.y-.006,p.x-cx)
            for pos,amp,w,phase in [(.416,.005,.011,1),(.452,.0045,.014,3),(.502,.004,.016,.5),(.555,.004,.019,3.4),(.622,.002,.029,2)]:
                centre=pos+.024*sin(t+phase)
                d+=amp*(gaussian(p.z-centre,w)-.55*gaussian(p.z-centre-w*1.3,w*.8))
            d+=.0025*cos(t*3+.4)*gaussian(p.z-.77,.14)
        v.co+=direction*d
        if ob.name.startswith('Calca') and p.y<0:
            # Fill the shallow hollow left by the hip/leg construction union.
            # This keeps the fly and upper thighs on one continuous cloth surface.
            front=max(0,min(1,(-p.y-.015)/.045))
            v.co.y-=.027*gaussian(p.x,.09)*gaussian(p.z-.895,.065)*front
    ob.data.update()

# Fine garment seams, with short thread segments following the cloth.
for s,side in [(1,'L'),(-1,'R')]:
    stitched('Pesponto_punho.'+side,[(s*.708,.049*cos(t),1.465+.046*sin(t)) for t in [i*2*pi/56 for i in range(57)]],'Forearm.'+side)
    for zz in [1.032,1.044]:
        stitched('Barra_gandola',[(.178*cos(t),.113*sin(t),zz) for t in [i*2*pi/80 for i in range(81)]],'Hips')
    # Pocket welt kept in the M41 lower position.
    cord('Bainha_bolso',[(s*.068,-.119,1.175),(s*.137,-.105,1.213)],.002,0,'Chest')
    stitched('Costura_bolso',[(s*.068,-.122,1.174),(s*.137,-.108,1.212)])
    # Sleeve longitudinal construction seam.
    pts=[(s*x,-a-.0008,1.461) for x,a in [(.29,.084),(.36,.073),(.43,.073),(.49,.067),(.58,.067),(.64,.057),(.70,.047)]]
    # The edge sits beneath the sleeve; keep it subtle to avoid a floating line.
    # Construction line omitted where the cloth folds would expose a floating cord.

rigcode=original[original.index('# Consistent outward normals'):original.index('# Export only the character')]
rigcode=rigcode.replace("rig.show_in_front=True","rig.show_in_front=False")
rigcode=rigcode.replace("ob.name=['FEB_Uniforme_Corpo_Equipamento','FEB_Distintivo_Esquerdo','FEB_Distintivo_Direito'][i]", "ob.name='FEB_'+objects[0].data.materials[0].name.split(' • ')[-1]")
exec(compile(rigcode,str(source),'exec'))
rig['Revisao']='Rosto anatomico com UV e normal dedicados; dobras integradas; materiais fisicos separados.'

# Save editable shaders in Blender. Exports are completed by the texture baking step.
studio_code=original[original.index('# Studio for honest inspection'):original.index("stats={'vertices'")]
studio_code=studio_code.replace("scene.cycles.samples=32", "scene.cycles.samples=64")
studio_code=studio_code.replace("resolution_x=1600", "resolution_x=1400").replace("resolution_y=1600", "resolution_y=1400")
studio_code=studio_code.replace("450,(1,.86,.69),4", "380,(1,.94,.85),2.3").replace("260,(.71,.83,1),3", "170,(.80,.88,1),2")
exec(compile(studio_code,str(source),'exec'))
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.overlay.show_overlays=False
            area.spaces.active.shading.studiolight_rotate_z=.45
stats={'vertices':sum(len(o.data.vertices) for o in joined),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in joined),'bones':len(arm.bones),'mesh_objects':len(joined),'materials':len(groups)}
stats['meshes']={o.name:sum(len(p.vertices)-2 for p in o.data.polygons) for o in joined}
(SRC/'model_report_realista.json').write_text(json.dumps(stats,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'FEB_Soldier_Realista.blend'))
studio.hide_viewport=False
draft=os.environ.get('FEB_DRAFT','')=='1'
if draft:
    scene.cycles.samples=24;scene.render.resolution_x=1000;scene.render.resolution_y=1000
for name,loc,target,scale in [('rosto',(1,-4,1.9),(0,-.01,1.687),.39),('uniforme',(2,-6,2.3),(0,0,1.30),1.13),('corpo',(2.3,-7,2.4),(0,0,.96),2.35)]:
    camdata.ortho_scale=scale;aim(loc,target);scene.render.filepath=str(PREVIEW/(name+'.png'));bpy.ops.render.render(write_still=True)
print('REALISM_COMPLETE',json.dumps(stats))
