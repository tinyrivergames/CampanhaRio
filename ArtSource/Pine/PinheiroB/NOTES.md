# Pine / PinheiroB

## Briefing (aprovado em 2026-10-05, opção A)
- **O que é:** variação (PinheiroB) da âncora aprovada (PinheiroA v008 / RochaA v008), mesmo gerador e mesmos parâmetros de estilo; muda só forma e tamanho. Preparada para aprovação (Fase 3).
- **Tamanho:** ~9 m de altura, copa ~4 m, tronco 0,35 m na base, 1,5 m visível, levemente inclinado.
- **Forma:** 5 "saias" arredondadas e infladas, bordas onduladas e macias, ponta arredondada, copa cheia.
- **Triângulos:** LOD0 <= 1500, LOD1 <= 500, LOD2 impostor (2 tris), fundo: cartão-borrão (2 tris).
- **Cores:** pine_dark (base, interior) -> pine_light (topo), tronco bark. Uma cor por vértice, um material só.
- **Técnica:** opção A, saias sólidas sem transparência; normais "infladas" (puxadas para um elipsoide da copa).
- **Referências:** Docs/Reference/estetica_01_floresta e estetica_03_lago.
- **Gerador:** `ArtSource/Pine/pine_gen.py`. O impostor e o cartão-borrão vêm depois que a forma for aprovada.

## Versões

### v001
- Gerado com: `--style blades --tiers 11 --density 0.85 --top_light 0.75 --hue_var 1.0 --tip_round 0.22 --core_soft 1 --core_r 0.3 --core_light 0.5 --core_rings 6 --core_step 0.7 --h_jitter 0.28 --droop_deg 32.0 --curl 0.2792526803190927 --blade_width 0.14 --whorls 9 --seed 11 --height 6.5 --radius 2.0 --layers 5 --lean 0.04 --scallop 0.28 --droop 0.12 --inflate 0.4`
- Triângulos: LOD0 1406, LOD1 251
- Prévia: `PinheiroB_v001_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** primeira versão.
