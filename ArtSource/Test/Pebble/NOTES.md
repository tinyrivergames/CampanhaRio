# Test / Pebble

## Briefing
- **O que é:** um seixo de rio arredondado e estilizado. **Asset descartável**: só prova o pipeline (Fase 0).
- **Referências:** pedras lisas laranja/areia das imagens de referência (Docs/Reference).
- **Tamanho:** ~0,62 x 0,46 x 0,32 m.
- **Orçamento de triângulos:** LOD0 <= 600, LOD1 <= 150, colisor <= 40.
- **Cores:** rock_sand com topo levemente musgo (paleta do rig).
- **Gerador:** `ArtSource/Test/pebble_gen.py`.

## Versões

### v001
- Gerado com: `--asset Pebble --seed 3 --length 0.62 --width 0.46 --height 0.32`
- Triângulos: LOD0 600, LOD1 150, COL 40
- Prévia: `Pebble_v001_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** primeira versão.
- **Fase 0 (teste do pipeline):** a prévia e o `.blend` foram abertos para o desenvolvedor (primeiro uso real do
  `art review`). Como o seixo é descartável, o ciclo seguiu sem esperar: `art export` → importado no Unity (2 LODs,
  colisor convexo, material SoftToon "Rock" a partir do `Pebble.softtoon.json`) → print na cena LookDev
  (`Docs/Screenshots/2026-10-03_2058_pebble/`) → comparação `Pebble_blender_vs_unity.png`.
- **Mudança no pipeline (mesma v001):** a folha de prévia passou a aplicar a cadeia de pós do Unity (tonemapping Neutral
  do URP), para mostrar as cores como no jogo. A prévia foi renderizada de novo a partir do mesmo `.blend`.
