import bpy,json,numpy as np,hashlib,struct
from pathlib import Path
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
records=json.loads((out/'color_components.json').read_text())
strap_regions=[(.3472,.1992,.4724,.2354),(.3475,.2413,.4719,.2749),(.1166,.5078,.1336,.6231),(.131,.1672,.149,.2828),(.209,.6203,.2203,.7457),(.2235,.6257,.2349,.7445),(.1342,.1973,.1454,.2429),(.119,.5224,.1304,.5852),(.2868,.0066,.3255,.0287),(.3116,.0642,.3269,.1408)]
hardware_regions=[(.1516,.2353,.1646,.2724)]
def within(uv,r):
 return all([uv[0][0]>=r[0]-.0001,uv[0][1]>=r[1]-.0001,uv[1][0]<=r[2]+.0001,uv[1][1]<=r[3]+.0001])
selection={};summary={}
for record in records:
 name=record['name']
 if not any(t in name for t in ('Corpo_uniforme','M41_parte_superior','Calca_perneiras_botinas')):continue
 cats={k:[] for k in ('calca','botas','suspensorios','ferragens')}
 for c in record['components']:
  zmin,zmax=c['bounds'][0][2],c['bounds'][1][2];uv=c['uv'];cat=None
  if zmax<.12:cat='botas'
  elif zmin>.25 and zmax<.99 and within(uv,(0,0,.311,.27)):cat='calca'
  elif zmin>1.04 and any(within(uv,r) for r in strap_regions):cat='suspensorios'
  elif zmin>1.04 and any(within(uv,r) for r in hardware_regions):cat='ferragens'
  if cat:cats[cat].extend(c['polygons'])
 selection[name]=cats;summary[name]={k:len(v) for k,v in cats.items()}
(out/'color_selection.json').write_text(json.dumps(selection,indent=2))
src=bpy.data.materials['FEB_Soldiers_Uniforme']
im=next(n.image for n in src.node_tree.nodes if n.type=='TEX_IMAGE' and n.image)
px=np.empty(im.size[0]*im.size[1]*4,dtype=np.float32);im.pixels.foreach_get(px);px=px.reshape(im.size[1],im.size[0],4)
stats={}
for name,rect in dict(calca=(.02,.02,.12,.24),suspensorios=(.36,.25,.46,.27),botas=(.25,.70,.28,.80)).items():
 a=px[int(rect[1]*im.size[1]):int(rect[3]*im.size[1]),int(rect[0]*im.size[0]):int(rect[2]*im.size[0]),:3]
 stats[name]={'median':np.median(a,axis=(0,1)).tolist(),'p90':np.percentile(a,90,axis=(0,1)).tolist()}
result={'selection':summary,'image_stats':stats}
