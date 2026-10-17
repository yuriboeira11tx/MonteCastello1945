import bpy, math, random
from mathutils import Vector
globals().update(bpy.app.driver_namespace['ITAPAGE_BUILD'])
# Raked oval funnel, actual hollow uptake, collars, ladder and steam pipes.
f=Build();n=72
def funnelring(z,rx=2.0,ry=1.55):
    cx=-13.5-(z-12.2)*.12
    return [(cx+rx*math.cos(i*math.tau/n),ry*math.sin(i*math.tau/n),z) for i in range(n)]
for za,zb,ma in [(12.2,13,HULL),(13,23.6,HULL),(23.6,24.1,DARK)]:
    a=funnelring(za);b=funnelring(zb)
    f.part(a+b,[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],ma,True)
a=funnelring(24.1);b=funnelring(24.1,1.85,1.4);c=funnelring(21.6,1.85,1.4)
f.part(a+b+c,[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]+[(n+i,n+(i+1)%n,2*n+(i+1)%n,2*n+i) for i in range(n)],DARK,True)
f.part(c,[tuple(range(n))],BLACK)
for z in [12.6,16.0,19.4,23.7]:f.line(funnelring(z,2.035,1.58)+[funnelring(z,2.035,1.58)[0]],.035,DARK,8)
for sy in [-.36,.36]:f.line([(-15.6,sy,12.3),(-17.1,sy,24.05)],.045,DARK,8)
for i in range(32):
    z=12.5+i*.35;x=-13.5-(z-12.2)*.12-2.08;f.rod((x,-.36,z),(x,.36,z),.028,DARK,n=8)
for y in [-1.75,1.75]:
    f.rod((-12.25,y,12.2),(-13.5,y,22.85),.105,DARK,n=14)
    f.ring((-13.5,y,22.8),.13,.025,BRASS,axis=(0,0,1),n=20)
    for z in [14,17,20]:f.rod((-13.5-(z-12.2)*.12,y*.86,z),(-12.25-(z-12.2)*.12,y,z),.04,DARK)
f.object('Chamine | oval inclinada com abertura e escada','06')
# Pale five-pointed emblem derived from the supplied postcard; independently editable.
star=Build()
for s in [-1,1]:
    pts=[];zc=19.8;cx=-13.5-(zc-12.2)*.12
    for i in range(10):
        a=math.pi/2+i*math.pi/5;r=.66 if i%2==0 else .28
        x=cx+r*math.cos(a);z=zc+r*math.sin(a)
        localx=x-(-13.5-(z-12.2)*.12);y=s*(1.55*math.sqrt(max(0,1-(localx/2)**2))+.012);pts.append((x,y,z))
    star.part([(cx,s*1.57,zc)]+pts,[(0,1+i,1+(i+1)%10) for i in range(10)],TRIM)
star.object('Chamine | estrela do postal - aproximacao','07')
# Cowl ventilators: continuously swept bent pipes with flared mouths.
def cowl(b,x,y,z,height=2.1,r=.27,direction=1):
    path=[]
    for i in range(5):path.append((Vector((x,y,z+height*.7*i/4)),Vector((0,0,1)),r))
    rad=height*.29
    for i in range(1,13):
        a=i*math.pi/2/12
        path.append((Vector((x+direction*rad*(1-math.cos(a)),y,z+height*.7+rad*math.sin(a))),Vector((direction*math.sin(a),0,math.cos(a))),r*(1+.78*(i/12)**3)))
    verts=[];ns=20
    for p,t,rr in path:
        u=Vector((0,1,0));v=t.cross(u)
        for j in range(ns):verts.append(tuple(p+rr*(u*math.cos(j*math.tau/ns)+v*math.sin(j*math.tau/ns))))
    faces=[(i*ns+j,i*ns+(j+1)%ns,(i+1)*ns+(j+1)%ns,(i+1)*ns+j) for i in range(len(path)-1) for j in range(ns)]
    b.part(verts,faces,WHITE,True);p,t,rr=path[-1]
    b.ring(p,rr,.036,TRIM,axis=t,n=24)
    b.rod(p-t*.07,p-t*.02,rr*.9,BLACK,n=24)
    b.ring((x,y,z+.12),r*1.2,.035,TRIM,axis=(0,0,1),n=20)
vent=Build()
for x in [-40,-31,-19,-7,1]:
    for y in [-3.9,3.9]:cowl(vent,x,y,10.86,height=2.0 if x<-25 else 2.6,r=.3,direction=1)
