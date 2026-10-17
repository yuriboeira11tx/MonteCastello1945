import bpy,json,math
from pathlib import Path
from mathutils import Vector
OUT=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers');s=bpy.data.scenes['FEB_Soldiers_Rig'];bpy.context.window.scene=s
rigs=[bpy.data.objects[f'FEB_Soldado_{i:02}_Rig'] for i in range(1,7)]
current={a.animation_data.action for a in rigs}
for action in list(bpy.data.actions):
 if action.name.startswith('FEB_') and '_Teste_Articulacoes' in action.name and action not in current:bpy.data.actions.remove(action)
for a in rigs:a.animation_data.action.name=f'FEB_{a["personagem"]:02}_Teste_Articulacoes'
notes='''FEB_Soldiers — rigs no Blender

Arquivo principal: FEB_Soldiers_Rig.blend
Cena ativa: FEB_Soldiers_Rig
Personagens: FEB_Soldado_01_Rig até FEB_Soldado_06_Rig.

Cada personagem possui 61 ossos: 52 de deformação e 9 controles.
O esqueleto inclui tronco, pescoço, cabeça, clavículas, braços, mãos,
30 ossos de dedos, coxas, pernas, pés e dedos dos pés.
As botas permanecem rígidas; os ossos toe estão reservados para futuras
adaptações de calçado. Os vértices têm no máximo quatro influências.

COMO MOVER
Selecione a armature do soldado no Outliner e use Pose Mode.
Por padrão, use FK: selecione um osso do corpo e rotacione com R.
CTRL_root desloca ou gira o personagem inteiro.
As coleções de ossos Corpo FK, Dedos, Controles IK e Raiz organizam o rig.
Para mostrar os dedos, habilite a coleção Dedos nos dados da armature.

IK OPCIONAL
Nas propriedades personalizadas do objeto armature, ajuste para 1:
IK_Braco_L / IK_Braco_R ou IK_Perna_L / IK_Perna_R.
Mova CTRL_mao.L/R ou CTRL_pe.L/R com G.
POLE_cotovelo.L/R e POLE_joelho.L/R orientam a dobra da articulação.
0 usa FK; 1 usa IK. Volte ao quadro 1 antes de trocar de modo.
A troca entre FK e IK não possui ajuste automático de correspondência
entre poses; configure os alvos antes de alternar em uma animação.

TESTES DE ARTICULAÇÃO
Há uma ação FEB_XX_Teste_Articulacoes por soldado.
Quadros 1 e 60: repouso; 15: braços e dedos; 30: passo de teste e giro;
45: flexão das pernas. Use a linha do tempo para conferir.
São poses de verificação do rig, não ciclos finais de gameplay.
Crie uma nova ação no Action Editor para suas animações de produção.

PRESERVAÇÃO
As 50 peças conservaram posições de vértices, faces, UVs e materiais.
Texturas, cores de pele, uniformes e emblemas foram preservados.
Os emblemas acompanham os braços; capacetes e equipamentos rígidos
possuem pesos apropriados. O arquivo está salvo na pose de repouso.
FEB_Soldiers.blend continua disponível como fonte sem rig.

UNITY
Assets/Art/Characters está vazia. Nenhum rig foi importado no Unity.
Os assets anteriores foram preservados em:
E:/Unity/MonteCastello1945/ArtSource/Characters_Archive_20261016
Os FBXs desse arquivo são anteriores ao rig. Não são a versão rigada.
Somente uma futura exportação autorizada deverá retornar a Assets.

VERIFICAÇÃO
Foram conferidos pesos normalizados, ausência de vértices sem peso,
repouso sem deslocamento da malha, preservação de UVs e materiais,
poses de braços/pernas e 24 alvos IK nos seis personagens.
O rig ainda não foi importado nem validado como Avatar Humanoid no Unity.
Detalhes: rig_final_validation.json.
'''
(OUT/'LEIA-ME_Rig_FEB.txt').write_text(notes,encoding='utf-8')
t=bpy.data.texts.get('LEIA-ME_Rig_FEB') or bpy.data.texts.new('LEIA-ME_Rig_FEB');t.clear();t.write(notes)
used_images=set()
for a in rigs:
 for ob in a.children:
  if ob.type!='MESH':continue
  for m in ob.data.materials:
   if m and m.use_nodes:
    for n in m.node_tree.nodes:
     if n.type=='TEX_IMAGE' and n.image:used_images.add(n.image)
for im in used_images:
 if not im.packed_file:im.pack()
assert all(i.packed_file for i in used_images)
s.render.engine='CYCLES';s.cycles.samples=32;s.cycles.use_denoising=True;s.render.image_settings.file_format='PNG';s.render.resolution_percentage=100
def camera(pos,target,scale,width,height):
 s.camera.location=pos;s.camera.rotation_euler=(Vector(target)-s.camera.location).to_track_quat('-Z','Y').to_euler();s.camera.data.type='ORTHO';s.camera.data.ortho_scale=scale;s.render.resolution_x=width;s.render.resolution_y=height
s.frame_set(15);camera((0,-12,3.1),(0,0,.95),8.6,2400,850);s.render.filepath=str(OUT/'FEB_Soldiers_Rig_Teste_15.png');bpy.ops.render.render(write_still=True)
hidden=[]
try:
 for arm in rigs[1:]:
  for ob in arm.children:
   if ob.type=='MESH':hidden.append((ob,ob.hide_render));ob.hide_render=True
 camera((-1.5,-3.2,2.1),(-3.5,-.05,1.32),1.45,1200,1200);s.render.filepath=str(OUT/'FEB_Soldado_01_Rig_Teste_Detalhe.png');bpy.ops.render.render(write_still=True)
 s.frame_set(45);camera((-1.2,-.7,1.1),(-3.5,-.03,.88),2.05,1000,1100);s.render.filepath=str(OUT/'FEB_Soldado_01_Rig_Teste_Lateral.png');bpy.ops.render.render(write_still=True)
finally:
 for ob,value in hidden:ob.hide_render=value
s.frame_set(1);camera((0,-12,2.6),(0,0,.90),8.6,2400,850);s.render.filepath=str(OUT/'FEB_Soldiers_Rig_Repouso.png');bpy.ops.render.render(write_still=True)
bpy.ops.object.select_all(action='DESELECT');arm=rigs[0];arm.select_set(True);bpy.context.view_layer.objects.active=arm
for a in rigs:
 a.data.collections['Dedos'].is_visible=False
 for pb in a.pose.bones:
  if pb.name.startswith('CTRL_'):pb.lock_scale=(True,True,True)
  if pb.name.startswith('POLE_'):pb.lock_rotation=(True,True,True);pb.lock_scale=(True,True,True)
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   sp=area.spaces.active;sp.region_3d.view_distance=7.7;sp.region_3d.view_location=(0,0,.9);sp.region_3d.view_rotation=s.camera.rotation_euler.to_quaternion();sp.region_3d.view_perspective='ORTHO';sp.shading.type='MATERIAL'
   sp.overlay.show_relationship_lines=False
for ob in s.objects:
 if ob.type in ['CAMERA','LIGHT']:ob.hide_set(True)
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.pose.select_all(action='DESELECT')
arm.data.bones.active=arm.data.bones['hips']
if hasattr(arm.pose.bones['hips'],'select'):arm.pose.bones['hips'].select=True
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FEB_Soldiers_Rig.blend'))
result={'file':bpy.data.filepath,'scene':s.name,'rigs':len(rigs),'packed_used_images':len(used_images),'frame':s.frame_current,'mode':bpy.context.mode,'guide':str(OUT/'LEIA-ME_Rig_FEB.txt')}
