"""Deterministic, tileable PBR atlas authored for the FEB reference model."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Art/Characters/FEB_Soldier/Textures'
OUT.mkdir(parents=True, exist_ok=True)
N = 512
rng = np.random.default_rng(1945)
palette = [
    ('Uniforme_verde_FEB', (111, 115, 91), .86, 0, 'cloth'),
    ('Lona_caqui_esverdeado', (145, 148, 95), .91, 0, 'webbing'),
    ('Couro_preto', (27, 29, 27), .57, 0, 'leather'),
    ('Aco_capacete_oliva', (62, 75, 48), .66, .62, 'paint'),
    ('Pele', (166, 120, 91), .60, 0, 'skin'),
    ('Ferragens_pretas', (34, 38, 35), .35, .8, 'metal'),
    ('Latão', (155, 123, 62), .29, .82, 'metal'),
    ('Borracha_sola', (20, 23, 22), .91, 0, 'leather'),
    ('Costura', (132, 134, 107), .95, 0, 'cloth'),
    ('Lábios', (137, 87, 73), .62, 0, 'skin'),
    ('Olho', (200, 192, 176), .32, 0, 'skin'),
    ('Íris_castanha', (53, 39, 24), .27, 0, 'metal'),
    ('Cabelo', (36, 29, 24), .85, 0, 'cloth'),
    ('Forro', (77, 74, 54), .9, 0, 'cloth'),
    ('Sombra_costuras', (77, 83, 65), .95, 0, 'cloth'),
    ('Aluminio_cantil', (144, 149, 147), .37, .95, 'metal'),
]
base = np.zeros((2048,2048,3),dtype=np.uint8)
normal = np.zeros_like(base)
rough = np.zeros((2048,2048),dtype=np.uint8)
metal = np.zeros_like(rough)
y,x=np.mgrid[:N,:N]
for i,(name,color,r,m,kind) in enumerate(palette):
    noise=rng.normal(0,1,(N,N))
    cloud=np.asarray(Image.fromarray(rng.integers(0,256,(32,32),dtype=np.uint8)).resize((N,N),Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(12))).astype(float)/255-.5
    weave=(np.sin(x*np.pi/2)*np.cos(y*np.pi/2))
    if kind in ('cloth','webbing'):
        h=.2*noise+.65*weave+.25*np.sin(y*np.pi/4)
        detail=2*noise+1.8*weave+11*cloud
        if kind=='webbing': detail+=2*np.sin(x*np.pi/8); h+=.5*np.sin(x*np.pi/8)
    elif kind=='skin':
        h=.13*noise; detail=1.4*noise+6*cloud
    elif kind=='paint':
        h=.35*noise; detail=2.2*noise+12*cloud
    else:
        h=.3*noise+cloud; detail=1.8*noise+7*cloud
    rgb=np.clip(np.array(color)[None,None,:]+detail[:,:,None],0,255).astype(np.uint8)
    dx=(np.roll(h,-1,1)-np.roll(h,1,1))*.17
    dy=(np.roll(h,-1,0)-np.roll(h,1,0))*.17
    v=np.stack([-dx,dy,np.ones_like(dx)],axis=-1); v/=np.linalg.norm(v,axis=-1,keepdims=True)
    a,b=(i//4)*N,(i%4)*N
    base[a:a+N,b:b+N]=rgb
    normal[a:a+N,b:b+N]=np.clip((v*.5+.5)*255,0,255).astype(np.uint8)
    rough[a:a+N,b:b+N]=np.clip((r+.025*noise+.05*cloud)*255,0,255)
    metal[a:a+N,b:b+N]=int(m*255)
Image.fromarray(base).save(OUT/'FEB_BaseColor.png')
Image.fromarray(normal).save(OUT/'FEB_Normal.png')
Image.fromarray(rough).save(OUT/'FEB_Roughness.png')
Image.fromarray(metal).save(OUT/'FEB_Metallic.png')
Image.fromarray(np.dstack([metal,metal,metal,255-rough])).save(OUT/'FEB_MetallicSmoothness_URP.png')
Image.fromarray(255-rough).save(OUT/'FEB_Smoothness.png')
import shutil
for source,target in [('reference_feb_patch.png','FEB_Patch.png'),('reference_5army_patch.png','FifthArmy_Patch.png')]:
    shutil.copyfile(Path(__file__).parent/source,OUT/target)
(Path(__file__).parent/'atlas_palette.txt').write_text('\n'.join(f'{i}: {p[0]}' for i,p in enumerate(palette)),encoding='utf-8')
print('Created 2048px PBR atlas and original PDF insignia textures:',OUT)
