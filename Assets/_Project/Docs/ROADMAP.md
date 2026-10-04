# Roadmap da campanha

As fases do `PLANO_CAMPANHA.md` (seção 6). **Princípio:** jogabilidade em caixa cinza primeiro, arte depois, em
paralelo. Cada fase termina com um relatório curto, e a próxima só começa quando o desenvolvedor disser.

| Fase | O quê | Pronto quando | Estado |
|---|---|---|---|
| **0. Fundação nova** | Projeto novo, Git + LFS. O que funciona do projeto antigo (caiaque, água e correnteza, rede, van). Pipeline do Blender (geradores, prévia, exportação, revisão). Shader SoftToon, céu, pós-processamento, ciclo do dia e cena LookDev. Save e carregamento por trechos | Um objeto feito no Blender aparece no Unity com a luz certa, pelo pipeline | ✅ **Concluída em 2026-10-03** |
| **1. Âncoras de estilo** | **Pinheiro**, **rocha** e **caiaque**, pelo ciclo de aprovação (briefing → v001 → prévia → ajustes → aprovado → Unity) | O desenvolvedor aprova os 3 no Blender e no jogo | Próxima (começa quando o desenvolvedor disser) |
| **2. Caixa cinza da Fase 1** | Partida (casa da avó / camping com o lago de treino), Estrada 1, evento da van (simples), Rio 1 e chegada, com formas simples, em trechos carregados aos poucos, jogável em grupo | Dá para jogar o prólogo inteiro com os amigos | |
| **3. Kit da floresta** | Variações de pinheiro, árvores de folha, arbustos, tufos, troncos, rochas, falésias modulares, montanhas de fundo; texturas do terreno, margens, seixos, o ajuste da água turquesa | A floresta inteira aprovada | |
| **4. Personagens** | Os 4 animais (conceito → MeshAI → retoques → limpeza, UVs, rig) e as animações | 4 personagens animados no jogo | |
| **5. Van e props** | A van nova (por dentro e por fora, com rack), remo, camping, placas, ponte, cercas | A van aprovada, por dentro e por fora | |
| **6. Montagem do prólogo** | Trocar a caixa cinza pela arte; a primeira cena do evento da van; a história (cartões-postais, rádio, mapa) | O prólogo bonito, do início ao fim | |
| **7. Sistemas da campanha** | Save do host, checkpoints, recompensas na van, tempo mínimo + ajuda escondida, amigo entrando no meio, tela de resultados do rio | Fechar, voltar e continuar de onde parou | |
| **8. 🧪 Teste com amigos** | "Prólogo + Rio 1" | A pergunta: **querem ver o que vem depois?** | |
| **9. Completar a Fase 1** | Rios 2 e 3, Estradas 2 e 3, eventos da van 2 e 3, o grande acampamento | A floresta completa | |

## O que a Fase 0 entregou (resumo)
- **Projeto:** `C:/Users/henri/CampanhaRio`, Unity 6000.5.10f1, URP 17.5, Git + LFS conferido (clone idêntico byte a byte).
- **Portado do CayaCozy:** caiaque (física idêntica: 95 de 97 números do FeelBenchmark iguais), água e correnteza, van e
  direção automática, rede (sessão, sincronia do caiaque, checagem de versão), ferramentas (autopiloto, bots, benchmark,
  capturas), animação procedural do remador (agora para qualquer personagem). Detalhes: `PORTED.md`.
- **Pipeline do Blender:** `ArtSource/art.cmd` (build / preview / review / export / compare), rig de luz compartilhado
  com o Unity, folha de prévia com a mesma cadeia de cor do jogo. Protocolo: `ART_PIPELINE.md`.
- **Visual:** shader SoftToon, céu em gradiente, ciclo do dia (tarde → hora dourada → pôr do sol → crepúsculo → noite),
  pós-processamento suave, cena LookDev com captura. Teste de comparação Blender x Unity: `TECH_DECISIONS.md`.
- **Campanha:** cena Core + trechos Test_A/B/C carregados e descarregados pelo host (os clientes seguem), save do host e
  perfil do jogador (`SAVE_MODEL.md`), a regra do rio (pôr do sol, metade do grupo, ajuda escondida) testada.
- **Pipeline provado com o seixo de teste** (`ArtSource/Test/Pebble/`).

## Pendências conhecidas (para as próximas fases)
- O terreno do KayakTest usa o shader de terreno do URP (faixas claras nas encostas): o terreno ganha um shader no estilo
  SoftToon na Fase 3.
- Aquecer os shaders no carregamento (o primeiro trecho, carregado junto com o boot, ainda tem um quadro de ~70 ms).
- A regra do rio roda no host; sincronizar o relógio e o resultado com os clientes vem com a caixa cinza do Rio 1 (Fase 2).
- O auto-driver da van ainda pensa em "acampamento / ponto de saída" (do jogo antigo): vira "o fim desta estrada" na Fase 2.
- O remador é o placeholder humano: os animais chegam na Fase 4 (o rig já aceita nomes de ossos e pernas opcionais).
