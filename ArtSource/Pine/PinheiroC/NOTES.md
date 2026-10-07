# Pine / PinheiroC

## Briefing (aprovado em 2026-10-05, opção A)
- **O que é:** variação (PinheiroC) da âncora aprovada (PinheiroA v008 / RochaA v008), mesmo gerador e mesmos parâmetros de estilo; muda só forma e tamanho. Preparada para aprovação (Fase 3).
- **Tamanho:** ~9 m de altura, copa ~4 m, tronco 0,35 m na base, 1,5 m visível, levemente inclinado.
- **Forma:** 5 "saias" arredondadas e infladas, bordas onduladas e macias, ponta arredondada, copa cheia.
- **Triângulos:** LOD0 <= 1500, LOD1 <= 500, LOD2 impostor (2 tris), fundo: cartão-borrão (2 tris).
- **Cores:** pine_dark (base, interior) -> pine_light (topo), tronco bark. Uma cor por vértice, um material só.
- **Técnica:** opção A, saias sólidas sem transparência; normais "infladas" (puxadas para um elipsoide da copa).
- **Referências:** Docs/Reference/estetica_01_floresta e estetica_03_lago.
- **Gerador:** `ArtSource/Pine/pine_gen.py`. O impostor e o cartão-borrão vêm depois que a forma for aprovada.

## Versões

### v001
- Gerado com: `--style blades --tiers 16 --density 0.85 --top_light 0.75 --hue_var 1.0 --tip_round 0.22 --core_soft 1 --core_r 0.3 --core_light 0.5 --core_rings 6 --core_step 0.7 --h_jitter 0.28 --droop_deg 32.0 --curl 0.2792526803190927 --blade_width 0.14 --whorls 9 --seed 23 --height 11.0 --radius 2.6 --layers 5 --lean 0.07 --scallop 0.28 --droop 0.12 --inflate 0.4`
- Triângulos: LOD0 1956, LOD1 347
- Prévia: `PinheiroC_v001_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** primeira versão.

### v002
- Gerado com: `--style fronds --tiers 11 --density 1.0 --top_light 0.6 --hue_var 0.6 --tip_round 0.0 --core_soft 0 --core_r 0.35 --core_light 0.35 --core_rings 2 --core_step 1.0 --h_jitter 0.06 --droop_deg 32.0 --curl 0.3839724354387525 --blade_width 0.16 --whorls 9 --seed 23 --height 14.0 --radius 2.9 --layers 5 --lean 0.06 --scallop 0.28 --droop 0.12 --inflate 0.4 --fronds 12 --frond_leaves 6 --leaf_len 0.34 --leaf_width 0.24 --leaf_angle 38.0 --under_upto 0.95 --first 2.8 --c_dark #26442C --c_mid #44723A --c_light #86AE55 --c_bark None`
- Triângulos: LOD0 11094, LOD1 934
- Prévia: `PinheiroC_v002_preview.png`
- **Feedback do desenvolvedor (literal):** _(aguardando)_
- **O que mudou:** Novo estilo (fronds): andares de galhos com folhinhas pontudas, como os pinheiros da imagem de referencia da agencia.
