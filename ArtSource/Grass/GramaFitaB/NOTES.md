# Grass / GramaFitaB

## Briefing (2026-10-06, Fase 3)
- **O que é:** o tufo de grama da floresta (espalhado aos milhares), no estilo das lâminas do pinheiro aprovado.
- **Pedido do desenvolvedor:** "monta as texturas principais da floresta e popule ela como floresta" (referência: floresta low-poly densa com grama e capim).
- **Triângulos:** LOD0 <= 120, LOD1 <= 40, LOD2 <= 8.
- **Gerador:** `ArtSource/Grass/grass_gen.py`.

## Versões

### v001
- Gerado com: `--seed 12 --blades 26 --style ribbon --patch 0.8 --height 0.85 --spread 0.2 --lean 1.35 --width 0.095 --root #12400F --mid #3A8226 --tip #8DC04A --petal #F4F1E6 --up_normals 0.88 --shadows 0.4 --wrap 0.85 --ao 0.6 --transl 0.05 --shade_var 0.38 --heart #F2C53D`
- Triângulos: LOD0 936, LOD1 170, LOD2 12
- Prévia: `GramaFitaB_v001_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** primeira versão.

### v002
- Gerado com: `--seed 12 --blades 28 --style ribbon --patch 0.8 --height 0.85 --spread 0.22 --lean 1.35 --width 0.095 --root #1C5214 --mid #4A962C --tip #A5D157 --petal #F4F1E6 --up_normals 0.88 --shadows 0.4 --wrap 0.85 --ao 0.6 --transl 0.05 --shade_var 0.32 --heart #F2C53D`
- Triângulos: LOD0 1008, LOD1 180, LOD2 14
- Prévia: `GramaFitaB_v002_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Mais clara e quente no jogo (comparada lado a lado com a referência).

### v003
- Gerado com: `--seed 12 --blades 18 --style ribbon --patch 0.8 --height 0.82 --spread 0.2 --lean 0.55 --width 0.11 --root #2A6A40 --mid #46904A --tip #84B652 --petal #F4F1E6 --up_normals 0.88 --shadows 0.4 --wrap 0.85 --ao 0.5 --transl 0.05 --shade_var 0.3 --leaf 0.55 --fold_dark 0.72 --hue_var 0.3 --heart #F2C53D`
- Triângulos: LOD0 648, LOD1 120, LOD2 8
- Prévia: `GramaFitaB_v003_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Lâmina em folha larga (largura cheia até o meio, depois a ponta), quase ereta, lado escuro e lado claro na dobra, verdes variados (feedback: parecia fiapo).

### v004
- Gerado com: `--seed 12 --blades 18 --style ribbon --patch 0.8 --height 0.82 --spread 0.2 --lean 0.55 --width 0.11 --root #2E6C3E --mid #559C48 --tip #A2C85A --petal #F4F1E6 --up_normals 0.88 --shadows 0.4 --wrap 0.85 --ao 0.5 --transl 0.05 --shade_var 0.3 --leaf 0.55 --fold_dark 0.74 --hue_var 0.3 --heart #F2C53D`
- Triângulos: LOD0 648, LOD1 120, LOD2 8
- Prévia: `GramaFitaB_v004_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Pontas mais quentes e claras (comparação no jogo).

### v005
- Gerado com: `--seed 12 --blades 18 --style ribbon --patch 0.8 --height 0.82 --spread 0.2 --lean 0.55 --width 0.11 --root #3B6E36 --mid #5C9444 --tip #8DB860 --petal #F4F1E6 --up_normals 0.98 --shadows 0.3 --wrap 0.9 --ao 0.4 --transl 0.05 --shade_var 0.12 --leaf 0.55 --fold_dark 0.95 --hue_var 0.1 --heart #F2C53D`
- Triângulos: LOD0 648, LOD1 120, LOD2 8
- Prévia: `GramaFitaB_v005_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Macia e menos forte (feedback: parecia esmeraldas brilhando com lineart): dobra quase sem contraste, luz igual nas lâminas, verde mais suave perto do chão.

### v006
- Gerado com: `--seed 12 --blades 18 --style ribbon --patch 0.8 --height 0.55 --spread 0.2 --lean 0.55 --width 0.11 --root #173A20 --mid #3A7030 --tip #7A9E48 --petal #F4F1E6 --up_normals 0.95 --shadows 0.3 --wrap 0.7 --ao 0.8 --transl 0.05 --shade_var 0.15 --leaf 0.55 --fold_dark 0.95 --hue_var 0.12 --heart #F2C53D`
- Triângulos: LOD0 648, LOD1 120, LOD2 8
- Prévia: `GramaFitaB_v006_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Touceira escura (manchas mais escuras, como na referência).

### v007
- Gerado com: `--seed 12 --blades 24 --style ribbon --patch 0.8 --height 0.65 --spread 0.2 --lean 0.8 --width 0.065 --root #45684A --mid #6E9862 --tip #A9C487 --petal #F4F1E6 --up_normals 0.95 --shadows 0.3 --wrap 0.7 --ao 0.7 --transl 0.05 --shade_var 0.15 --leaf 0.25 --fold_dark 0.95 --hue_var 0.15 --heart #F2C53D`
- Triângulos: LOD0 864, LOD1 160, LOD2 12
- Prévia: `GramaFitaB_v007_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Folhas mais finas e longas nas pontas, cores pastel (feedback).

### v008
- Gerado com: `--seed 12 --blades 24 --style ribbon --patch 0.8 --height 0.65 --spread 0.2 --lean 0.8 --width 0.065 --root #3D6342 --mid #5F9050 --tip #90B86C --petal #F4F1E6 --up_normals 0.95 --shadows 0.3 --wrap 0.7 --ao 0.7 --transl 0.05 --shade_var 0.15 --leaf 0.25 --fold_dark 0.95 --hue_var 0.15 --heart #F2C53D`
- Triângulos: LOD0 864, LOD1 160, LOD2 12
- Prévia: `GramaFitaB_v008_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Pastel com mais verde (o anterior desbotou).

### v009
- Gerado com: `--seed 12 --blades 24 --style ribbon --patch 0.8 --height 0.65 --spread 0.2 --lean 0.8 --width 0.065 --root #2F5A34 --mid #4E8A40 --tip #88B25A --petal #F4F1E6 --up_normals 0.95 --shadows 0.3 --wrap 0.7 --ao 0.7 --transl 0.05 --shade_var 0.15 --leaf 0.25 --fold_dark 0.95 --hue_var 0.15 --heart #F2C53D`
- Triângulos: LOD0 864, LOD1 160, LOD2 12
- Prévia: `GramaFitaB_v009_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Meio-termo entre pastel e verde vivo.

### v010
- Gerado com: `--seed 12 --blades 14 --style ribbon --patch 0.8 --height 0.8 --spread 0.24 --lean 0.7 --width 0.06 --root #3E6B38 --mid #568A48 --tip #84AE62 --petal #F4F1E6 --up_normals 0.97 --shadows 0.25 --wrap 0.8 --ao 0.3 --transl 0.05 --shade_var 0.06 --leaf 0.25 --fold_dark 0.98 --hue_var 0.05 --heart #F2C53D`
- Triângulos: LOD0 504, LOD1 90, LOD2 6
- Prévia: `GramaFitaB_v010_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Estilo RV There Yet: a raiz com a cor exata do chão, quase uma cor por lâmina, pouca variação, lâminas mais soltas e longas.

### v011
- Gerado com: `--seed 12 --blades 14 --style ribbon --patch 0.8 --height 0.8 --spread 0.24 --lean 0.7 --width 0.06 --root #3E6B38 --mid #42703B --tip #547E46 --petal #F4F1E6 --up_normals 0.97 --shadows 0.25 --wrap 0.8 --ao 0.3 --transl 0.05 --shade_var 0.06 --leaf 0.25 --fold_dark 0.98 --hue_var 0.05 --heart #F2C53D`
- Triângulos: LOD0 504, LOD1 90, LOD2 6
- Prévia: `GramaFitaB_v011_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Teste: grama escurecida quase até a cor do chão (aspecto de floresta).

### v012
- Gerado com: `--seed 12 --blades 14 --style ribbon --patch 0.8 --height 0.8 --spread 0.24 --lean 0.7 --width 0.06 --root #3D6B38 --mid #3D6B38 --tip #426F3C --petal #F4F1E6 --up_normals 0.97 --shadows 0.25 --wrap 0.8 --ao 0.3 --transl 0.05 --shade_var 0.05 --leaf 0.25 --fold_dark 0.98 --hue_var 0.04 --heart #F2C53D`
- Triângulos: LOD0 504, LOD1 90, LOD2 6
- Prévia: `GramaFitaB_v012_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Teste C: grama praticamente da cor do chão.

### v013
- Gerado com: `--seed 12 --blades 14 --style ribbon --patch 0.8 --height 0.8 --spread 0.24 --lean 0.7 --width 0.06 --root #2F532C --mid #2F532C --tip #33572F --petal #F4F1E6 --up_normals 0.97 --shadows 0.25 --wrap 0.8 --ao 0.3 --transl 0.05 --shade_var 0.05 --leaf 0.25 --fold_dark 0.98 --hue_var 0.04 --heart #F2C53D`
- Triângulos: LOD0 504, LOD1 90, LOD2 6
- Prévia: `GramaFitaB_v013_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Teste C: grama da cor do chão como aparece no jogo (compensando a luz mais forte da grama).

### v014
- Gerado com: `--seed 12 --blades 14 --style ribbon --patch 0.8 --height 0.8 --spread 0.24 --lean 0.7 --width 0.06 --root #AEB2AE --mid #AEB2AE --tip #B4B8B4 --petal #F4F1E6 --up_normals 0.97 --shadows 0.25 --wrap 0.8 --ao 0.3 --transl 0.05 --shade_var 0.05 --leaf 0.25 --fold_dark 0.98 --hue_var 0.02 --heart #F2C53D --ground_tint 1.0`
- Triângulos: LOD0 504, LOD1 90, LOD2 6
- Prévia: `GramaFitaB_v014_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Cor neutra: no jogo cada tufo pega a cor do chao onde nasce (GrassField.groundTint); o vertex color so guarda a variacao entre tufos.

### v015
- Gerado com: `--seed 12 --blades 14 --style ribbon --patch 0.8 --height 0.8 --spread 0.24 --lean 0.7 --width 0.06 --root #AEB2AE --mid #AEB2AE --tip #B4B8B4 --petal #F4F1E6 --up_normals 0.97 --shadows 1.0 --wrap 0.8 --ao 0.3 --transl 0.05 --shade_var 0.05 --leaf 0.25 --fold_dark 0.98 --hue_var 0.02 --heart #F2C53D --ground_tint 1.0`
- Triângulos: LOD0 504, LOD1 90, LOD2 6
- Prévia: `GramaFitaB_v015_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Sombras: a grama recebe as sombras com forca total, como o chao.
