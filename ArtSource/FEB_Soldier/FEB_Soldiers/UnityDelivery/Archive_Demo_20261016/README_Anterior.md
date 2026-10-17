# FEB Soldiers — uso no Unity

Seis personagens preparados para Unity 6.3 / URP, cada um com seu Avatar Humanoid, materiais e prefab. Os três movimentos compartilhados são parado (`FEB_Idle`), caminhada (`FEB_Walk`) e corrida (`FEB_Run`).

## Testar agora

1. Abra `Assets/Scenes/FEB_Character_Test.unity` e pressione **Play**.
2. Use **Parado**, **Caminhar** e **Correr** para ver os seis personagens animando no lugar.
3. Escolha **01–06** e clique em **Controlar selecionado**.
4. Use **WASD** ou setas para mover; **Shift** para correr; **V** para alternar a câmera; **R** para reposicionar.
5. Clique na aba **Game** para ela receber o teclado. Mantenha o zoom da aba Game em 1x para ver o painel inteiro.

## Usar em outra cena

Arraste um dos arquivos da pasta `Prefabs` para uma cena com chão e colisão. No componente **FEB Character Motor**, marque **Player Controlled** e associe a câmera de movimento, ou mantenha uma câmera com a tag `MainCamera`.

O prefab já contém `CharacterController`, gravidade, rotação e `Animator`. O deslocamento é feito pelo controle: **Apply Root Motion permanece desligado**. Caminhada: 1,2 m/s; corrida: 3,4 m/s. O parâmetro **Speed** do Animator mistura os três movimentos. Para apenas exibir animações, deixe **Player Controlled** desmarcado e ajuste **Preview Speed** entre 0 e 3,4.

## Onde está cada parte

| Pasta | Conteúdo |
|---|---|
| `Prefabs` | Seis personagens prontos para colocar em cenas |
| `Models` | Seis FBXs com malha e esqueleto; Avatar próprio por personagem |
| `Animations` | Três FBXs animados e `FEB_Locomotion.controller` |
| `Materials` | Materiais URP Lit; também contém o material do chão da demonstração |
| `Textures` | As 17 imagens originais, preservadas byte a byte |

As texturas de pele são `FEB_01_Pele_Rosto.png` e `FEB_01_Pele_Maos.png`, seguindo a mesma nomenclatura até 06. Uniforme, cores e emblemas continuam separados das peles. Os materiais usam superfície opaca; o canal alfa dos atlas não deve ser usado para recortar o uniforme.

## Blender e exportação

Fonte preparada e animações: `ArtSource/FEB_Soldier/FEB_Soldiers/UnityDelivery/FEB_Soldiers_Unity.blend`.

A cena **FEB_Unity_Export** contém os seis rigs para exportação. Os modelos têm 53 ossos exportados e no máximo quatro influências por vértice. Cada soldado usa uma malha com seus materiais e UVs preservados. O arquivo `FEB_Soldiers_Rig.blend` original continua na pasta superior; a cópia `FEB_Soldiers_Antes_Unity.blend` preserva o estado anterior à integração.

As animações são ciclos básicos de locomoção feitos para esta integração. Servem para usar e testar o personagem; combate, armas, salto, agachamento e adaptação dos pés a terreno irregular precisam de movimentos e lógica próprios.

Ao reexportar animações separadas, mantenha **Preserve Hierarchy** no importador Unity. Elas usam **Copy From Other Avatar**, apontando para o Avatar do soldado 01. Isso preserva o mesmo pai da armature e evita deslocamento incorreto durante a animação. Cada modelo continua com **Create From This Model** e seu próprio Avatar. Outros clipes Humanoid válidos podem ser associados ao Animator e reaproveitados pelos seis soldados.

## Verificação e manutenção

O menu **Tools > FEB** permite validar a importação e executar o teste de animações e movimento. A opção **Preparar personagens e cena** refaz os arquivos gerados e a cena de demonstração; use-a ao atualizar a entrega, pois ela recria esses resultados.

Relatórios em `ArtSource/FEB_Soldier/FEB_Soldiers/UnityDelivery`: `unity_validation.json` para assets/texturas e `unity_playtest.json` para animação, geometria, apoio no chão e deslocamento em Play Mode. Imagens de conferência ficam em `Previews`.

A cena de demonstração foi adicionada depois de `Menu` nas configurações de cenas. A implementação e o conteúdo do menu inicial não foram alterados por esta integração.

Referência técnica: [ModelImporter — documentação Unity 6.3](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/modelimporter).
