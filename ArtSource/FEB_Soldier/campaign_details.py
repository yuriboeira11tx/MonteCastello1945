"""Editable shaping decisions for the user-supplied FEB photographic references."""
from math import exp,tanh,sqrt,atan2
import bmesh,hashlib

def g(x,w):return exp(-(x/w)**2)
def clamp(v):return max(0,min(1,v))
def smooth(v):
    v=clamp(v);return v*v*(3-2*v)

def campaign_face_transform(p):
    x,y,z=p;side=1 if x>=0 else -1
    # Simple cervical profile: the lower nape stays inside the collar.
    neck=smooth((1.655-z)/.065)
    if y>0:
        limit=.044+.018*smooth((z-1.55)/.10)
        y=y*(1-neck)+limit*tanh(y/limit)*neck
    # Individual features inspired by the reference faces, not a nationality preset.
    front=smooth((-y+.005)/.045)
    cheek=.0045*g(z-1.678,.030)*tanh(abs(x)/.018)
    jaw=.0032*g(z-1.615,.025)*tanh(abs(x)/.020)
    x+=side*(cheek+jaw)*front
    nose=g(z-1.671,.013)*g(x,.021)*front
    x+=x*.19*nose
    y+=.0025*g(z-1.692,.028)*g(x,.012)*front
    mouth=g(z-1.642,.012)*g(x,.025)*front
    x+=x*.055*mouth;y-=.0015*mouth
    y-=.0018*g(z-1.725,.010)*g(abs(x)-.031,.022)*front
    # Small natural asymmetry, with eye deformation shared by the eyeball.
    y+=.0007*side*g(z-1.66,.045)*g(abs(x)-.03,.03)*front
    return (x,y,z)

def campaign_materials():
    mats[0]=pbr('01 • Jaqueta de campanha FEB',(115,111,85),.80)
    mats[1]=pbr('02 • Lona caqui',(137,128,98),.84,kind='canvas')
    mats.append(pbr('19 • Calca oliva',(75,80,62),.83))
    # Reduce broad mottling: variation comes mostly from cloth structure and folds.
    for m in [mats[0],mats[1],mats[16]]:
        ramp=next(n for n in m.node_tree.nodes if n.type=='VALTORGB')
        col=m.diffuse_color[:3]
        ramp.color_ramp.elements[0].color=(*[x*.91 for x in col],1)
        ramp.color_ramp.elements[1].color=(*[x*1.09 for x in col],1)

def campaign_body_source(s):
    # Field jacket extends below the web belt, as in the supplied reference model.
    s=s.replace('(1.00,.171,.107),(1.025,.179,.111),(1.07,.177,.112),(1.12,.165,.106),(1.18,.163,.108)',
        '(.905,.191,.139),(.924,.190,.136),(.97,.182,.124),(1.025,.169,.108),(1.11,.167,.110),(1.18,.173,.114)')
    s=s.replace("(.145,.085,.081),(.20,.092,.082),(.28,.084,.077),(.36,.073,.068)","(.125,.087,.078),(.20,.088,.079),(.28,.080,.075),(.36,.073,.067)")
    # Four-pocket field jacket, retaining the reference emblems and equipment.
    s='\n'.join(line for line in s.splitlines() if "'Bolso_inferior'" not in line)
    s=s.replace("[(0,-.111,1.015),(0,-.112,1.12),(0,-.12,1.25),(0,-.126,1.39),(0,-.118,1.49)]", "[(0,-.118,.92),(0,-.12,1.025),(0,-.12,1.25),(0,-.126,1.39),(0,-.118,1.49)]")
    s=s.replace("[1.05,1.13,1.22,1.31,1.40,1.475]", "[.96,1.06,1.18,1.29,1.40,1.475]")
    s=s.replace("(sign*.102,-.083,1.53),(sign*.066,-.127,1.488),(sign*.025,-.118,1.532)","(sign*.079,-.081,1.534),(sign*.054,-.124,1.505),(sign*.022,-.116,1.536)")
    return s

def campaign_equipment_position():
    # The gear moves as a set so its proportions and attachment points agree.
    waist_prefix=('Cinto_interno','Fivela_latao','Cinturao_NA','Fecho_cinturao','Bolsa_municao','Tampa_municao','Botao_pressao','Cantil_','Porta_cantil','Tampa_cantil','Aba_porta_cantil','Pressao_porta_cantil','Corrente_cantil','Porta_curativo','Aba_porta_curativo','Pressao_curativo','Mosquetao','Regulador_suspensorio')
    for ob in pieces:
        if ob.name.startswith(waist_prefix):ob.location.z-=.085
        elif ob.name.startswith(('Suspensorio_NA','Costura_suspensorio','Tira_lateral')):
            for v in ob.data.vertices:v.co.z-=.085*(1-smooth((v.co.z-1.17)/.37))