for x in [34,44,-52]:
    for y in [-3.2,3.2]:cowl(vent,x,y,sheer(x),height=1.65,r=.23,direction=1)
vent.object('Ventiladores | cogumelos e pescocos de ganso','06')
# Eight wooden lifeboats with seats, ribs, rudders, oars, cradles and davits.
def lifeboat(cx,cy,z,index,cover=False):
    b=Build();ss=24;cs=16;v=[]
    def bp(u,angle,inside=False):
        fac=max(.035,math.sin(math.pi*u))**.66
        radius=1.08*fac-(.065 if inside else 0)
        x=cx+(u-.5)*7.25;y=cy+radius*math.sin(angle)
        zz=z+.36*(abs(2*u-1)**3)+1.0*(1-math.cos(angle))
        return (x,y,zz+(.085 if inside else 0))
    for inside in [False,True]:
        vs=[bp(i/ss,-math.pi/2+j*math.pi/cs,inside) for i in range(ss+1) for j in range(cs+1)]
        faces=[(i*(cs+1)+j,(i+1)*(cs+1)+j,(i+1)*(cs+1)+j+1,i*(cs+1)+j+1) for i in range(ss) for j in range(cs)]
        if inside:faces=[tuple(reversed(face)) for face in faces]
        b.part(vs,faces,WHITE if not inside else WOOD2,True)
    for s in [-1,1]:
        b.line([bp(i/ss,s*math.pi/2) for i in range(ss+1)],.07,WOOD,8)
        for ang in [.35,.65,.95,1.25]:b.line([bp(i/ss,s*ang) for i in range(ss+1)],.012,TRIM,6)
    if not cover:
        for u in [.2,.32,.44,.56,.68,.8]:
            x=cx+(u-.5)*7.25;w=.94*math.sin(math.pi*u)**.66
            b.box((x,cy,z+.82),(.24,w*2,.11),WOOD)
            pts=[bp(u,-math.pi/2+j*math.pi/20,True) for j in range(21)];b.line(pts,.034,WOOD,6)
        for dy in [-.2,.2]:
            b.rod((cx-2.6,cy+dy,z+1),(cx+2.3,cy+dy,z+1),.037,WOOD,n=8)
            b.box((cx+2.1,cy+dy,z+1),(.65,.16,.05),WOOD)
    else:
        vs=[]
        for i in range(25):
            u=i/24;w=1.08*max(.035,math.sin(math.pi*u))**.66
            for j in range(9):
                t=-1+j/4;vs.append((cx+(u-.5)*7.25,cy+t*w,z+1+.36*abs(2*u-1)**3+.23*(1-t*t)))
        b.part(vs,[(i*9+j,(i+1)*9+j,(i+1)*9+j+1,i*9+j+1) for i in range(24) for j in range(8)],CANVAS,True)
        for u in [.22,.5,.78]:
            x=cx+(u-.5)*7.25;w=1.08*math.sin(math.pi*u)**.66
            b.line([(x,cy-w,z+1.03),(x,cy,z+1.25),(x,cy+w,z+1.03)],.026,ROPE,8)
    for dx in [-2.1,2.1]:
        b.box((cx+dx,cy,z-.17),(.17,2.15,.3),WOOD)
        for s in [-1,1]:b.rod((cx+dx,cy+s*.85,z-.05),(cx+dx,cy+s*.95,z+.65),.055,DARK)
    b.object('Baleeira %02d | %s'%(index,'com lona' if cover else 'bancos e remos'),'04')
    d=Build();side=1 if cy>0 else -1
    for dx in [-2.4,2.4]:
        x=cx+dx;basey=side*4.75
        path=[(x,basey,10.87),(x,basey,13.0),(x,basey+side*.12,13.55),(x,basey+side*.5,13.95),(x,cy,14.08),(x,cy+side*.3,13.92)]
        d.line(path,.085,TRIM,12);d.rod((x,basey,10.75),(x,basey,11.1),.19,DARK,n=20)
        d.ring((x,cy+side*.28,13.84),.12,.04,BRASS,axis=(1,0,0),n=20)
        for dd in [-.07,.07]:d.rod((x+dd,cy,13.8),(x+dd,cy,z+1.16),.016,ROPE,n=6)
        d.line([(x,cy,z+1.14),(x-.4,cy-.64,z+.9)],.025,ROPE)
        d.line([(x,cy,z+1.14),(x+.4,cy+.64,z+.9)],.025,ROPE)
    d.object('Turcos %02d | roldanas e talhas'%index,'04')
