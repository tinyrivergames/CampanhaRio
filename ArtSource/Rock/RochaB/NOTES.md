# Rock / RochaB

## Briefing (confirmado em 2026-10-05)
- **O que é:** variação (RochaB) da âncora aprovada (PinheiroA v008 / RochaA v008), mesmo gerador e mesmos parâmetros de estilo; muda só forma e tamanho. Preparada para aprovação (Fase 3).
- **Tamanho:** ~2,4 x 1,8 x 1,4 m, assentada no chão.
- **Forma:** grandes planos lisos com arestas arredondadas, volume "inflado" (referências `estetica_02`, `estetica_03`).
- **Triângulos:** LOD0 <= 800, LOD1 <= 250, LOD2 <= 80, colisor <= 40.
- **Cores:** rock_orange, topo rock_sand, base rock_shadow; um tom por face (como as lâminas do pinheiro); musgo leve em cima.
- **Pedido do desenvolvedor:** "se baseia em algo pra mesclar bem com os graficos do pinheiro" → facetada como o pinheiro, normais infladas, variação de tom por face.
- **Gerador:** `ArtSource/Rock/rock_gen.py`.

## Versões

### v001
- Gerado com: `--seed 3 --size 1.2 --depth 0.8 --height 0.62 --points 16 --merge 18.0 --jitter 0.22 --bevel 0.016 --inflate 0.05 --patches 0.08 --edge_light 0.22 --ramp 0.14 --wrap 0.3 --flat 1.0 --edge_angle 24.0 --tone 0.8 --moss 0.8 --variety 0.6 --warmth 0.4 --moss_cover -0.15 --moss_up 0.5`
- Triângulos: LOD0 276, LOD1 120, LOD2 16, COL 18
- Prévia: `RochaB_v001_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** primeira versão.
