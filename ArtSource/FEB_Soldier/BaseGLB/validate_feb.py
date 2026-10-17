import bpy,json,math,hashlib
from mathutils import Vector,Matrix
from pathlib import Path
ROOT=Path('E:/Unity/MonteCastello1945');OUT=ROOT/'ArtSource/FEB_Soldier/BaseGLB';ASSET=ROOT/'Assets/Art/Characters/FEB_Soldier/BaseGLB'
scene=bpy.data.scenes['FEB_BaseGLB'];bpy.context.window.scene=scene
rig=bpy.data.objects['FEB_Rig_BaseGLB'];body=bpy.data.objects['FEB_GameBody'];char=bpy.data.collections['FEB_Personagem_BaseGLB']
report={'checks':{},'roundtrip':{}};checks=report['checks']
checks['source_53_bones']=len(rig.data.bones)==53
checks['finger_bones_preserved']=all(f'{finger}{i}.{side}' in rig.data.bones for side in ['L','R'] for finger in ['Thumb','Index','Middle','Ring','Little'] for i in [1,2,3])
checks['rest_T_pose']=all(sum(abs(b.matrix_basis[i][j]-(1 if i==j else 0)) for i in range(4) for j in range(4))<.001 for b in rig.pose.bones)
checks['export_weights_four_normalized']=all(0<len(v.groups)<=4 and abs(sum(g.weight for g in v.groups)-1)<.0001 for v in body.data.vertices)
checks['export_UV']=len(body.data.uv_layers)==1
expected={'FEB_Patch.png':'470f563973b03489b9d5180185d887e78eca2c79ee3da1c5f8749a91c6d1086b','FifthArmy_Patch.png':'f0270096be66371b085c8afce31171406ff06b059e0910a911c225faa0ad2861'}
for name,digest in expected.items():checks['unchanged_'+name]=hashlib.sha256((ROOT/'Assets/Art/Characters/FEB_Soldier/Textures'/name).read_bytes()).hexdigest()==digest
checks['original_GLB_untouched']=hashlib.sha256(Path('C:/Users/yurib/Downloads/call_of_duty_1_soldier_rig.glb').read_bytes()).hexdigest()==json.loads((OUT/'export_report.json').read_text())['source_glb_sha256']
for channel in ['BaseColor','Normal','Roughness','Metallic','MetallicSmoothness_URP']:
    p=ASSET/'Textures'/('FEB_BaseGLB_'+channel+'.png');im=bpy.data.images.load(str(p),check_existing=False)
    checks['map_'+channel]=tuple(im.size)==(2048,2048) and im.has_data
    bpy.data.images.remove(im)

hand=bpy.data.objects['FEB_Maos']
def evaluated_points(ob):
    deps=bpy.context.evaluated_depsgraph_get();return [ob.matrix_world@v.co for v in ob.evaluated_get(deps).data.vertices]
for bn,limit in [('Forearm.L',.025),('Index1.L',.001)]:
    pb=rig.pose.bones[bn];old=pb.matrix_basis.copy();before=evaluated_points(hand)
    pb.matrix_basis=old@Matrix.Rotation(.45,4,'X');bpy.context.view_layer.update();after=evaluated_points(hand)
    movement=max((a-b).length for a,b in zip(before,after));pb.matrix_basis=old;bpy.context.view_layer.update()
    checks['deforms_'+bn]=movement>limit;report['movement_'+bn]=movement

def inspect_scene(sc):
    meshes=[o for o in sc.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)]
    rigs=[o for o in sc.objects if o.type=='ARMATURE']
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    images=[]
    for o in meshes:
        for m in o.data.materials:
            if m and m.use_nodes:
                for n in m.node_tree.nodes:
                    if n.type=='TEX_IMAGE' and n.image:images.append(n.image)
    for im in images:
        if im.size[0] and len(im.pixels):_pixel=im.pixels[0]
    return {'meshes':len(meshes),'bones':len(rigs[0].data.bones) if rigs else 0,'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),'height':max(p.z for p in points)-min(p.z for p in points),'uvs':all(bool(o.data.uv_layers) for o in meshes),'weights':all(v.groups and abs(sum(g.weight for g in v.groups)-1)<.002 for o in meshes for v in o.data.vertices),'images_loaded':all(im.has_data for im in images),'finite':all(all(math.isfinite(c) for c in p) for p in points)}

for fmt,path in [('glb',OUT/'FEB_Soldado_BaseGLB.glb'),('fbx',ASSET/'Models/FEB_Soldado_BaseGLB.fbx')]:
    temp=bpy.data.scenes.new('Verificacao_'+fmt);bpy.context.window.scene=temp
    if fmt=='glb':bpy.ops.import_scene.gltf(filepath=str(path))
    else:bpy.ops.import_scene.fbx(filepath=str(path))
    info=inspect_scene(temp);report['roundtrip'][fmt]=info
    checks[fmt+'_roundtrip']=info['meshes']==3 and info['bones']==53 and info['triangles']<10000 and 1.75<info['height']<1.9 and info['uvs'] and info['weights'] and info['images_loaded'] and info['finite']
    if fmt=='glb':
        for o in bpy.data.collections['Studio_FEB'].objects:temp.collection.objects.link(o)
        temp.world=scene.world;temp.camera=scene.camera
        temp.render.engine='CYCLES';temp.cycles.samples=32;temp.cycles.use_denoising=True
        temp.render.resolution_x=1200;temp.render.resolution_y=1200;temp.render.resolution_percentage=100;temp.view_settings.view_transform='AgX'
        temp.render.filepath=str(OUT/'FEB_exportado_conferido.png');bpy.ops.render.render(write_still=True)
    bpy.context.window.scene=scene
    for o in list(temp.objects):
        if o.name not in scene.objects:bpy.data.objects.remove(o,do_unlink=True)
    bpy.data.scenes.remove(temp)

for win in list(bpy.context.window_manager.windows):
    if win.screen.areas and all(a.type=='PREFERENCES' for a in win.screen.areas):
        with bpy.context.temp_override(window=win):bpy.ops.wm.window_close()
for win in bpy.context.window_manager.windows:
    win.scene=scene
    for area in win.screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active;space.shading.type='MATERIAL';space.overlay.show_overlays=False;space.region_3d.view_location=(0,0,.95);space.region_3d.view_distance=3;space.region_3d.view_rotation=scene.camera.rotation_euler.to_quaternion();space.region_3d.view_perspective='ORTHO'
body.hide_set(True);body.hide_render=True
bpy.ops.object.select_all(action='DESELECT')
report['passed']=all(checks.values());(OUT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FEB_Soldado_BaseGLB.blend'))
result=report
assert report['passed'],report
