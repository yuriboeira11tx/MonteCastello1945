import bpy, math, random
from mathutils import Vector
globals().update(bpy.app.driver_namespace['ITAPAGE_BUILD'])
# Move the foremost boat pair clear of the bridge.
for o in scene.objects:
    if o.name.startswith(('Baleeira 07','Baleeira 08','Turcos 07','Turcos 08')):o.location.x-=19.5
# Hull portholes, drain scuppers and subtle salt/rust streaks.
b=Build();weather=Build()
for side in [-1,1]:
    for x in [i*2.65 for i in range(-20,20)]:
        y=side*(width(x)+.032);z=2.24
        b.rod((x,y,z),(x,y+side*.043,z),.172,BLACK,n=28)
        b.ring((x,y+side*.05,z),.19,.032,BRASS,axis=(0,side,0),n=28)
        b.ring((x,y+side*.075,z),.15,.009,DARK,axis=(0,side,0),n=24)
        if random.random()<.58:
            length=random.uniform(.22,.9);yy=y+side*.015
            weather.part([(x-.04,yy,z-.19),(x+.04,yy,z-.19),(x+.017,yy,z-.19-length),(x-.009,yy,z-.29-length*.4)],[(0,1,2,3)],RUST)
    for x in [-56,-52,29,34,39,44,49,54]:
        y=side*(width(x)+.035);z=sheer(x)+.18
        b.box((x,y,z),(.45,.035,.14),BLACK)
        weather.part([(x-.18,y+side*.022,z-.09),(x+.18,y+side*.022,z-.09),(x+.05,y+side*.022,z-.6),(x-.08,y+side*.022,z-.8)],[(0,1,2,3)],RUST)
b.object('Costado | vigias de bronze e embornais','01');weather.object('Pintura | escorrimentos localizados','01')
# Mooring bollards, fairleads, rope coils and ventilator mushrooms.
b=Build();ropes=Build()
for x in [-55,-50,29,43,52,56]:
    for side in [-1,1]:
        y=side*max(.8,width(x)-1.05);z=sheer(x)+.07
        b.box((x,y,z),(.95,.65,.15),DARK)
        for dx in [-.3,.3]:
            b.rod((x+dx,y,z),(x+dx,y,z+.65),.17,DARK,n=16)
            b.rod((x+dx,y,z+.63),(x+dx,y,z+.72),.23,DARK,n=20)
        for k in range(5):
            ropes.ring((x-.1,y-side*.65,z+.13+k*.045),.25+k*.04,.025,ROPE,axis=(0,0,1),n=24,m=6)
        if x in [29,43,-50]:
            b.rod((x+1.3,y,z),(x+1.3,y,z+.7),.16,WHITE,n=16)
            b.rod((x+1.3,y,z+.7),(x+1.3,y,z+.83),.31,WHITE,n=24)
b.object('Amarracao | cabecos e cogumelos','06',.01);ropes.object('Cordas | aduchas nos conveses','06')
# Cargo hatches with coamings, battens and fastening dogs.
b=Build()
for x,y,l,w in [(38,0,7.2,6.3),(48,0,4.5,5.2),(-52.5,0,4.9,4.2)]:
    z=sheer(x)+.09
    b.box((x,y,z+.4),(l,w,.8),DARK)
    b.box((x,y,z+.85),(l+.2,w+.15,.16),CANVAS)
    for i in range(9):b.box((x-l/2+i*l/8,y,z+.96),(.12,w+.18,.07),WOOD)
    for sy in [-1,1]:
        b.box((x,y+sy*(w/2+.08),z+.57),(l+.25,.09,.15),DARK)
        for j in range(7):b.box((x-l*.43+j*l*.86/6,y+sy*(w/2+.14),z+.7),(.1,.17,.23),BRASS)
b.object('Poroes | escotilhas e travessas de lona','06',.035)
# Steam winches / anchor windlass, with axle drums and operating wheels.
def winch(b,x,y,z,size=1):
    b.box((x,y,z+.12),(1.9*size,1.65*size,.24),DARK)
    for dx in [-.62,.62]:b.box((x+dx*size,y,z+.57),(.19*size,1.35*size,.9*size),HULL)
    b.rod((x-1.15*size,y,z+.85*size),(x+1.15*size,y,z+.85*size),.16*size,BRASS,n=20)
    for dx in [-.87,0,.87]:
        b.rod((x+(dx-.16)*size,y,z+.85*size),(x+(dx+.16)*size,y,z+.85*size),.44*size,DARK,n=32)
        for k in range(5):b.ring((x+(dx-.12+k*.06)*size,y,z+.85*size),.31*size,.03*size,ROPE,axis=(1,0,0),n=28,m=6)
    b.rod((x+.7*size,y+.5*size,z+.3),(x+.7*size,y+.5*size,z+1.45*size),.045,DARK)
    center=(x+.7*size,y+.5*size,z+1.47*size)
    b.ring(center,.28*size,.027,BRASS,axis=(0,0,1),n=28)
    for a in range(6):
        t=a*math.tau/6;b.rod(center,(center[0]+.27*size*math.cos(t),center[1]+.27*size*math.sin(t),center[2]),.02,BRASS)
