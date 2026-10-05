# Rock / RochaA

## Briefing (confirmado em 2026-10-05)
- **O que é:** o matacão-mestre da família (âncora de estilo da Fase 1): pedra de beira de rio e obstáculo na água.
- **Tamanho:** ~2,4 x 1,8 x 1,4 m, assentada no chão.
- **Forma:** grandes planos lisos com arestas arredondadas, volume "inflado" (referências `estetica_02`, `estetica_03`).
- **Triângulos:** LOD0 <= 800, LOD1 <= 250, LOD2 <= 80, colisor <= 40.
- **Cores:** rock_orange, topo rock_sand, base rock_shadow; um tom por face (como as lâminas do pinheiro); musgo leve em cima.
- **Pedido do desenvolvedor:** "se baseia em algo pra mesclar bem com os graficos do pinheiro" → facetada como o pinheiro, normais infladas, variação de tom por face.
- **Gerador:** `ArtSource/Rock/rock_gen.py`.

## Versões

### v001
- Gerado com: `--seed 7 --size 2.7 --depth 0.8 --height 0.82 --points 18 --merge 14.0 --jitter 0.18 --bevel 0.05 --inflate 0.3 --tone 0.8 --moss 0.25`
- Triângulos: LOD0 310, LOD1 136, LOD2 16, COL 20
- Prévia: `RochaA_v001_preview.png`
- **Feedback do desenvolvedor (literal):** "beleza, acho que a pedra ficou legal, porem nao vejo nenhum musgo (nao sei se isso deve entrar na secao de variacoes de graficos na fase 3), as cores tambem parecem as mesmas e sem variacao tente adicionar isso, e teste uma versao menos arredondada tambem."
- **O que mudou:** primeira versão.

### v002
- Gerado com: `--seed 7 --size 2.7 --depth 0.8 --height 0.82 --points 18 --merge 14.0 --jitter 0.18 --bevel 0.05 --inflate 0.3 --tone 0.8 --moss 0.8 --variety 0.6`
- Triângulos: LOD0 310, LOD1 136, LOD2 16, COL 20
- Prévia: `RochaA_v002_preview.png`
- **Feedback do desenvolvedor (literal):** (junto com a v003) "essas sao somente as pedras beira rio por enquanto ne? tem que ter musgo mesmo verde em cima? o musgo ta com cor muito clara, tem que escurecer um pouco. Alem disso qual voce acha que fica melhor? a pedra mais quadrada ou menos igual na primeira versao?" → "faz um meio termo do formato quadrado da v3 com a v2 porfavor. e sim aplique essas outras alteracoes." (musgo mais escuro e em manchas menores)
- **O que mudou:** mesma forma; musgo em manchas nas faces viradas para cima (na cor de vértice, verde que combina com o pinheiro); mais variação de cor: cada face grande vira uma pedra mais areia, laranja ou avermelhada, mais manchas grandes de claro/escuro.

### v003
- Gerado com: `--seed 7 --size 2.7 --depth 0.8 --height 0.66 --points 14 --merge 18.0 --jitter 0.18 --bevel 0.022 --inflate 0.3 --tone 0.8 --moss 0.8 --variety 0.6`
- Triângulos: LOD0 254, LOD1 112, LOD2 16, COL 20
- Prévia: `RochaA_v003_preview.png`
- **Feedback do desenvolvedor (literal):** ver v002 (comparadas juntas).
- **O que mudou:** igual à v002, mas menos arredondada (arestas mais firmes: chanfro 0,022 contra 0,05; 14 pontos; planos maiores) e mais achatada (1,17 m de altura).

### v004
- Gerado com: `--seed 7 --size 2.7 --depth 0.8 --height 0.74 --points 16 --merge 22.0 --jitter 0.18 --bevel 0.035 --inflate 0.3 --flat 0.75 --edge_angle 24.0 --tone 0.8 --moss 0.8 --variety 0.6 --moss_cover -0.15 --moss_up 0.5`
- Triângulos: LOD0 234, LOD1 100, LOD2 16, COL 20
- Prévia: `RochaA_v004_preview.png`
- **Feedback do desenvolvedor (literal):** "voce acha que as cores estao boas? nao tem que ser um pouco mais pro cinza do que laranja? o que acha? é um primeiro rio na floresta, alem de que ainda ta tudo laranja, deixa um pouco mais variada a cor"
- **O que mudou:** forma no meio-termo entre v002 e v003 (chanfro 0,035, 16 pontos, 1,31 m de altura); musgo mais escuro (verde do pinheiro) e em manchas menores, só no topo; arredonda só as quinas de verdade (> 24°) e junta mais os planos quase retos, o que eliminou os traços claros dentro das faces; as tiras das arestas herdam o tom das faces vizinhas.

### v005
- Gerado com: `--seed 7 --size 2.7 --depth 0.8 --height 0.74 --points 16 --merge 22.0 --jitter 0.18 --bevel 0.035 --inflate 0.3 --flat 0.75 --edge_angle 24.0 --tone 0.8 --moss 0.8 --variety 0.6 --warmth 0.4 --moss_cover -0.15 --moss_up 0.5`
- Triângulos: LOD0 234, LOD1 100, LOD2 16, COL 20
- Prévia: `RochaA_v005_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Cor de pedra de rio de floresta: base cinza-quente (cada face sorteia cinza, cinza-claro, cinza-frio ou bege), topo cinza-claro, base cinza-escuro; o laranja antigo virou só manchas de ferrugem suaves (warmth 0.4). Forma e musgo iguais à v004.
