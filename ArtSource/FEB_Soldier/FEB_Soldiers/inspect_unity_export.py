import bpy,json
from pathlib import Path
O=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers/UnityDelivery');O.mkdir(exist_ok=True)
rows=[]
for i in range(1,7):
 a=bpy.data.objects[f'FEB_Soldado_{i:02}_Rig']
 rows.append({'id':i,'armature':a.name,'matrix':[list(r) for r in a.matrix_world],'children':[{'name':o.name,'type':o.type,'matrix':[list(r) for r in o.matrix_world],'mods':[(m.name,m.type) for m in o.modifiers],'materials':[m.name if m else None for m in o.data.materials]} for o in a.children if o.type=='MESH'],'bones':[{'name':b.name,'parent':b.parent.name if b.parent else None,'head':list(b.head_local),'tail':list(b.tail_local),'deform':b.use_deform} for b in a.data.bones]})
mats={}
for r in rows:
 for ob in r['children']:
  for name in ob['materials']:
   if name in mats or name is None:continue
   m=bpy.data.materials[name];nodes=[]
   for n in m.node_tree.nodes:
    if n.type=='TEX_IMAGE':nodes.append({'type':n.type,'image':n.image.name if n.image else None,'path':n.image.filepath if n.image else None,'packed':bool(n.image.packed_file) if n.image else None})
    elif n.type=='BSDF_PRINCIPLED':nodes.append({'type':n.type,'base_color':list(n.inputs['Base Color'].default_value),'roughness':n.inputs['Roughness'].default_value,'metallic':n.inputs['Metallic'].default_value,'alpha':n.inputs['Alpha'].default_value,'alpha_linked':n.inputs['Alpha'].is_linked})
   mats[name]=nodes
result={'rigs':rows,'materials':mats,'file':bpy.data.filepath,'dirty':bpy.data.is_dirty}
(O/'source_inspection.json').write_text(json.dumps(result,indent=2))
if not (O/'FEB_Soldiers_Antes_Unity.blend').exists():bpy.ops.wm.save_as_mainfile(filepath=str(O/'FEB_Soldiers_Antes_Unity.blend'),copy=True)
result={'output':str(O/'source_inspection.json'),'backup':str(O/'FEB_Soldiers_Antes_Unity.blend'),'rigs':len(rows),'materials':len(mats)}
