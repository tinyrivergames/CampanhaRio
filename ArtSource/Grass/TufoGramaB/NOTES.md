# Grass / TufoGramaB

## Briefing (2026-10-06, Fase 3)
- **O que é:** o tufo de grama da floresta (espalhado aos milhares), no estilo das lâminas do pinheiro aprovado.
- **Pedido do desenvolvedor:** "monta as texturas principais da floresta e popule ela como floresta" (referência: floresta low-poly densa com grama e capim).
- **Triângulos:** LOD0 <= 120, LOD1 <= 40, LOD2 <= 8.
- **Gerador:** `ArtSource/Grass/grass_gen.py`.

## Versões

### v001
- Gerado com: `--seed 7 --blades 26 --height 0.32 --spread 0.28 --lean 1.0 --width 0.03 --root #2C4A1F --mid #3F6B2C --tip #6E9A3E`
- Triângulos: LOD0 130, LOD1 40, LOD2 2
- Prévia: `TufoGramaB_v001_preview.png`
- **Feedback do desenvolvedor (literal):** "show, espalha bem elas no mapa e deixa bem preenchido." → **APROVADA**
- **O que mudou:** primeira versão.

### v002
- Gerado com: `--seed 7 --blades 26 --style tuft --patch 0.8 --height 0.36 --spread 0.28 --lean 1.0 --width 0.03 --root #1A3F26 --mid #2A6636 --tip #5E9446 --petal #F4F1E6 --heart #F2C53D`
- Triângulos: LOD0 130, LOD1 40, LOD2 2
- Prévia: `TufoGramaB_v002_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Cores do tapete (verde de floresta) para mesclar com a grama baixa (feedback: os tufos não encaixavam).

### v003
- Gerado com: `--seed 7 --blades 26 --style tuft --patch 0.8 --height 0.42 --spread 0.28 --lean 1.0 --width 0.035 --root #245019 --mid #3E8A2A --tip #7FBF45 --petal #F4F1E6 --heart #F2C53D`
- Triângulos: LOD0 130, LOD1 40, LOD2 2
- Prévia: `TufoGramaB_v003_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Verde vivo da referência do desenvolvedor (imagem da agência).

### v004
- Gerado com: `--seed 7 --blades 26 --style tuft --patch 0.8 --height 0.42 --spread 0.28 --lean 1.0 --width 0.035 --root #214A17 --mid #3A8228 --tip #70B03E --petal #F4F1E6 --heart #F2C53D`
- Triângulos: LOD0 130, LOD1 40, LOD2 2
- Prévia: `TufoGramaB_v004_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Verde menos limão (comparação com a referência).
