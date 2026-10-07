# Grass / GramaFitaA

## Briefing (2026-10-06, Fase 3)
- **O que é:** o tufo de grama da floresta (espalhado aos milhares), no estilo das lâminas do pinheiro aprovado.
- **Pedido do desenvolvedor:** "monta as texturas principais da floresta e popule ela como floresta" (referência: floresta low-poly densa com grama e capim).
- **Triângulos:** LOD0 <= 120, LOD1 <= 40, LOD2 <= 8.
- **Gerador:** `ArtSource/Grass/grass_gen.py`.

## Versões

### v001
- Gerado com: `--seed 5 --blades 30 --style ribbon --patch 0.8 --height 0.62 --spread 0.2 --lean 1.35 --width 0.095 --root #154012 --mid #357A1E --tip #86C23E --petal #F4F1E6 --up_normals 0.55 --shadows 0.4 --wrap 0.5 --ao 0.6 --transl 0.05 --heart #F2C53D`
- Triângulos: LOD0 1080, LOD1 200, LOD2 14
- Prévia: `GramaFitaA_v001_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** primeira versão.

### v002
- Gerado com: `--seed 5 --blades 30 --style ribbon --patch 0.8 --height 0.62 --spread 0.2 --lean 1.35 --width 0.095 --root #12400F --mid #3A8226 --tip #8DC04A --petal #F4F1E6 --up_normals 0.88 --shadows 0.4 --wrap 0.85 --ao 0.6 --transl 0.05 --shade_var 0.38 --heart #F2C53D`
- Triângulos: LOD0 1080, LOD1 200, LOD2 14
- Prévia: `GramaFitaA_v002_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Fitas largas e curvas, uma cor por lâmina, normais para cima (sem lâminas cinzas contra o sol), comparada com a referência no Blender.

### v003
- Gerado com: `--seed 5 --blades 32 --style ribbon --patch 0.8 --height 0.62 --spread 0.22 --lean 1.35 --width 0.095 --root #1C5214 --mid #4A962C --tip #A5D157 --petal #F4F1E6 --up_normals 0.88 --shadows 0.4 --wrap 0.85 --ao 0.6 --transl 0.05 --shade_var 0.32 --heart #F2C53D`
- Triângulos: LOD0 1152, LOD1 210, LOD2 16
- Prévia: `GramaFitaA_v003_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Mais clara e quente no jogo (comparada lado a lado com a referência).

### v004
- Gerado com: `--seed 5 --blades 20 --style ribbon --patch 0.8 --height 0.6 --spread 0.2 --lean 0.55 --width 0.11 --root #2A6A40 --mid #46904A --tip #84B652 --petal #F4F1E6 --up_normals 0.88 --shadows 0.4 --wrap 0.85 --ao 0.5 --transl 0.05 --shade_var 0.3 --leaf 0.55 --fold_dark 0.72 --hue_var 0.3 --heart #F2C53D`
- Triângulos: LOD0 720, LOD1 130, LOD2 10
- Prévia: `GramaFitaA_v004_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Lâmina em folha larga (largura cheia até o meio, depois a ponta), quase ereta, lado escuro e lado claro na dobra, verdes variados (feedback: parecia fiapo).

### v005
- Gerado com: `--seed 5 --blades 20 --style ribbon --patch 0.8 --height 0.6 --spread 0.2 --lean 0.55 --width 0.11 --root #2E6C3E --mid #559C48 --tip #A2C85A --petal #F4F1E6 --up_normals 0.88 --shadows 0.4 --wrap 0.85 --ao 0.5 --transl 0.05 --shade_var 0.3 --leaf 0.55 --fold_dark 0.74 --hue_var 0.3 --heart #F2C53D`
- Triângulos: LOD0 720, LOD1 130, LOD2 10
- Prévia: `GramaFitaA_v005_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Pontas mais quentes e claras (comparação no jogo).

### v006
- Gerado com: `--seed 5 --blades 20 --style ribbon --patch 0.8 --height 0.6 --spread 0.2 --lean 0.55 --width 0.11 --root #3B6E36 --mid #5C9444 --tip #8DB860 --petal #F4F1E6 --up_normals 0.98 --shadows 0.3 --wrap 0.9 --ao 0.4 --transl 0.05 --shade_var 0.12 --leaf 0.55 --fold_dark 0.95 --hue_var 0.1 --heart #F2C53D`
- Triângulos: LOD0 720, LOD1 130, LOD2 10
- Prévia: `GramaFitaA_v006_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Macia e menos forte (feedback: parecia esmeraldas brilhando com lineart): dobra quase sem contraste, luz igual nas lâminas, verde mais suave perto do chão.
