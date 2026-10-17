# FEB Soldiers — prefabs para animação

Seis personagens em pose T, preparados para Unity 6.3 / URP. Cada prefab contém a malha com pesos, esqueleto, materiais, texturas e um Animator com Avatar Humanoid próprio.

## Conteúdo

| Pasta | Conteúdo |
|---|---|
| `Prefabs` | `FEB_Soldado_01.prefab` até `FEB_Soldado_06.prefab` |
| `Models` | Seis FBXs com malha e rig; importação Humanoid configurada |
| `Materials` | 17 materiais URP Lit vinculados às texturas |
| `Textures` | 17 imagens originais de uniforme, emblemas, equipamento e peles |

Os prefabs não contêm scripts de movimento, CharacterController, interface, clipes de animação ou Animator Controller associado. A pose T é a pose de referência do modelo, não uma animação.

## Começar a animar

1. Arraste um prefab para a sua cena.
2. Expanda sua raiz e selecione o objeto filho do modelo, onde está o componente **Animator**.
3. O campo **Avatar** já está preenchido e validado. Associe seu próprio **Animator Controller** ao campo **Controller**, atualmente vazio.
4. Adicione seus clipes Humanoid ao controlador e desenvolva a lógica do personagem conforme o seu jogo.

Cada soldado possui seu próprio Avatar; os seis podem compartilhar um Animator Controller e clipes Humanoid compatíveis. **Apply Root Motion** está desligado por padrão: você poderá ativá-lo quando suas animações e sua lógica de deslocamento exigirem.

Você também pode criar e editar animações no Blender a partir dos rigs, exportando os clipes para o Unity. Os modelos têm 53 ossos exportados e até quatro influências por vértice. Os FBXs desta pasta importam somente modelo e rig, sem clipes.

## Texturas e fonte Blender

`FEB_01_Pele_Rosto.png` e `FEB_01_Pele_Maos.png` pertencem ao soldado 01; a numeração segue até 06. Uniforme e emblemas possuem materiais próprios. Os materiais são opacos: o alfa dos atlas não deve ser usado para recortar o uniforme.

Fonte Blender preparada: `ArtSource/FEB_Soldier/FEB_Soldiers/UnityDelivery/FEB_Soldiers_Unity.blend`. A cena **FEB_Unity_Export** contém os rigs de exportação. O arquivo original `FEB_Soldiers_Rig.blend` permanece na pasta superior. As ações de demonstração existentes no arquivo de trabalho do Blender não estão incluídas nem associadas aos prefabs Unity.

A demonstração anterior foi arquivada fora de `Assets`, em `ArtSource/FEB_Soldier/FEB_Soldiers/UnityDelivery/Archive_Demo_20261016`. Ela não participa da importação nem da execução do jogo.

Validação da configuração atual: `ArtSource/FEB_Soldier/FEB_Soldiers/UnityDelivery/prefabs_only_validation.json`.
