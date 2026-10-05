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
- **Feedback do desenvolvedor (literal):** "show, parece bem legal para uma floresta. As camadas ainda estao muito separadas, tem que ser mais algo entremeado. E o principal que eu acho que fica legal de adicionar, nao deixar somente uma cor dependente das sombras, da pra varias um pouquinho pouquinho as cores mais de dentro fora/folhas diferentes no pinheiro nao?"
- **O que mudou:** no estilo da referência (spruce/fir): 10 camadas de leques de lâminas finas e pontudas, com dobra no meio, caindo nas pontas, e uma segunda camada mais escura por baixo; tronco com base alargada e uma coroa de lâminas na ponta; topo das lâminas mais claro. Rascunhos internos ajustaram a queda (32 → 18 graus), a largura (copa de 5 m) e a densidade.

### v005
- Gerado com: `--style blades --tiers 14 --density 1.0 --top_light 0.75 --hue_var 1.0 --h_jitter 0.28 --droop_deg 18.0 --curl 0.2792526803190927 --blade_width 0.14 --whorls 9 --seed 4 --height 9.0 --radius 2.4 --layers 5 --lean 0.04 --scallop 0.28 --droop 0.12 --inflate 0.4`
- Triângulos: LOD0 1521, LOD1 317
- Prévia: `PinheiroA_v005_preview.png`
- **Feedback do desenvolvedor (literal):** "legal, para pinheiro fica legal essas pontas pontiagudas mesmo? ou seria interessante mudar? ficou bem bonito do que eu estou vendo aqui" → depois de um rascunho com pontas arredondadas (forte demais): "arredonda menos pra testar"
- **O que mudou:** camadas entremeadas (14 camadas mais próximas, cada lâmina com altura levemente diferente); cada lâmina com um tom próprio (umas mais amareladas, outras mais azuladas), o miolo mais frio e as pontas mais quentes, além da luz e da sombra. Mesmos 1.521 triângulos.

### v006
- Gerado com: `--style blades --tiers 14 --density 0.85 --top_light 0.75 --hue_var 1.0 --tip_round 0.22 --h_jitter 0.28 --droop_deg 18.0 --curl 0.2792526803190927 --blade_width 0.14 --whorls 9 --seed 4 --height 9.0 --radius 2.4 --layers 5 --lean 0.04 --scallop 0.28 --droop 0.12 --inflate 0.4`
- Triângulos: LOD0 1610, LOD1 297
- Prévia: `PinheiroA_v006_preview.png`
- **Feedback do desenvolvedor (literal):** "essa 006 ficou boa, o meio termo caiu bem. pode seguir."
- **O que mudou:** pontas das lâminas levemente arredondadas (uma ponta curta e cega em vez da agulha; `--tip_round 0.22`). LOD0 com 1.610 triângulos: 7% acima do teto de 1.500, aceito pela aparência aprovada.
- ✅ **APROVADA (2026-10-05): é o mestre da família Pine.** Próximo: exportar, impostor (LOD2), cartão-borrão do fundo, conferência no Unity.

## No jogo
- Exportado com impostor (LOD2, 4 tris), conferido no LookDev (tarde e pôr do sol, fileira de 12 a 260 m) e jogado no KayakTest com 450 cópias (tamanho 0,75–1,35x).
- **OK final do desenvolvedor (literal):** "show, ficaram bem no aspecto que eu queria."
- Pendente da família: o cartão-borrão do fundo; as variações de forma (Fase 3, pelo gerador).

### v007
- Gerado com: `--style blades --tiers 14 --density 0.85 --top_light 0.75 --hue_var 1.0 --tip_round 0.22 --core_soft 1 --core_r 0.3 --core_light 0.45 --h_jitter 0.28 --droop_deg 32.0 --curl 0.2792526803190927 --blade_width 0.14 --whorls 9 --seed 4 --height 9.0 --radius 2.4 --layers 5 --lean 0.04 --scallop 0.28 --droop 0.12 --inflate 0.4`
- Triângulos: LOD0 1640, LOD1 311
- Prévia: `PinheiroA_v007_preview.png`
- **Feedback do desenvolvedor (literal):** "ja melhorou, mas deixa um pouco ainda mais mesclado, como se o miolo fizesse mais parte da folhagem. Melhora um pouco mais ainda o que voce fez. Quantos triangulos esta essa arvore?"
- **O que mudou:** Miolo que mescla com a árvore: no lugar do cone liso quase preto, um miolo fino em forma de estrela (lê como folhagem interna), nos verdes das lâminas (mais escuro embaixo, mais claro em cima). Lâminas iguais à v006.

### v008
- Gerado com: `--style blades --tiers 14 --density 0.85 --top_light 0.75 --hue_var 1.0 --tip_round 0.22 --core_soft 1 --core_r 0.3 --core_light 0.5 --core_rings 6 --core_step 0.7 --h_jitter 0.28 --droop_deg 32.0 --curl 0.2792526803190927 --blade_width 0.14 --whorls 9 --seed 4 --height 9.0 --radius 2.4 --layers 5 --lean 0.04 --scallop 0.28 --droop 0.12 --inflate 0.4`
- Triângulos: LOD0 1736, LOD1 311
- Prévia: `PinheiroA_v008_preview.png`
- **Feedback do desenvolvedor (literal):** "beleza, parece bom. Seguimos" → **APROVADA** (substitui a v006 como pinheiro-mestre)
- **O que mudou:** Miolo ainda mais parte da folhagem: em degraus que acompanham as camadas (6 anéis, alternando largura), pontas claras e vãos escuros como as dobras das lâminas, e um tom próprio em cada ponta (como cada lâmina). Lâminas iguais à v006/v007.
