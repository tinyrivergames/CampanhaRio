# Roadmap: "Agência do Rio"

As fases do `PLANO_CAMPANHA.md` v3 (seção 8). **Princípio:** jogabilidade em caixa cinza primeiro, arte depois, em
paralelo. Cada fase termina com um relatório curto, e a próxima só começa quando o desenvolvedor disser.

| Fase | O quê | Pronto quando | Estado |
|---|---|---|---|
| **0. Fundação nova** | Projeto, sistemas portados, pipeline do Blender, shader macio, ciclo do dia, trechos carregados aos poucos, save, regra do pôr do sol | Um objeto feito no Blender aparece no Unity com a luz certa | ✅ **2026-10-03** |
| **1. Âncoras de estilo** | **Pinheiro → rocha → caiaque**, pelo ciclo de aprovação (`ART_PIPELINE.md`), vistos também na hora dourada e no pôr do sol | O desenvolvedor aprova os 3 | ✅ **2026-10-05** |
| **2. Caixa cinza do vertical slice** | Agência (hub), estrada curta, Rio do Moinho, chegada; **o sistema de trabalhos** (quadro de pedidos, modificadores de carga e passageiro, avaliação ⭐); **2 trabalhos no mesmo rio** (carta urgente, bode medroso); Seu Alce simples | Dá para jogar os 2 trabalhos com os amigos, e o mesmo rio parece diferente | ✅ **2026-10-06** (falta o teste do desenvolvedor com os amigos) |
| **3. Kit da floresta** | Lotes 2 e 3: variações de pinheiro, árvores de folha, arbustos, tufos, troncos, rochas, falésias, montanhas de fundo; terreno, margens, seixos, a água turquesa | A floresta aprovada | |
| **4. Personagens e passageiros** | Os 4 amigos + os passageiros (bode, coruja, urso…): MeshAI → limpeza, rig e integração | Os 4 amigos + o bode animados no jogo | |
| **5. Barcos, van e cargas** | Canoa, bote, boia, a van, remos, as encomendas (caixas, ovos, galinhas, piano…) | Aprovados | |
| **6. Montagem do vertical slice** | Arte no lugar da caixa cinza; o prólogo (tempestade, galpão, caderneta); a primeira chegada do Seu Alce; a agência crescendo um estágio | Bonito do início ao fim | |
| **7. Sistemas da campanha** | Save do host, reputação e dinheiro, melhorias da agência e da van, o mapa revelando o vale, amigo entrando no meio | Fechar, voltar e continuar | **Em andamento** (save do host e "fechar e voltar" ✅, entrar no meio ✅, melhorias ✅ rascunho, mapa do vale ✅ rascunho; a lista final de melhorias e lugares espera aprovação) |
| **8. 🧪 Teste com amigos** | O vertical slice | **Querem o próximo pedido?** O mesmo rio com outro trabalho pareceu novo? | |
| **9. Completar o Vale da Floresta** | Trabalhos 2 a 4, o especial do bote, os vilarejos, a festa | A região completa | |

## O que a Fase 0 entregou
Projeto e Git/LFS conferidos; caiaque, água, van, rede e ferramentas portados (física idêntica no FeelBenchmark); pipeline
do Blender (`art build | preview | review | export | compare | look`); shader SoftToon, céu, ciclo do dia, LookDev; Core +
trechos carregados pelo host; save do host e perfil do jogador; a regra do pôr do sol. Sombras com sol baixo corrigidas
(bias baixo + oclusão de contato). Detalhes: `PORTED.md`, `TECH_DECISIONS.md`, `SAVE_MODEL.md`.

## O que a Fase 1 entregou
As 3 âncoras aprovadas pelo ciclo de versões (notas literais em `ArtSource/<Família>/<Asset>/NOTES.md`):
- **PinheiroA v008:** lâminas em camadas (referência spruce/fir), pontas levemente arredondadas, miolo em estrela nos verdes
  das lâminas; LOD0 1.736 tris (16% acima do orçamento, aceito), impostor de 4 tris ao longe.
- **RochaA v008:** pedra de rio de floresta cinza-quente, planos com luz chapada e arestas claras finas, musgo escuro no topo,
  manchas de ferrugem; LOD por asset (detalhe até ~60 m).