def campaign_folds():
    for ob in pieces:
        if ob.name not in ['Gandola_M41_continua','Calca_M43_continua']:continue
        if ob.name.startswith('Calca'):
            ob.data.materials.clear();ob.data.materials.append(mats[16])
        for v in ob.data.vertices:
            p=v.co;x=abs(p.x);sgn=1 if p.x>0 else -1;d=0
            if ob.name.startswith('Gandola'):
                if x>.225:
                    t=atan2(p.z-1.465,p.y);phase=.45 if sgn>0 else -.28
                    for pos,amp,w,shift in [(.30,.0025,.024,1),(.425,.0045,.022,2.6),(.477,.0055,.019,.5),(.535,.004,.023,3.4),(.63,.004,.022,1.5),(.686,.0045,.017,4)]:
                        center=pos+.024*sin(t+shift+phase)
                        d+=amp*(g(x-center,w)-.55*g(x-center-w*1.15,w*.75))
                    direction=Vector((0,p.y,p.z-1.465)).normalized()
                else:
                    direction=Vector((p.x,p.y,0)).normalized()
                    fb=smooth((abs(p.y)-.025)/.07)
                    for pos,amp in [(.052,.0035),(.11,.005),(.166,.0035)]:
                        d+=amp*g(x-(pos+.025*sin(p.z*16+sgn)),.014)*g(p.z-1.23,.20)*fb
                    # Raised arms pull diagonal folds into the chest and underarm.
                    for dz in [0,.052]:
                        q=p.z-(1.35+dz+.60*(x-.1))
                        d+=.005*(g(q,.017)-.45*g(q-.025,.012))*g(x-.17,.12)*fb
                    d+=.004*sin(x*105+sgn*.4)*g(p.z-1.078,.045)
                    d+=.002*sin(x*45)*g(p.z-.94,.045)
            else:
                cx=sgn*(.092 if p.z>.7 else .105);t=atan2(p.y-.006,p.x-cx)
                direction=Vector((p.x-cx,p.y-.006,0)).normalized()
                d+=.004*g(p.z-.74,.14)
                for pos,amp,w,phase in [(.412,.006,.012,.8),(.45,.008,.014,3.0),(.50,.006,.018,.3),(.565,.005,.024,3.6),(.68,.003,.035,1.8)]:
                    center=pos+.025*sin(t+phase+sgn*.16)
                    d+=amp*(g(p.z-center,w)-.55*g(p.z-center-w*1.15,w*.80))
                d+=.003*cos(t*3+.5)*g(p.z-.78,.17)
                if p.y<0:p.y-=.027*g(p.x,.09)*g(p.z-.895,.065)*smooth((-p.y-.015)/.045)
            v.co+=direction*d
        ob.data.update()

def remove_piece(ob):
    pieces.remove(ob);bpy.data.objects.remove(ob,do_unlink=True)

def pocket(name,s,z,w,h,depth,center_y):
    # Structured fabric bag with sewn edges and a shaped flap, not a solid cube.
    nx=12;nz=16;verts=[];uv=[]
    cx=s*.095
    for j in range(nz+1):
        v=j/nz
        for i in range(nx+1):
            u=i/nx;xx=cx+(u-.5)*w;zz=z+(v-.5)*h
            bulge=depth*sin(pi*u)**.7*sin(pi*v)**.7
            yy=center_y+ .20*(abs(xx)-.095)-bulge+.0015*sin(u*18+v*4)*sin(pi*u)*sin(pi*v)
            verts.append((xx,yy,zz));uv.append((u,v))
    faces=[]
    for j in range(nz):
        for i in range(nx):
            a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
    bone='Chest' if z>1.15 else 'Hips'
    ob=mesh_obj(name,verts,faces,0,bone,uv);bpy.context.view_layer.objects.active=ob
    so=ob.modifiers.new('Tecido da bolsa','SOLIDIFY');so.thickness=.0012;bpy.ops.object.modifier_apply(modifier=so.name)
    # Two rows of fine thread, constrained to the pocket surface.
    for off in [.003,.006]:
        pts=[]
        for u,v in [(off/w,1),(off/w,off/h),(1-off/w,off/h),(1-off/w,1)]:
            xx=cx+(u-.5)*w;zz=z+(v-.5)*h;yy=center_y+.20*(abs(xx)-.095)-depth*sin(pi*u)**.7*sin(pi*v)**.7-.0007
            pts.append((xx,yy,zz))
        stitched('Pesponto_'+name,pts,bone)
    top=z+h/2
    outline=[(cx-w*.53,center_y-.003,top+.006),(cx+w*.53,center_y-.003,top+.006),(cx+w*.50,center_y-.011,top-.028),(cx,center_y-.014,top-.037),(cx-w*.50,center_y-.011,top-.028)]
    flap=mesh_obj('Aba_'+name,outline,[(0,1,2,3,4)],0,bone);bpy.context.view_layer.objects.active=flap
    so=flap.modifiers.new('Aba dupla','SOLIDIFY');so.thickness=.002;bpy.ops.object.modifier_apply(modifier=so.name)
    bevel=flap.modifiers.new('Borda aba','BEVEL');bevel.width=.0018;bevel.segments=3;bpy.ops.object.modifier_apply(modifier=bevel.name)
    stitched('Costura_aba_'+name,[(x,y-.0007,z) for x,y,z in outline+[outline[0]]],bone)
    ellipsoid('Botao_'+name,(cx,center_y-.016,top-.022),(.005,.0017,.005),5,bone,16,8)

