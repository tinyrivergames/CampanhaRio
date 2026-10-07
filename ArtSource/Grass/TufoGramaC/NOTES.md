# Grass / TufoGramaC

## Briefing (2026-10-06, Fase 3)
- **O que é:** o tufo de grama da floresta (espalhado aos milhares), no estilo das lâminas do pinheiro aprovado.
- **Pedido do desenvolvedor:** "monta as texturas principais da floresta e popule ela como floresta" (referência: floresta low-poly densa com grama e capim).
- **Triângulos:** LOD0 <= 120, LOD1 <= 40, LOD2 <= 8.
- **Gerador:** `ArtSource/Grass/grass_gen.py`.

## Versões

### v001
- Gerado com: `--seed 9 --blades 18 --height 0.8 --spread 0.24 --lean 1.2 --width 0.025 --root #6E6A32 --mid #B39A4A --tip #E3C46A`
- Triângulos: LOD0 90, LOD1 30, LOD2 2
- Prévia: `TufoGramaC_v001_preview.png`
- **Feedback do desenvolvedor (literal):** "show, espalha bem elas no mapa e deixa bem preenchido." → **APROVADA**
- **O que mudou:** primeira versão.
