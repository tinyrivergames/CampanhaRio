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
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** mesma forma; musgo em manchas nas faces viradas para cima (na cor de vértice, verde que combina com o pinheiro); mais variação de cor: cada face grande vira uma pedra mais areia, laranja ou avermelhada, mais manchas grandes de claro/escuro.

### v003
- Gerado com: `--seed 7 --size 2.7 --depth 0.8 --height 0.66 --points 14 --merge 18.0 --jitter 0.18 --bevel 0.022 --inflate 0.3 --tone 0.8 --moss 0.8 --variety 0.6`
- Triângulos: LOD0 254, LOD1 112, LOD2 16, COL 20
- Prévia: `RochaA_v003_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** igual à v002, mas menos arredondada (arestas mais firmes: chanfro 0,022 contra 0,05; 14 pontos; planos maiores) e mais achatada (1,17 m de altura).
