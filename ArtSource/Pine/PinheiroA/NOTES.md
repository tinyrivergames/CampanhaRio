# Pine / PinheiroA

## Briefing (aprovado em 2026-10-05, opção A)
- **O que é:** o pinheiro-mestre da família (âncora de estilo da Fase 1).
- **Tamanho:** ~9 m de altura, copa ~4 m, tronco 0,35 m na base, 1,5 m visível, levemente inclinado.
- **Forma:** 5 "saias" arredondadas e infladas, bordas onduladas e macias, ponta arredondada, copa cheia.
- **Triângulos:** LOD0 <= 700, LOD1 <= 250, LOD2 impostor (2 tris), fundo: cartão-borrão (2 tris).
- **Cores:** pine_dark (base, interior) -> pine_light (topo), tronco bark. Uma cor por vértice, um material só.
- **Técnica:** opção A, saias sólidas sem transparência; normais "infladas" (puxadas para um elipsoide da copa).
- **Referências:** Docs/Reference/estetica_01_floresta e estetica_03_lago.
- **Gerador:** `ArtSource/Pine/pine_gen.py`. O impostor e o cartão-borrão vêm depois que a forma for aprovada.

## Versões

### v001
- Gerado com: `--seed 4 --height 9.0 --radius 2.0 --layers 5 --lean 0.04 --scallop 0.28 --droop 0.12 --inflate 0.45`
- Triângulos: LOD0 524, LOD1 248
- Prévia: `PinheiroA_v001_preview.png`
- **Feedback do desenvolvedor (literal):** "vi aqui, me parecei muito simples. mas sim parece um pinheiro. Pode deixar esse pinheiro com folhagem mais detalhada/bonita. Quero um jogo bem bonito de se ver com os graficos, mas ainda na ideia low poly. Tente colocar folhas mais detalhadas ao inves dessa simplicidade toda."
- **O que mudou:** primeira versão.

### v002
- Gerado com: `--style tufts --whorls 9 --seed 4 --height 9.0 --radius 2.0 --layers 5 --lean 0.04 --scallop 0.28 --droop 0.12 --inflate 0.4`
- Triângulos: LOD0 1395, LOD1 611
- Prévia: `PinheiroA_v002_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** as "saias" viraram galhos individuais (tufos alongados, levemente caídos, com crista e bordas recortadas de tufos de agulhas) em 9 verticilos ao redor do tronco; núcleo escuro por dentro, ponta própria. Orçamento subiu para LOD0 1.500 / LOD1 500 (pedido de mais detalhe).
