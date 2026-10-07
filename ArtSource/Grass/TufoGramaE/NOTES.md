# Grass / TufoGramaE

## Briefing (2026-10-06, Fase 3)
- **O que é:** o tufo de grama da floresta (espalhado aos milhares), no estilo das lâminas do pinheiro aprovado.
- **Pedido do desenvolvedor:** "monta as texturas principais da floresta e popule ela como floresta" (referência: floresta low-poly densa com grama e capim).
- **Triângulos:** LOD0 <= 120, LOD1 <= 40, LOD2 <= 8.
- **Gerador:** `ArtSource/Grass/grass_gen.py`.

## Versões

### v001
- Gerado com: `--seed 17 --blades 20 --height 0.42 --spread 0.24 --lean 1.0 --width 0.03 --root #2F5240 --mid #4E7E62 --tip #8DB494`
- Triângulos: LOD0 100, LOD1 30, LOD2 2
- Prévia: `TufoGramaE_v001_preview.png`
- **Feedback do desenvolvedor (literal):** "show, espalha bem elas no mapa e deixa bem preenchido." → **APROVADA**
- **O que mudou:** primeira versão.

### v002
- Gerado com: `--seed 17 --blades 20 --style tuft --patch 0.8 --height 0.45 --spread 0.24 --lean 1.0 --width 0.03 --root #1C4632 --mid #2E7448 --tip #6FAE68 --petal #F4F1E6 --heart #F2C53D`
- Triângulos: LOD0 100, LOD1 30, LOD2 2
- Prévia: `TufoGramaE_v002_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Cores do tapete (verde de floresta) para mesclar com a grama baixa (feedback: os tufos não encaixavam).

### v003
- Gerado com: `--seed 17 --blades 20 --style tuft --patch 0.8 --height 0.5 --spread 0.24 --lean 1.0 --width 0.035 --root #285A22 --mid #45923A --tip #88C860 --petal #F4F1E6 --heart #F2C53D`
- Triângulos: LOD0 100, LOD1 30, LOD2 2
- Prévia: `TufoGramaE_v003_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Verde vivo da referência do desenvolvedor (imagem da agência).

### v004
- Gerado com: `--seed 17 --blades 20 --style tuft --patch 0.8 --height 0.5 --spread 0.24 --lean 1.0 --width 0.035 --root #255420 --mid #3F8936 --tip #78B656 --petal #F4F1E6 --heart #F2C53D`
- Triângulos: LOD0 100, LOD1 30, LOD2 2
- Prévia: `TufoGramaE_v004_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Verde menos limão (comparação com a referência).
