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

### v007
- Gerado com: `--seed 5 --blades 18 --style ribbon --patch 0.8 --height 0.5 --spread 0.2 --lean 0.55 --width 0.11 --root #1F4424 --mid #4A8636 --tip #9AB952 --petal #F4F1E6 --up_normals 0.95 --shadows 0.3 --wrap 0.7 --ao 0.8 --transl 0.05 --shade_var 0.15 --leaf 0.55 --fold_dark 0.95 --hue_var 0.12 --heart #F2C53D`
- Triângulos: LOD0 648, LOD1 120, LOD2 8
- Prévia: `GramaFitaA_v007_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Verde mais fechado (menos limão), base escura (feedback: comparar com a referência).

### v008
- Gerado com: `--seed 5 --blades 24 --style ribbon --patch 0.8 --height 0.6 --spread 0.2 --lean 0.8 --width 0.065 --root #557A50 --mid #84AC6C --tip #C6DA94 --petal #F4F1E6 --up_normals 0.95 --shadows 0.3 --wrap 0.7 --ao 0.7 --transl 0.05 --shade_var 0.15 --leaf 0.25 --fold_dark 0.95 --hue_var 0.15 --heart #F2C53D`
- Triângulos: LOD0 864, LOD1 160, LOD2 12
- Prévia: `GramaFitaA_v008_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Folhas mais finas e longas nas pontas, cores pastel (feedback).

### v009
- Gerado com: `--seed 5 --blades 24 --style ribbon --patch 0.8 --height 0.6 --spread 0.2 --lean 0.8 --width 0.065 --root #4A7448 --mid #72A65A --tip #A8CC78 --petal #F4F1E6 --up_normals 0.95 --shadows 0.3 --wrap 0.7 --ao 0.7 --transl 0.05 --shade_var 0.15 --leaf 0.25 --fold_dark 0.95 --hue_var 0.15 --heart #F2C53D`
- Triângulos: LOD0 864, LOD1 160, LOD2 12
- Prévia: `GramaFitaA_v009_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Pastel com mais verde (o anterior desbotou).

### v010
- Gerado com: `--seed 5 --blades 24 --style ribbon --patch 0.8 --height 0.6 --spread 0.2 --lean 0.8 --width 0.065 --root #3A6A3A --mid #62A048 --tip #A4CA66 --petal #F4F1E6 --up_normals 0.95 --shadows 0.3 --wrap 0.7 --ao 0.7 --transl 0.05 --shade_var 0.15 --leaf 0.25 --fold_dark 0.95 --hue_var 0.15 --heart #F2C53D`
- Triângulos: LOD0 864, LOD1 160, LOD2 12
- Prévia: `GramaFitaA_v010_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Meio-termo entre pastel e verde vivo.

### v011
- Gerado com: `--seed 5 --blades 14 --style ribbon --patch 0.8 --height 0.75 --spread 0.24 --lean 0.7 --width 0.06 --root #548743 --mid #6A9A50 --tip #9CC06E --petal #F4F1E6 --up_normals 0.97 --shadows 0.25 --wrap 0.8 --ao 0.3 --transl 0.05 --shade_var 0.06 --leaf 0.25 --fold_dark 0.98 --hue_var 0.05 --heart #F2C53D`
- Triângulos: LOD0 504, LOD1 90, LOD2 6
- Prévia: `GramaFitaA_v011_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Estilo RV There Yet: a raiz com a cor exata do chão, quase uma cor por lâmina, pouca variação, lâminas mais soltas e longas.

### v012
- Gerado com: `--seed 5 --blades 14 --style ribbon --patch 0.8 --height 0.75 --spread 0.24 --lean 0.7 --width 0.06 --root #548743 --mid #578A45 --tip #6B9952 --petal #F4F1E6 --up_normals 0.97 --shadows 0.25 --wrap 0.8 --ao 0.3 --transl 0.05 --shade_var 0.06 --leaf 0.25 --fold_dark 0.98 --hue_var 0.05 --heart #F2C53D`
- Triângulos: LOD0 504, LOD1 90, LOD2 6
- Prévia: `GramaFitaA_v012_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Teste: grama escurecida quase até a cor do chão (aspecto de floresta).

### v013
- Gerado com: `--seed 5 --blades 14 --style ribbon --patch 0.8 --height 0.75 --spread 0.24 --lean 0.7 --width 0.06 --root #548743 --mid #548743 --tip #5B8C49 --petal #F4F1E6 --up_normals 0.97 --shadows 0.25 --wrap 0.8 --ao 0.3 --transl 0.05 --shade_var 0.05 --leaf 0.25 --fold_dark 0.98 --hue_var 0.04 --heart #F2C53D`
- Triângulos: LOD0 504, LOD1 90, LOD2 6
- Prévia: `GramaFitaA_v013_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Teste C: grama praticamente da cor do chão.

### v014
- Gerado com: `--seed 5 --blades 14 --style ribbon --patch 0.8 --height 0.75 --spread 0.24 --lean 0.7 --width 0.06 --root #41693A --mid #41693A --tip #476E3E --petal #F4F1E6 --up_normals 0.97 --shadows 0.25 --wrap 0.8 --ao 0.3 --transl 0.05 --shade_var 0.05 --leaf 0.25 --fold_dark 0.98 --hue_var 0.04 --heart #F2C53D`
- Triângulos: LOD0 504, LOD1 90, LOD2 6
- Prévia: `GramaFitaA_v014_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Teste C: grama da cor do chão como aparece no jogo (compensando a luz mais forte da grama).

### v015
- Gerado com: `--seed 5 --blades 14 --style ribbon --patch 0.8 --height 0.75 --spread 0.24 --lean 0.7 --width 0.06 --root #E6E6E6 --mid #E6E6E6 --tip #EDEDED --petal #F4F1E6 --up_normals 0.97 --shadows 0.25 --wrap 0.8 --ao 0.3 --transl 0.05 --shade_var 0.05 --leaf 0.25 --fold_dark 0.98 --hue_var 0.02 --heart #F2C53D --ground_tint 1.0`
- Triângulos: LOD0 504, LOD1 90, LOD2 6
- Prévia: `GramaFitaA_v015_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Cor neutra: no jogo cada tufo pega a cor do chao onde nasce (GrassField.groundTint); o vertex color so guarda a variacao entre tufos.
