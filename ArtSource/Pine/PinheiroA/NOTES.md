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
- **Feedback do desenvolvedor (literal):** "ainda um pouco esquisito, pode ser um pouco mais elaborado nessa referencia" (imagem: `Docs/Reference/Pinheiro/ref_spruce_fir.png`, os pinheiros "spruce" e "fir")
- **O que mudou:** as "saias" viraram galhos individuais (tufos alongados, levemente caídos, com crista e bordas recortadas de tufos de agulhas) em 9 verticilos ao redor do tronco; núcleo escuro por dentro, ponta própria. Orçamento subiu para LOD0 1.500 / LOD1 500 (pedido de mais detalhe).

### v003
- Gerado com: `--style blades --tiers 12 --droop_deg 32.0 --curl 0.3839724354387525 --blade_width 0.16 --whorls 9 --seed 4 --height 9.0 --radius 2.0 --layers 5 --lean 0.04 --scallop 0.28 --droop 0.12 --inflate 0.4`
- Triângulos: LOD0 1313, LOD1 301
- Prévia: `PinheiroA_v003_preview.png`
- **Não mostrada ao desenvolvedor:** descartada pelo Claude (galhos caídos demais, parecia um cipreste, tronco escondido). Ajustada em rascunhos até a v004.
- **O que mudou:** primeira tentativa no estilo da referência (camadas de lâminas).

### v004
- Gerado com: `--style blades --tiers 10 --density 1.4 --top_light 0.75 --droop_deg 18.0 --curl 0.2792526803190927 --blade_width 0.14 --whorls 9 --seed 4 --height 9.0 --radius 2.4 --layers 5 --lean 0.04 --scallop 0.28 --droop 0.12 --inflate 0.4`
- Triângulos: LOD0 1521, LOD1 303
- Prévia: `PinheiroA_v004_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** no estilo da referência (spruce/fir): 10 camadas de leques de lâminas finas e pontudas, com dobra no meio, caindo nas pontas, e uma segunda camada mais escura por baixo; tronco com base alargada e uma coroa de lâminas na ponta; topo das lâminas mais claro. Rascunhos internos ajustaram a queda (32 → 18 graus), a largura (copa de 5 m) e a densidade.