b=Build()
for x,y in [(30.8,-2.3),(30.8,2.3),(-48.9,-2.8),(-48.9,2.8)]:winch(b,x,y,sheer(x),.85)
winch(b,53.8,0,sheer(53.8),1.0)
b.object('Guinchos | tambores engrenagens e comandos','06',.015)
# Anchor chains laid on deck and stockless anchors at both bow cheeks.
b=Build();a=Build()
for side in [-1,1]:
    for i in range(28):
        t=i/27;x=53.8+t*2.8;y=side*(.65+t*.65);z=sheer(x)+.2
        b.ring((x,y,z),.1,.028,DARK,axis=(0,0,1) if i%2==0 else (0,1,0),n=16,m=6)
    x=54.3;y=side*(width(x)+.12);z=3.9
    a.rod((x,y,z),(x,y,z-1.75),.1,DARK,n=16)
    a.ring((x,y,z+.1),.17,.05,DARK,axis=(0,1,0),n=24)
    a.rod((x-.72,y,z-1.5),(x+.72,y,z-1.5),.13,DARK,n=14)
    for dx in [-.66,.66]:
        a.part([(x+dx-.25,y-.18,z-1.5),(x+dx+.25,y-.18,z-1.5),(x+dx,y-.18,z-.97),(x+dx-.25,y+.18,z-1.5),(x+dx+.25,y+.18,z-1.5),(x+dx,y+.18,z-.97)],[(0,1,2),(3,5,4),(0,3,4,1),(1,4,5,2),(2,5,3,0)],DARK)
    a.ring((x,y-side*.04,z+.25),.34,.095,HULL,axis=(0,1,0),n=32)
b.object('Ancoras | correntes com elos','06');a.object('Ancoras | ferragens de proa','06',.025)
# External companionway stairs, real treads and railings.
stairs=Build()
def stair(x0,x1,y,z0,z1):
    n=13
    for i in range(n):
        t=(i+.5)/n;x=x0+(x1-x0)*t;z=z0+(z1-z0)*(i+1)/n
        stairs.box((x,y,z),((abs(x1-x0)/n)+.045,.95,.075),WOOD)
    for sy in [-.5,.5]:
        stairs.rod((x0,y+sy,z0),(x1,y+sy,z1),.07,WHITE,n=8)
        stairs.rod((x0,y+sy,z0+.95),(x1,y+sy,z1+.95),.035,TRIM,n=8)
        stairs.rod((x0,y+sy,z0+.5),(x1,y+sy,z1+.5),.022,TRIM,n=6)
        for t in [0,.33,.66,1]:
            x=x0+(x1-x0)*t;z=z0+(z1-z0)*t;stairs.rod((x,y+sy,z),(x,y+sy,z+.95),.029,TRIM,n=8)
for s in [-1,1]:
    stair(-49.3,-46,s*5.3,sheer(-49.3)+.1,7.78)
    stair(-47.3,-44,s*6.55,7.78,10.78)
    stairs.box((-46.6,s*6.55,7.67),(1.6,1.3,.18),WHITE)
stairs.object('Escadas | degraus e corrimaos','03',.012)
# Lifebuoys, lockers and promenade benches.
b=Build()
for x in [-40,-29,-18,-7,4,18]:
    for side in [-1,1]:
        y=side*7.29;z=8.45
        b.ring((x,y,z),.34,.09,RING,axis=(0,1,0),n=32,m=10)
        for a0 in [0,math.pi/2,math.pi,math.pi*1.5]:
            p=(x+.34*math.cos(a0),y,z+.34*math.sin(a0));b.ring(p,.093,.025,TRIM,axis=(-math.sin(a0),0,math.cos(a0)),n=12,m=6)
        b.line([(x-.36,y,z-.02),(x-.44,y,z-.45),(x+.44,y,z-.45),(x+.36,y,z-.02)],.018,ROPE)
for x in [-38,-25,-12,1,14]:
    for side in [-1,1]:
        y=side*6.06;z=7.8
        for i in range(4):b.box((x,y-.28+i*.16,z+.46),(1.7,.12,.08),WOOD)
        for i in range(3):b.box((x,y-side*.34,z+.75+i*.16),(1.7,.07,.12),WOOD)
        for xx in [-.63,.63]:
            for yy in [-.25,.25]:b.rod((x+xx,y+yy,z),(x+xx,y+yy,z+.47),.035,DARK)
