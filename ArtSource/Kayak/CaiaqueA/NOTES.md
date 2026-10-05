# Kayak / CaiaqueA

## Briefing (confirmado em 2026-10-05)
- **O que é:** o caiaque do jogador (âncora de estilo da Fase 1), o barco da Agência do Rio: leva o remador e, atrás do cockpit, uma encomenda ou um segundo passageiro.
- **Tamanho:** 3,3 x 0,68 m, o mesmo do colisor do jogo (a física não muda; sem `_COL`, o jogo usa a cápsula dele).
- **Referência:** a foto do desenvolvedor (caiaque sit-in laranja e preto, cabos em X, tampa redonda, assento preto).
- **Estilo:** facetado como o pinheiro e a pedra (planos que aparecem, luz firme), "bem detalhado".
- **Triângulos:** LOD0 <= 2500, LOD1 <= 800, LOD2 <= 200.
- **Pedido do desenvolvedor (literal):** "voce pegou o contexto de que o jogo sera a ideia de uma agencia do rio e das entregas e turistas certo? faca o caiaque basico com base nisso [...] teremos animacoes do personagem dentro do caiaque e que esse primeiro caiaque unico vai levar possivelmente um segundo passageiro ou uma encomenda com ele. Mas faca bem detalhado, seguindo a estica das arvores."
- **Gerador:** `ArtSource/Kayak/kayak_gen.py`.

## Versões

### v001
- Gerado com: `--length 3.3 --width 0.68 --widest 0.06 --rocker 0.11 --sheer_rise 0.07 --end_rise 0.13 --stations 18 --cockpit_y 0.1 --cockpit_l 0.86 --cockpit_w 0.44 --well_y 0.98 --well_l 0.52 --well_w 0.36 --hatch_y -1.17 --flat 0.75 --tone 0.04 --ramp 0.16 --wrap 0.35`
- Triângulos: LOD0 1596, LOD1 612, LOD2 112
- Prévia: `CaiaqueA_v001_preview.png`
- **Feedback do desenvolvedor (literal):** _(não mostrada: afundava na água do jogo, substituída pela v002)_
- **O que mudou:** primeira versão.

### v002
- Gerado com: `--length 3.3 --width 0.68 --widest 0.06 --rocker 0.11 --sheer_rise 0.07 --end_rise 0.13 --lift 0.08 --stations 18 --cockpit_y 0.1 --cockpit_l 0.86 --cockpit_w 0.44 --well_y 0.98 --well_l 0.52 --well_w 0.36 --hatch_y -1.17 --flat 0.75 --tone 0.04 --ramp 0.16 --wrap 0.35`
- Triângulos: LOD0 1596, LOD1 612, LOD2 112
- Prévia: `CaiaqueA_v002_preview.png`
- **Feedback do desenvolvedor (literal):** "em relacao ao caiaque, voce acha que encaixou legal com a textura da ideia do jogo? um bug, a agua parece estar entrando no caiaque, ta bugada a visualizacao, as texturas da agua e do caiaque estao se misturando" / "o caiaque ta muito detalhado talvez, tenta uma versao um pouco mais simples que tenha uma mesclagem mais natural com o ambiente do jogo.."
- **O que mudou:** v001 (não mostrada) afundava na água do jogo: casco 8 cm mais alto; o assento continua na altura do quadril do remador.

### v003
- Gerado com: `--length 3.3 --width 0.68 --widest 0.06 --rocker 0.11 --sheer_rise 0.07 --end_rise 0.13 --lift 0.08 --simple 1 --stations 12 --cockpit_y 0.1 --cockpit_l 0.86 --cockpit_w 0.44 --well_y 0.98 --well_l 0.52 --well_w 0.36 --hatch_y -1.17 --flat 0.75 --tone 0.04 --ramp 0.16 --wrap 0.35`
- Triângulos: LOD0 972, LOD1 584, LOD2 112
- Prévia: `CaiaqueA_v003_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Mais simples, para mesclar com o pinheiro e a pedra: menos facetas (12 estações), sem presilhas, tampa, alças e tira do remo; só o X de cabos na frente e a rede cruzada na carga; assento em bloco; laranja mais terroso e grafite quente no lugar do preto. Fundo do cockpit e assento não atravessam mais o casco.