for i,(cx,cy) in enumerate([(x,s*5.95) for x in [-37,-27,-5,4] for s in [-1,1]],1):lifeboat(cx,cy,11.35,i,cover=i%3==0)

mast=Build();rig=Build();fittings=Build()
def cable(a,b,sag=.3,r=.023,ma=DARK):
    a=Vector(a);b=Vector(b);pts=[]
    for i in range(25):
        t=i/24;p=a.lerp(b,t);p.z-=4*sag*t*(1-t);pts.append(tuple(p))
    rig.line(pts,r,ma,6)
for x,base,top in [(28,sheer(28),36.5),(-47,sheer(-47),29.5)]:
    foot=(x,0,base);head=(x-1.1,0,top)
    mast.rod(foot,(x-.7,0,top-8),.34,WHITE,r2=.19,n=24)
    mast.rod((x-.7,0,top-8),head,.19,WOOD2,r2=.065,n=18)
    mast.rod((x-.8,-4,top-6),(x-.8,4,top-6),.11,WHITE,r2=.11,n=16)
    mast.rod((x-.5,-2.0,top-10),(x-.5,2.0,top-10),.1,WHITE,n=12)
    for z in [base+.3,base+1.0,base+3.1,top-8]:mast.ring((x-(z-base)/(top-base)*1.1,0,z),.34 if z<base+4 else .2,.045,DARK,axis=(0,0,1),n=24)
    for sy in [-1,1]:
        for dx in [-7,8]:cable((x+dx,sy*min(width(x+dx)-.6,6.8),max(sheer(x+dx),10.8 if x<0 and x+dx>-44 else 0)),(x-.85,0,top-5),.14,.023)
        cable((x-1.1,0,top),(x-.8,sy*4,top-6),.12,.018)
        cable((x-.8,sy*3.7,top-6),(x-1,sy*2,base+.3),.16,.015,ROPE)
    # Paired cargo derricks, angled outward above the hatch.
    direction=1 if x>0 else -1
    for sy in [-1,1]:
        start=(x,sy*.45,base+2.7);end=(x+direction*12,sy*3.3,base+10.5)
        mast.rod(start,end,.14,WHITE,r2=.095,n=18)
        fittings.ring(start,.25,.055,DARK,axis=(0,1,0),n=20)
        cable((x-.7,0,top-10),end,.16,.025)
        cable(end,(end[0],end[1],base+1.4),.04,.027)
        fittings.ring((end[0],end[1],base+1.25),.17,.045,DARK,axis=(0,1,0),n=20)
    # Climbing rungs.
    for z in [base+1+i*.38 for i in range(int((top-base-9)/.38))]:
        mx=x-1.1*(z-base)/(top-base);mast.rod((mx-.29,-.23,z),(mx-.29,.23,z),.022,DARK,n=6)
cable((26.9,0,36.5),(-48.1,0,29.5),2.1,.022)
cable((26.9,.15,36.45),(-48.1,.15,29.45),2.4,.014)
cable((26.9,0,36.5),(58,0,sheer(58)+.8),.65,.027)
cable((-48.1,0,29.5),(-59,0,sheer(-59)+.8),.25,.027)
for s in [-1,1]:
    cable((-14.9,s*1.4,23.5),(-22,s*3.3,12.25),.08,.023)
    cable((-14.9,s*1.4,23.5),(-5,s*3.3,12.25),.08,.023)
mast.object('Mastros | vergas e paus de carga','05');rig.object('Cordame | estais adriças e antenas','05');fittings.object('Aparelho de carga | aneis e ganchos','05')
# Weather-deck railings.
r=Build()
for s in [-1,1]:
    rail(r,[(-44,s*7.22,10.8),(3,s*7.22,10.8)],1.0)
    rail(r,[(-46,s*6.5,7.8),(-49,s*6.1,7.8)],1.0)
rail(r,[(-44,-7.22,10.8),(-44,7.22,10.8)],1.0)
rail(r,[(7.6,-5.6,16.8),(20.4,-5.6,16.8),(20.4,5.6,16.8),(7.6,5.6,16.8),(7.6,-5.6,16.8)],.8)
r.object('Balaustradas | teto da ponte e conves dos botes','03')
print('STAGE2',len(scene.objects),'objects')
bpy.app.driver_namespace['ITAPAGE_BUILD']=globals().copy()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Itapage.blend'))
scene.render.filepath=str(OUT/'Itapage_WIP.png');bpy.ops.render.render(write_still=True)
