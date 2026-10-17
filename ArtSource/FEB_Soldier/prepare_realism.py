from pathlib import Path
import zipfile, json
import numpy as np
R=Path(__file__).resolve().parent/'ReferenceAssets'
with zipfile.ZipFile(R/'skins02_cc0.zip') as z:
    for n in z.namelist():
        if 'mindfront_aksel_skin/' in n and not n.endswith('/'):
            (R/Path(n).name).write_bytes(z.read(n))
verts=[]; groups={}; group='none'
for line in (R/'mh_base.obj').read_text().splitlines():
    p=line.split()
    if not p:continue
    if p[0]=='v':verts.append(list(map(float,p[1:4])))
    elif p[0]=='g':group=p[1]
    elif p[0]=='f':groups.setdefault(group,set()).update(int(t.split('/')[0])-1 for t in p[1:])
v=np.array(verts)
for line in (R/'male-young.target').read_text().splitlines():
    p=line.split()
    if p and not p[0].startswith('#'):v[int(p[0])]+=np.array(list(map(float,p[1:4])))
for n in ['body','joint-head','joint-neck','joint-l-eye','joint-r-eye']:
    if n in groups:
        a=v[list(groups[n])]; print(n,len(a),'min',a.min(0),'max',a.max(0),'mean',a.mean(0))
print((R/'mindfront_aksel_skin.mhmat').read_text())
