<p align="center">
  <img src="Assets/UI/Menu/Icone_MC1945.png" alt="Emblema do jogo Monte Castello 1945" width="150">
</p>

# Monte Castello: 1945

**Protótipo de jogo sério 3D desenvolvido como Trabalho de Conclusão de Curso em Sistemas de Informação na Universidade Federal de Santa Maria (UFSM).**

[Acesse a página do projeto e o guia de participação](https://yuriboeira11tx.github.io/MonteCastello1945/) para responder ao pré-teste, baixar o jogo nos Releases e enviar a avaliação da experiência com o relatório final.

Em *Monte Castello: 1945*, o jogador assume o papel de um pesquisador histórico. Sua missão é consultar documentos, examinar objetos, reunir evidências e relacionar acontecimentos para compreender a participação brasileira na Segunda Guerra Mundial e a trajetória da Força Expedicionária Brasileira (FEB).

O projeto combina exploração em primeira pessoa, investigação e atividades contextualizadas. A leitura das fontes faz parte da progressão: aquilo que o jogador observa e registra ajuda a responder às perguntas e a concluir os dossiês de cada etapa.

![Acampamento da FEB no protótipo](docs/screenshots/acampamento.png)

## Um protótipo de TCC

Este repositório apresenta **o recorte que foi possível desenvolver no tempo disponível para o TCC até o momento**. O jogo ainda é um protótipo e não representa toda a campanha da FEB.

A ideia é expandir a experiência para acompanhar toda essa trajetória, desde os antecedentes da participação brasileira na guerra até a campanha na Itália e os acontecimentos relacionados à tomada de Monte Castello. Essa ampliação é uma direção futura do projeto; as etapas disponíveis nesta versão estão descritas abaixo.

O trabalho acadêmico que fundamenta o desenvolvimento tem o título:

> **Monte Castello 1945: Desenvolvimento de um Jogo Sério 3D sobre a Trajetória da Força Expedicionária Brasileira**

| Informação | Responsável |
| --- | --- |
| Autor, direção e desenvolvimento | Yuri Juliano do Nascimento Boeira |
| Professor orientador | Sidnei Renato Silveira |
| Curso | Bacharelado em Sistemas de Informação |
| Instituição | Universidade Federal de Santa Maria — campus Frederico Westphalen |

## Objetivo educativo

O objetivo do TCC é estudar e desenvolver um jogo sério que apoie os processos de ensino e de aprendizagem de História, aproximando o jogador de documentos, personagens e situações ligados à FEB. A proposta também busca contribuir para a preservação e a difusão da memória da participação brasileira na Segunda Guerra Mundial.

O pesquisador precisa observar as fontes, compreender seu contexto e relacionar informações. Fotografias, jornais, objetos e textos são apresentados dentro do jogo, acompanhados de atividades que retomam o conteúdo encontrado durante a investigação.

O protótipo registra progresso, respostas, tentativas e tempo de pesquisa. Ao concluir o percurso, o jogador pode identificar-se e emitir um relatório para análise pelo professor. A avaliação proposta no TCC combina esses registros com pré-teste, pós-teste e um questionário de experiência, para investigar a aprendizagem e a usabilidade do jogo.

## O que já é possível jogar

### Arquivo histórico

O acervo funciona como ponto de partida da pesquisa. O jogador explora um escritório, encontra evidências em diferentes locais e consulta as informações necessárias para organizar os dossiês. Um quadro permite ordenar acontecimentos e estabelecer relações entre as fontes, conforme a etapa da investigação.

![Sala do arquivo histórico e quadro de investigação](docs/screenshots/acervo.png)

### Inspeção de evidências

Os objetos podem ser examinados em 3D, com rotação e aproximação. Ao lado, uma ficha com aparência de documento antigo apresenta o contexto histórico em páginas de leitura. O registro da evidência integra o progresso da pesquisa.

![Inspeção de uma evidência com objeto tridimensional e documento de leitura](docs/screenshots/inspecao.png)

### Caderneta do pesquisador

A caderneta é um objeto 3D aberto diante do jogador. Ela reúne orientações, anotações, atividades e opções da experiência, acompanhando o pesquisador no acervo e no acampamento. As evidências continuam ligadas aos objetos presentes nos cenários: para examiná-las novamente, é preciso voltar até elas.

![Caderneta tridimensional do pesquisador](docs/screenshots/caderneta.png)

### Reconstrução do Itapagé

A primeira investigação aborda a guerra no Atlântico e o caso do navio Itapagé. A reconstrução combina um filme com uma atividade de navegação e desvio de torpedos. O encerramento retoma o desfecho histórico e apresenta uma homenagem às vítimas. Depois, o pesquisador retorna ao acervo para relacionar as fontes e responder às perguntas do dossiê.

### Acampamento da FEB

A segunda etapa aborda a vida em campanha. Fotografias consultadas no acervo introduzem temas como abrigo, alimentação e correspondência. A visita ao acampamento permite procurar novas evidências e observar a ambientação: barracas, enfermaria, cozinha, equipamentos, soldados em atividade e circulação de viaturas.

A cena utiliza referências de acampamentos da FEB, incluindo San Rossore, na região de Pisa, e reúne conteúdos sobre o cotidiano dos expedicionários. O percurso termina com o retorno ao acervo, as perguntas do relatório e a apresentação das estatísticas da pesquisa.

*As imagens desta página são capturas do protótipo em execução no Unity. Cenários, interfaces e conteúdo podem mudar durante o desenvolvimento.*

## Como a investigação funciona

1. Conhecer a missão do pesquisador e explorar o acervo.
2. Examinar os objetos, ler os documentos e registrar as evidências.
3. Relacionar as fontes no quadro e responder às atividades da caderneta.
4. Participar da reconstrução ou visitar o cenário de campo correspondente.
5. Retornar ao acervo, concluir o dossiê e avançar na pesquisa.
6. Ao concluir as etapas do protótipo, emitir o relatório de desempenho e acompanhar os créditos.

## Controles básicos

| Ação | Controle |
| --- | --- |
| Caminhar nas fases com o pesquisador | W, A, S, D |
| Olhar ao redor | Mouse |
| Interagir com objetos | E |
| Abrir a caderneta | Tab |
| Girar um objeto durante a inspeção | Arrastar com o mouse |
| Aproximar ou afastar o objeto inspecionado | Roda do mouse |

As interfaces apresentam as instruções específicas de cada atividade.

## Tecnologias e organização

O projeto utiliza **Unity 6000.6.8f1**, **C#** e **Universal Render Pipeline (URP)**. As interfaces são construídas com **UI Toolkit**, incluindo a apresentação das páginas da caderneta no objeto 3D. A ambientação utiliza recursos de animação, Animation Rigging, navegação por NavMesh e áudio espacial. O Blender faz parte do processo de preparação e adaptação de modelos.

| Pasta | Conteúdo |
| --- | --- |
| `Assets/Scenes` | Menu, carregamento, acervo, reconstrução e acampamento |
| `Assets/Content/ArquivoHistorico` | Dossiês, evidências do acervo, perguntas e créditos |
| `Assets/Content/Acampamento` | Conteúdos e evidências da visita de campo |
| `Assets/Scripts/Archive` | Investigação, interações e caderneta |
| `Assets/Scripts/Persistence` | Saves e relatórios de desempenho |
| `Assets/UI` | Interfaces, estilos, fontes e elementos visuais |
| `docs/screenshots` | Capturas utilizadas neste README |

## Abrir o projeto no Unity

1. Instale o **Git LFS** antes de clonar o repositório; modelos, imagens, áudio e vídeo utilizam esse armazenamento.
2. Clone o projeto e baixe seus arquivos LFS:

   ```bash
   git lfs install
   git clone https://github.com/yuriboeira11tx/MonteCastello1945.git
   cd MonteCastello1945
   git lfs pull
   ```

3. No Unity Hub, adicione a pasta do projeto e abra com a versão indicada em [`ProjectSettings/ProjectVersion.txt`](ProjectSettings/ProjectVersion.txt).
4. Aguarde a importação dos assets e a resolução dos pacotes.
5. Abra `Assets/Scenes/Carregamento.unity` e pressione **Play** para iniciar pelo fluxo de carregamento e menu.

A plataforma de distribuição preparada no projeto é **Windows de 64 bits**. Este repositório contém o projeto de desenvolvimento; a experiência deve ser validada também no executável gerado para cada versão distribuída.

## Créditos

**Direção, desenvolvimento e realização:** Yuri Juliano do Nascimento Boeira.

**Orientação acadêmica:** Sidnei Renato Silveira.

| Dublagem | Participação |
| --- | --- |
| Brandon Lima | Motorista do acampamento |
| Fábio Mamede | Sargento do acampamento |
| Fernando Oliveira | Soldado do acampamento |
| Leonardo Vidal | Soldado do acampamento |

O desenvolvimento utiliza recursos próprios e de terceiros, incluindo modelos, texturas, fotografias, fontes e áudio. Os materiais de terceiros mantêm seus respectivos créditos e condições de uso; sua presença neste repositório não atribui a autoria desses recursos ao desenvolvedor do jogo.
