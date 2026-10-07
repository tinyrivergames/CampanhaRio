# Rock / PedraMusgoA

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
- Gerado com: `--seed 31 --size 3.0 --depth 0.8 --height 0.85 --points 26 --merge 18.0 --jitter 0.22 --bevel 0.016 --inflate 0.18 --patches 0.08 --edge_light 0.18 --ramp 0.2 --wrap 0.35 --flat 0.9 --edge_angle 24.0 --tone 0.8 --moss 1.0 --variety 0.6 --warmth 0.0 --desat 0.85 --moss_dark #5E8E2E --moss_light #9CC842 --moss_cover 0.15 --moss_up 0.35`
- Triângulos: LOD0 356, LOD1 160, LOD2 22, COL 20
- Prévia: `PedraMusgoA_v001_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** primeira versão.

### v002
- Gerado com: `--seed 31 --size 3.0 --depth 0.8 --height 0.85 --points 30 --merge 11.0 --jitter 0.2 --bevel 0.016 --inflate 0.18 --patches 0.08 --edge_light 0.18 --ramp 0.2 --wrap 0.35 --flat 0.9 --edge_angle 24.0 --tone 0.8 --moss 1.0 --variety 0.6 --warmth 0.0 --desat 0.85 --moss_dark #6E9E32 --moss_light #A6D044 --moss_cover 0.7 --moss_up 0.2`
- Triângulos: LOD0 398, LOD1 182, LOD2 26, COL 20
- Prévia: `PedraMusgoA_v002_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Mais facetas e capa de musgo nítida no topo.