- **CaiaqueA v008:** baseado na foto low-poly do desenvolvedor: vinho, facetado em triângulos, borda do cockpit grossa,
  assento marrom, cabos em X (a carga vai no X de trás), alças em arco; cockpit por corte exato (casco fechado); a água
  não aparece sobre o casco nem dentro do barco (stencil). Remo provisório em vinho e marrom.
Também: câmera um pouco mais afastada (4,6 m, 16°); floresta e pedras de teste no KayakTest; repositório no GitHub
(`tinyrivergames/CampanhaRio`, privado).

## O que a Fase 2 entregou (2026-10-06)
O vertical slice inteiro em caixa cinza, jogável em grupo: a agência com o quadro de pedidos, a van do Seu Alce pela
estrada, o Rio do Moinho (o rio de teste) com a largada e a Vila do Moinho na chegada, e o ciclo completo (pedido →
van → descida → avaliação ⭐ → vila → de volta). Os 2 pedidos: a carta urgente (não pode molhar) e o bode medroso (o
medo enche; ele pula e alguém busca). Tudo sincronizado (pedido, relógio, HUD, resultado, céu, bode). Testes:
`-cc-script jobrun | jobrules | slice` (host + clientes + bots). Fase 7 adiantada: entrar no meio, melhorias, mapa do
vale e "fechar e voltar" (`-cc-script upgrades | map | resume`). O que precisa do desenvolvedor: `PENDENTES.md`.

## Fase 2: o roteiro (uma parte por commit; tudo em caixa cinza, verificado em build de release)
- **A. Sistema de trabalhos:** o pedido (rio + trabalho + prazo + pagamento), o quadro de pedidos (2 a 4, o
  host escolhe), o modificador em cima da regra do pôr do sol, a avaliação (⭐ pôr do sol, ⭐ carga/passageiro, ⭐ tempo), o save. ✅ (a sincronização com os clientes vai para a parte D, junto com o rio em rede)
- **B. Os 2 trabalhos:** **carta urgente** (a regra básica: prazo apertado, a carta não pode molhar demais) e **bode
  medroso** (o medo enche com velocidade e pulos; cheio, ele pula na água e alguém tem que buscá-lo). ✅
- **C. Os lugares:** `Agencia` (o hub com o quadro e a van), `Estrada_Vale` (curta), `Rio_Moinho` (corredeiras, remansos e
  a primeira cachoeirinha) e a chegada, como cenas carregadas aos poucos. ✅ (o Rio do Moinho reaproveita o rio do KayakTest)
- **D. O ciclo inteiro:** agência → quadro → van → largada → descida → chegada → avaliação → de volta à agência. ✅ (host + cliente, os 2 pedidos; `-cc-script slice`)
- **E. O Seu Alce simples:** um boneco cinza na chegada, com uma piada diferente a cada chegada. ✅
- **F. Teste em grupo:** host + clientes (e bots) jogando os 2 trabalhos; o mesmo rio tem que parecer diferente. ✅ (host + 2 clientes + 1 bot, os 2 pedidos, `-cc-script slice -cc-players 3 -cc-bots 1`; o "parece diferente" é o teste do desenvolvedor)

## Pendências conhecidas
- O terreno do KayakTest usa o shader de terreno do URP: ganha um shader no estilo SoftToon na Fase 3.
- Aquecer os shaders no boot (o primeiro trecho ainda tem um quadro de ~70 ms).
- A regra do rio roda só no host: sincronizar o relógio e a avaliação com os clientes vem na Fase 2.
- Os horários do dia no Blender (`review_scene.py`) copiam os do `DayCycle.cs`: mover a tabela para o `lookdev_rig.json`.
- O remador ainda é o placeholder humano (os animais vêm na Fase 4; o rig já aceita outros ossos).
- Antes do lançamento: o cache de pipelines do D3D12 dos jogadores pode corromper entre atualizações (o crash ao fechar de
  2026-10-06, `TECH_DECISIONS.md`); ver as opções do Unity para esse cache.
- O remo é provisório (caixas): o remo definitivo vem na Fase 5.
- Pinheiro LOD0 acima do orçamento (1.736 / 1.500): dá para tirar lâminas escondidas sem mudar o visual.