def anatomical_hands():
    ref=SRC/'ReferenceAssets';vs,uv,fs,fuv,gr=load_obj(ref/'mh_base.obj')
    for line in (ref/'male-young.target').read_text().splitlines():
        p=line.split()
        if p and not p[0].startswith('#'):vs[int(p[0])]+=Vector(tuple(map(float,p[1:4])))
    def center(name):
        ids=set(j for f,group in zip(fs,gr) if group==name for j in f)
        return sum((vs[j] for j in ids),Vector())/len(ids)
    wrist=center('joint-l-hand');tip=center('joint-l-finger-3-4');axis=(tip-wrist).normalized()
    width=center('joint-l-finger-5-1')-center('joint-l-finger-2-1');width=(width-axis*width.dot(axis)).normalized()
    normal=axis.cross(width).normalized();scale=.091
    # Only the hand and a short wrist collar, with the original skin UVs.
    selected=[i for i,f in enumerate(fs) if gr[i]=='body' and all((vs[j]-wrist).dot(axis)>-.14 and vs[j].x>4.2 for j in f)]
    ids=sorted(set(j for i in selected for j in fs[i]));mapping={j:k for k,j in enumerate(ids)}
    skin=next(m for m in bpy.data.materials if m.name.startswith('17 • Pele facial')).copy();skin.name='20 • Pele anatomica maos'
    hs=next(n for n in skin.node_tree.nodes if n.type=='HUE_SAT');bs=skin.node_tree.nodes.get('Principled BSDF');skin.node_tree.links.new(hs.outputs[0],bs.inputs['Base Color'])
    for sg,side in [(1,'L'),(-1,'R')]:
        coords=[]
        for j in ids:
            d=vs[j]-wrist;coords.append((sg*(.746+d.dot(axis)*scale),d.dot(width)*scale,1.465+d.dot(normal)*scale))
        me=bpy.data.meshes.new('Mao_anatomica_'+side);me.from_pydata(coords,[],[[mapping[j] for j in fs[i]] for i in selected]);me.update()
        ob=bpy.data.objects.new('Mao_anatomica_nova.'+side,me);char.objects.link(ob);pieces.append(ob);me.materials.append(skin)
        layer=me.uv_layers.new(name='UV_Pele')
        for poly,i in zip(me.polygons,selected):
            for li,ui in zip(poly.loop_indices,fuv[i]):layer.data[li].uv=uv[ui]
            poly.use_smooth=True
        weights(ob,'Hand.'+side);bpy.context.view_layer.objects.active=ob
        sub=ob.modifiers.new('Forma anatomica','SUBSURF');sub.levels=1;bpy.ops.object.modifier_apply(modifier=sub.name)

