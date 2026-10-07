# Rock / PedraCinzaB

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
- Gerado com: `--seed 63 --size 1.6 --depth 0.8 --height 0.62 --points 28 --merge 9.0 --jitter 0.15 --bevel 0.02 --inflate 0.35 --patches 0.06 --edge_light 0.1 --ramp 0.22 --wrap 0.4 --flat 0.7 --edge_angle 24.0 --tone 0.3 --moss 0.0 --variety 0.6 --warmth 0.0 --desat 1.0 --moss_dark #3F5A2E --moss_light #5C7A38 --moss_cover 0.35 --moss_up 0.35`
- Triângulos: LOD0 328, LOD1 160, LOD2 22, COL 20
- Prévia: `PedraCinzaB_v001_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** primeira versão.
