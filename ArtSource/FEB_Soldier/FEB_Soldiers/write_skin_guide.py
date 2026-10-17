from pathlib import Path
import json
out=Path('E:/Unity/MonteCastello1945/ArtSource/FEB_Soldier/FEB_Soldiers')
assets=Path('E:/Unity/MonteCastello1945/Assets/Art/Characters/FEB_Soldiers')
data=json.loads((out/'skin_variants.json').read_text(encoding='utf-8'))
lines=['GUIA DAS TEXTURAS DE PELE - SEIS SOLDADOS FEB','',
'O QUE FOI ALTERADO',
'Somente a cor da pele: rosto, orelhas, pescoco e maos. Cada soldado tem duas imagens proprias. Foram preservados o desenho dos rostos, as malhas, os UVs, as poses, o uniforme, os equipamentos e os emblemas.',
'As 11 imagens anteriores continuam intactas. Os novos tons foram aplicados em copias independentes; as variantes 03 e 04 agora usam imagens distintas, embora compartilhassem a mesma textura de rosto na base.',
'', '1. ONDE ESTAO OS ARQUIVOS',str(assets/'Textures/Pele'),
'Na pasta do projeto Unity: Assets > Art > Characters > FEB_Soldiers > Textures > Pele.',
'No ZIP, mantenha Textures e Models na mesma pasta FEB_Soldiers.',
'', '2. IDENTIFICACAO DE CADA SOLDADO']
for v in data['variants']:
 i=v['id'];lines += ['',f"SOLDADO {i:02d} - {v['tone'].upper()}",f"Colecao no Blender: FEB_{i:02d}",f"Modelo: Models/FEB_Soldado_{i:02d}.fbx",f"Rosto, orelhas e pescoco: Textures/Pele/FEB_{i:02d}_Pele_Rosto.png",f"Material do rosto: FEB_{i:02d}_Pele_Rosto",f"Maos: Textures/Pele/FEB_{i:02d}_Pele_Maos.png",f"Material das maos: FEB_{i:02d}_Pele_Maos",'Objetos da cabeca: '+', '.join(data['head_objects'][str(i)]),'Objeto que contem as maos: '+', '.join(v['hand_faces'])]
lines += ['', '3. COMO VER NO BLENDER',
'Abra FEB_Soldiers.blend e escolha a cena FEB_Soldiers.',
'No Outliner, expanda a colecao FEB_01, FEB_02, etc. Selecione o objeto da cabeca listado acima.',
'Na aba Shading, escolha o material FEB_XX_Pele_Rosto. O no Image Texture mostra a imagem de rosto correspondente ligada ao Base Color.',
'Para as maos, selecione Corpo_uniforme ou M41_parte_superior, conforme a lista acima, e escolha o material FEB_XX_Pele_Maos. Esse material esta atribuido somente as faces das maos.',
'Na aba UV Editing, abra a lista de imagens no Image Editor e escolha FEB_XX_Pele_Rosto ou FEB_XX_Pele_Maos para ver o PNG diretamente. A imagem de maos tem fundo transparente: os espacos vazios sao normais, pois as coordenadas UV foram preservadas.',
'Use Material Preview para ver as cores no modelo. Na cena Guia_visual_tons_de_pele ha uma comparacao numerada dos seis soldados.',
'', '4. COMO LOCALIZAR NO UNITY',
'No painel Project, abra Assets/Art/Characters/FEB_Soldiers/Textures/Pele. Clique no PNG para visualizar a textura no Inspector.',
'Os FBXs atualizados apontam para as respectivas imagens de pele por caminho relativo. Se voce usar materiais URP personalizados, associe o PNG Rosto ao Base Map do material FEB_XX_Pele_Rosto e o PNG Maos ao Base Map do material FEB_XX_Pele_Maos. Os materiais do uniforme continuam com os mapas anteriores.',
'', '5. ARQUIVOS ATUALIZADOS E CONFERENCIA',
str(out/'FEB_Soldiers.blend'),str(assets/'Models'),str(out/'FEB_Soldiers_FBX_Texturas.zip'),
'Previa numerada: FEB_Tons_de_Pele_identificados.png (no ZIP: Previa_Pele/).',
'Copia anterior a esta alteracao: '+str(out/'FEB_Soldiers_antes_variacao_pele.blend'),
'Conferencia: 50 pecas com geometria, UVs e transformacoes identicos; 11 texturas anteriores preservadas; sete FBXs atualizados; seis soldados reimportados com as duas imagens de pele corretas para cada um; 59.138 triangulos mantidos.',
'Relatorio: '+str(out/'skin_validation.json'), '']
guide='\n'.join(lines)
(assets/'GUIA_TEXTURAS_PELE.txt').write_text(guide,encoding='utf-8')
(out/'GUIA_TEXTURAS_PELE.txt').write_text(guide,encoding='utf-8')
print('Guia salvo com os caminhos das 12 texturas, materiais e objetos de cada soldado.')
