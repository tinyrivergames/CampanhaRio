# Guia de estilo

As três imagens em `Docs/Reference/` (`estetica_01_floresta`, `estetica_02_encosta`, `estetica_03_lago`) são
**referência de atmosfera**, não de conteúdo. Este guia diz o que aproveitar delas e o que **não** copiar.

## O que pegar das referências

### Formas
- **Macias e "infladas".** Tudo parece ter sido moldado em massinha e depois alisado: bordas arredondadas, volumes
  cheios, nada pontiagudo. As rochas são grandes planos lisos com cantos boleados, não pedras realistas cheias de quebras.
- **Leitura de longe primeiro.** A silhueta conta mais que o detalhe. Uma árvore precisa ser reconhecível como uma mancha
  escura recortada contra o céu (ver a silhueta na folha de prévia).
- **Pouco detalhe de textura.** A variação vem de **gradientes grandes** (base mais escura, topo mais claro) e de cor de
  vértice, não de texturas detalhadas.

### Paleta (os nomes estão em `Art/LookDev/lookdev_rig.json`)
- **Verdes médios e profundos** nos pinheiros (`pine_dark`, `pine_mid`, `pine_light`), sem verde-limão nem verde neon.
- **Laranja/areia** nas falésias e rochas (`rock_orange`, `rock_sand`, sombra `rock_shadow`): é a cor-assinatura da região.
- **Turquesa limpa** na água (`water_shallow` → `water_deep`), com o fundo de areia aparecendo na beira.
- **Grama** amarelada nas encostas secas (`grass`, `grass_dry`), **terra batida** (`dirt`), céu azul com nuvens macias.
- Contraste vindo da **luz**, não de cores saturadas: a sombra é azul-arroxeada e quente, nunca preta.

### Luz
- **Sol quente** de fim de tarde, **sombras suaves e coloridas** (a cor de sombra do rig, `shadowTint`).
- **Luz que envolve as formas** (o *wrap* do SoftToon): o lado escuro ainda tem forma, nada vira um borrão preto.
- Um **brilho leve na borda** (rim) para separar as formas do fundo.
- **Névoa de distância** em azul-acinzentado: as montanhas e florestas do fundo ficam mais claras e mais azuis
  (perspectiva atmosférica). Isso também esconde o fim de cada trecho.
- **O céu marca o tempo:** o rio acontece do fim da tarde ao pôr do sol (o `DayCycle`). Toda cor precisa funcionar
  também com a luz dourada e com a luz do crepúsculo.

### Folhagem
- **Pinheiros robustos**, galhos em camadas, copa cheia e arredondada. As camadas leem como "saias" macias, não como
  agulhas individuais.
- Tufos de **grama e flores** em grupos, não espalhados um a um.
- Tecnicamente: cartões com alpha clip em troncos low-poly, normais do proxy suave, impostores no meio do caminho e
  silhuetas no fundo (`ART_PIPELINE.md`, seção 5).

### Rochas e falésias
- Grandes, lisas, laranja/areia, com **um gradiente suave** de baixo para cima e um pouco de musgo ou poeira por cima
  (o *top tint* do preset Rock).
- Falésias modulares que se repetem sem a repetição ficar óbvia (variar escala e rotação).

### Água
- **Turquesa, clara e transparente**, com ondinhas suaves e o fundo de areia visível na beira.
- A espuma é branca e macia, em faixas, nunca "ruidosa".

## O que NÃO copiar
- **Nada da interface** das referências: barras, ícones, contadores, o indicador de van. A interface do jogo é outra
  conversa (Fase 6).
- **Não copiar a van** (o motorhome das imagens) nem os **personagens** delas. A nossa van e os nossos animais são
  projetos próprios.
- **Não copiar assets específicos** (as lápides "RIP", aquela pedra empilhada), nem composições de cena.
- **Não buscar realismo:** nada de PBR com reflexos metálicos, texturas fotográficas, normal maps de detalhe ou
  contornos pretos (outline). Também não volta a aquarela do projeto antigo.
- **Não ficar com cara de Roblox:** nada de cores de plástico, plataformas flutuando, peças repetidas sem variação ou
  menu-lobby no lugar do mundo (veja o `PLANO_CAMPANHA.md`).

## Na prática, para cada asset
1. A silhueta funciona? (tile SILHUETA da prévia)
2. A escala está certa ao lado do bicho de 0,9 m? (tile ESCALA)
3. Funciona à tarde, na hora dourada e no pôr do sol? (cena LookDev, slider da hora)
4. Cabe no orçamento de triângulos? (cabeçalho da prévia, tabela do `ART_PIPELINE.md`)