b.object('Seguranca e passeio | boias e bancos','06',.012)
# Twin screw underwater stern, bronze propellers and rudder.
b=Build()
for side in [-1,1]:
    yy=side*2.3;zz=-3.9
    b.rod((-43,yy*.8,zz+.3),(-56.2,yy,zz),.24,DARK,n=24)
    b.rod((-55.3,yy,zz),(-56.4,yy,zz),.42,BRASS,r2=.16,n=28)
    for blade in range(4):
        vs=[]
        for i in range(14):
            t=i/13;r=.35+t*1.34;ang=blade*math.pi/2+.35*t
            for j in range(7):
                u=-1+j/3;chord=.52*math.sin(math.pi*(.08+.9*t))
                vs.append((-55.7+u*chord*.53,yy+r*math.cos(ang)-u*chord*math.sin(ang),zz+r*math.sin(ang)+u*chord*math.cos(ang)))
        b.part(vs,[(i*7+j,(i+1)*7+j,(i+1)*7+j+1,i*7+j+1) for i in range(13) for j in range(6)],BRASS,True)
    b.rod((-50,yy,zz),(-48,yy*.4,-1.8),.11,DARK,n=14)
b.box((-56.5,0,-3.45),(2.4,.22,4.8),HULL)
b.rod((-55.6,0,-5.7),(-55.6,0,-.2),.18,DARK,n=18)
b.object('Obras vivas | dois helices e leme','01',.015)
# Bridge roof equipment, lamps and navigation lights.
b=Build()
b.rod((14,0,16.83),(14,0,17.7),.23,WHITE,n=20)
b.rod((14,0,17.7),(14,0,18.05),.36,BRASS,n=24)
b.rod((16,0,16.83),(16,0,18.1),.07,DARK,n=12)
b.rod((16,0,18.1),(16.45,0,18.1),.12,BRASS,r2=.23,n=20)
for side,ma in [(-1,RING),(1,GREEN)]:
    b.box((21,side*7.25,14.2),(.6,.28,.45),DARK)
    b.rod((21.03,side*7.4,14.05),(21.03,side*7.4,14.35),.15,ma,n=18)
for x in [-40,-30,-20,-10,0,10,20]:
    for side in [-1,1]:
        b.rod((x,side*5.6,9.94),(x,side*5.6,10.16),.1,BRASS,n=14)
        b.rod((x,side*5.6,9.78),(x,side*5.6,9.94),.13,TRIM,n=14)
b.object('Navegacao | bussola apito e luminarias','06')

def textobj(name,body,loc,size,rotation,ma=TRIM):
    d=bpy.data.curves.new(name,'FONT');d.body=body;d.size=size;d.align_x='CENTER';d.extrude=.004;d.bevel_depth=.002;d.space_character=1.15
    o=bpy.data.objects.new(name,d);COL['07'].objects.link(o);o.location=loc;o.rotation_euler=rotation;d.materials.append(M[ma]);return o
for side in [-1,1]:
    x=47;z=3.8;y=side*(width(x)+.065)
    # Text follows the local tangent to the hull and faces outward.
    o=textobj('Nome | %s'%('bombordo' if side<0 else 'boreste'),'ITAPAGÉ',(x,y,z),.68,(math.pi/2 if side<0 else -math.pi/2,0,0))
    if side>0:o.rotation_euler.z=math.pi
    tangent=(width(x+.05)-width(x-.05))/.1
    o.rotation_euler.z=math.atan(side*tangent)+(math.pi if side>0 else 0)
textobj('Popa | nome','ITAPAGÉ',(-59.4,0,3.6),.55,(math.pi/2,0,-math.pi/2))
textobj('Popa | porto de registro','RIO DE JANEIRO',(-59.42,0,2.9),.25,(math.pi/2,0,-math.pi/2))
for side in [-1,1]:
    for j in range(9):
        z=-2.7+j*.6;x=56.6;y=side*(width(x)+.06)
        textobj('Calado | marca %d %d'%(side,j),str(j+1),(x,y,z),.22,(math.pi/2 if side<0 else -math.pi/2,0,0))
print('STAGE3',len(scene.objects),'objects')
bpy.app.driver_namespace['ITAPAGE_BUILD']=globals().copy()
scene.camera=bpy.data.objects['Camera | tres quartos'];bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Itapage.blend'))
scene.render.filepath=str(OUT/'Itapage_WIP.png');bpy.ops.render.render(write_still=True)