def campaign_finish_geometry():
    # Remove the old straight decorative seams before adding the field-jacket details.
    for ob in list(pieces):
        if ob.name.startswith(('Barra_gandola','Bainha_bolso','Costura_bolso','Mao_anatomica.','Unha_')):remove_piece(ob)
    for s,side in [(1,'L'),(-1,'R')]:
        pocket('Bolso_peito.'+side,s,1.335,.100,.120,.0055,-.116)
        pocket('Bolso_inferior.'+side,s,.968,.130,.095,.006,-.127)
        stitched('Barra_jaqueta.'+side,[(.190*cos(t),.138*sin(t),.916) for t in [i*2*pi/96 for i in range(97)]],'Hips')
    anatomical_hands()
    # A standing collar at the nape hides the neck boundary without adding bulk.
    pts=[(.063*cos(t),.002+.066*sin(t),1.556+.003*sin(t)) for t in [i*pi/40 for i in range(41)]]
    ribbon('Gola_traseira',pts,.019,0,width_axis=(0,0,1))
    stitched('Costura_gola_traseira',[(x,y+.0005,z+.006) for x,y,z in pts])
    # Seat the original PDF insignia onto the actual sleeve surface.
    jacket=next(o for o in pieces if o.name=='Gandola_M41_continua')
    from mathutils.bvhtree import BVHTree
    tree=BVHTree.FromObject(jacket,bpy.context.evaluated_depsgraph_get())
    def cast_jacket(origin,direction):
        loc,norm,face,distance=tree.ray_cast(origin,direction)
        return loc is not None,loc,norm,face
    # Keep the continuous webbing construction and its clearance over pockets.
    for ob in list(pieces):
        if ob.name.startswith('Debrum_Distintivo_'):remove_piece(ob)
    for ob in list(pieces):
        if not ob.name.startswith('Distintivo_'):continue
        name=ob.name;mat=ob.data.materials[0];sg=1 if 'FEB_esquerdo' in name else -1;side='L' if sg>0 else 'R'
        remove_piece(ob)
        outline=[(.20,1),(.80,1),(1,.82),(1,.18),(.80,0),(.20,0),(0,.18),(0,.82)]
        uvs=[(.5,.5)]+outline
        verts=[(sg*(.29+(1-v)*.088),(u-.5)*.065,1.55) for u,v in uvs]
        faces=[(0,i+1,(i+1)%8+1) for i in range(8)]
        me=bpy.data.meshes.new(name+'_conformado');me.from_pydata(verts,[],faces);me.update()
        layer=me.uv_layers.new(name='UV_original_PDF')
        for poly in me.polygons:
            for li in poly.loop_indices:layer.data[li].uv=uvs[me.loops[li].vertex_index]
        bm=bmesh.new();bm.from_mesh(me);bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=7,use_grid_fill=True);bm.to_mesh(me);bm.free()
        for v in me.vertices:
            hit,loc,norm,face=cast_jacket(Vector((v.co.x,v.co.y,1.8)),Vector((0,0,-1)))
            if hit:v.co.z=loc.z+.0022
        ob=bpy.data.objects.new(name,me);char.objects.link(ob);pieces.append(ob);me.materials.append(mat);weights(ob,'UpperArm.'+side)
        for poly in me.polygons:poly.use_smooth=True
        bpy.context.view_layer.objects.active=ob
        so=ob.modifiers.new('Espessura bordado','SOLIDIFY');so.thickness=.0007;so.offset=0;bpy.ops.object.modifier_apply(modifier=so.name)
        edge=[]
        for a,b in zip(outline,outline[1:]+outline[:1]):
            for k in range(12):
                u=a[0]+(b[0]-a[0])*k/12;v=a[1]+(b[1]-a[1])*k/12
                x=sg*(.29+(1-v)*.088);y=(u-.5)*.065
                hit,loc,norm,face=cast_jacket(Vector((x,y,1.8)),Vector((0,0,-1)))
                edge.append((x,y,loc.z+.0024 if hit else 1.55))
        cord('Debrum_'+name,edge,.0007,0,'UpperArm.'+side,True)
    # Lower the head assembly slightly to shorten the visible neck.
    for ob in pieces:
        if ob.name.startswith(('Rosto_anatomico','Olhos_castanhos','Pelo_sobrancelha','Capacete_M1','Aro_capacete','Forro_capacete','Jugular_M1','Fivela_jugular')):
            ob.location.z-=.014

def campaign_finish_rig(rig):
    rig['Revisao']='Referencia fotografica FEB: rosto individual, pescoco contido, jaqueta de campanha, maos anatomicas, emblemas originais.'
    rig['Referencias_fotograficas']='01a-monte-castelo-03.jpg; backmcastello.jpg; pelotao ten. Cavalcanti; exemplo 3D fornecido pelo usuario.'
    rig['Rosto']='Interpretacao artistica das referencias; nao retrato identificado nem preset de nacionalidade.'

def campaign_manifest(stats):
    hashes={name:hashlib.sha256((TEX/name).read_bytes()).hexdigest() for name in ['FEB_Patch.png','FifthArmy_Patch.png']}
    stats['emblemas_sha256']=hashes;stats['references']='User supplied FEB photos and humanoid reference; original PDF emblem images retained.'
    (SRC/'model_report_campanha.json').write_text(json.dumps(stats,indent=2))
