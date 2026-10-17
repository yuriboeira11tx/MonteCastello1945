import bpy,numpy as np,hashlib,struct

def read_pixels(im):
 w,h=im.size;a=np.empty(w*h*4,np.float32);im.pixels.foreach_get(a);return a.reshape(h,w,4)

def mesh_hash(ob):
 h=hashlib.sha256();me=ob.data
 for v in me.vertices:h.update(struct.pack('3f',*v.co))
 for p in me.polygons:h.update(struct.pack('%di'%len(p.vertices),*p.vertices))
 for uv in me.uv_layers:
  for d in uv.data:h.update(struct.pack('2f',*d.uv))
 for row in ob.matrix_world:h.update(struct.pack('4f',*row))
 return h.hexdigest()

def dilate(mask,n=6):
 m=mask.copy()
 for _ in range(n):
  e=m.copy()
  for dy,dx in ((-1,0),(1,0),(0,-1),(0,1),(-1,-1),(-1,1),(1,-1),(1,1)):
   e|=np.roll(np.roll(m,dy,axis=0),dx,axis=1)
  m=e
 return m

def raster(ob,faces,w,h):
 me=ob.data;me.calc_loop_triangles();uv=me.uv_layers.active.data;m=np.zeros((h,w),bool);selected=set(faces)
 for tri in me.loop_triangles:
  if tri.polygon_index not in selected:continue
  pts=np.array([[uv[l].uv.x*w,uv[l].uv.y*h] for l in tri.loops])
  lo=np.maximum(np.floor(pts.min(axis=0)).astype(int),0);hi=np.minimum(np.ceil(pts.max(axis=0)).astype(int),[w-1,h-1])
  if np.any(hi<lo):continue
  xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
  cross=[]
  for j in range(3):
   a,b=pts[j],pts[(j+1)%3];cross.append((b[0]-a[0])*(yy-a[1])-(b[1]-a[1])*(xx-a[0]))
  m[lo[1]:hi[1]+1,lo[0]:hi[0]+1]|=(np.minimum.reduce(cross)>=-.001)|(np.maximum.reduce(cross)<=.001)
 return m

def smooth(a,b,x):
 t=np.clip((x-a)/(b-a),0,1);return t*t*(3-2*t)

def save_image(name,path,pixels):
 h,w=pixels.shape[:2]
 im=bpy.data.images.get(name)
 if im is None:im=bpy.data.images.new(name,width=w,height=h,alpha=True)
 if im.packed_file:im.unpack(method='REMOVE')
 im.colorspace_settings.name='sRGB';im.pixels.foreach_set(pixels.astype(np.float32).ravel());im.update()
 im.filepath_raw=str(path);im.file_format='PNG';im.save();im.pack();return im

def uvgrid(w,h):
 # Image convention: x from left and y from top, both normalized.
 return np.meshgrid((np.arange(w)+.5)/w,1-(np.arange(h)+.5)/h)

def polygon_mask(x,y,points):
 inside=np.zeros(x.shape,bool)
 for (a,b),(c,d) in zip(points,points[1:]+points[:1]):
  if abs(d-b)<1e-10:continue
  inside^=((b>y)!=(d>y))&(x<((c-a)*(y-b)/(d-b)+a))
 return inside

def face_mask(pixels,kind):
 h,w=pixels.shape[:2];x,y=uvgrid(w,h);rgb=pixels[:,:,:3]
 lum=rgb@np.array([.2126,.7152,.0722]);r,g,b=rgb[:,:,0],rgb[:,:,1],rgb[:,:,2]
 # Soft color key preserves dark individual hair strands and painted creases.
 weight=smooth(.075,.255,lum)*smooth(.005,.052,r-g)
 skin_key=weight.copy()
 if kind=='braebury':
  face=polygon_mask(x,y,[(.27,.20),(.32,.12),(.44,.075),(.70,.07),(.83,.12),(.91,.22),(.995,.26),(.98,.41),(.85,.52),(.70,.63),(.48,.63),(.32,.48),(.20,.36),(.20,.28)])
  neck=polygon_mask(x,y,[(.45,.64),(.80,.61),(.99,.67),(.99,.80),(.82,.82),(.73,.97),(.60,.99),(.60,.87),(.50,.78)])
  ears=((x>.74)&(y>.80))
  weight*=face|neck|ears
  for cx,cy,rx,ry in [(.468,.189,.060,.014),(.711,.189,.061,.015),(.921,.550,.088,.095)]:
   weight[((x-cx)/rx)**2+((y-cy)/ry)**2<1]=0
 elif kind=='david':
  face=polygon_mask(x,y,[(0,.55),(.12,.55),(.16,.44),(.20,.39),(.25,.44),(.29,.40),(.34,.32),(.37,.25),(.43,.27),(.49,.21),(.55,.28),(.61,.26),(.67,.31),(.71,.42),(.77,.45),(.79,.39),(.85,.40),(.88,.55),(1,.55),(1,.94),(.83,.94),(.81,1),(.20,1),(0,.96)])
  weight*=face
  # Existing eye whites, iris, teeth and mouth interior remain exactly original.
  for cx,cy,rx,ry in [(.092,.918,.069,.031),(.909,.918,.072,.031),(.427,.410,.027,.008),(.574,.410,.028,.008)]:
   weight[((x-cx)/rx)**2+((y-cy)/ry)**2<1]=0
  weight[(x>.84)&(y>.67)&(y<.79)]=0
  weight[(x>.62)&(x<.84)&(y>.947)]=0
 else:
  # These three heads use the same main UV layout: face/neck below the hair.
  top={'mo':.282,'randall':.294,'wolf':.235}[kind]
  weight*=y>top
  if kind=='wolf':
   weight[(x<.17)&(y>.86)]=0
   # Recolor only the eyelid rim around the separate eyeball UV island.
   rim=(((x-.081)/.069)**2+((y-.936)/.065)**2<1)
   white=(((x-.081)/.056)**2+((y-.941)/.030)**2<1)
   weight[rim&~white]=skin_key[rim&~white]
   eyes=[(.367,.458,.044,.004),(.632,.458,.044,.004)]
  else:eyes=[(.365,.505,.044,.004),(.635,.505,.044,.004)]
  for cx,cy,rx,ry in eyes:weight[((x-cx)/rx)**2+((y-cy)/ry)**2<1]=0
 # Preserve eyebrows by a soft dark-pigment threshold, avoiding recoloring hairs.
 return weight.astype(np.float32)

def recolor_face(pixels,kind,target):
 weight=face_mask(pixels,kind);rgb=pixels[:,:,:3];h,w=pixels.shape[:2];x,y=uvgrid(w,h)
 if kind=='braebury':sample=(x>.76)&(x<.84)&(y>.29)&(y<.39)
 elif kind=='david':sample=(x>.60)&(x<.67)&(y>.45)&(y<.51)
 else:sample=(x>.61)&(x<.69)&(y>.55)&(y<.60)
 sample&=weight>.95
 anchor=np.median(rgb[sample],axis=0)
 # Multiplicative chromatic adaptation preserves all pores, creases and blemishes.
 scaled=rgb*(np.asarray(target)/anchor)
 result=pixels.copy();result[:,:,:3]=np.clip(rgb+(scaled-rgb)*weight[:,:,None],0,1)
 return result,weight,anchor
