# Briefing completo para a nova sessão de planejamento

> **Para:** a nova sessão do Claude que vai ser o **planejador** deste jogo (em outra conta).
> **De:** a sessão de planejamento anterior (a conversa de 28/09 a 05/10/2026).
> **Objetivo:** você continuar exatamente de onde paramos, sem a conversa antiga. Leia tudo. Os arquivos citados existem no PC do desenvolvedor.

---

## 0. Resumo em 30 segundos

- **O jogo:** um jogo cooperativo para **4 amigos**, com visual **estilizado e macio**, em que **personagens animais** reabrem a **agência de rio da Vó Nina**. Depois de uma tempestade, o rio é o único caminho do vale, e eles **levam passageiros malucos e encomendas impossíveis rio abaixo, de caiaque**.
- **A variedade vem de "Descida = Rio × Trabalho":** o tipo de encomenda ou de passageiro muda a regra da descida.
- **O limite de tempo é o pôr do sol.**
- **A recompensa é ver a agência e os vilarejos do vale crescendo.**
- **O projeto novo** está em `C:/Users/henri/CampanhaRio` (Unity 6000.5.10f1, URP). **A Fase 0 (fundação) está pronta.** O próximo passo é a **Fase 1: a arte no Blender, elemento por elemento, começando pelo pinheiro**, com o desenvolvedor aprovando cada versão.
- **O seu papel:** planejar com o desenvolvedor, escrever os prompts das etapas para o "engenheiro" (outra sessão do Claude Code, no projeto) e revisar os resultados.

---

## 1. O desenvolvedor

- **Henrique**, brasileiro, desenvolvedor solo. Tem um amigo que ajuda com ideias, arte (MeshAI), som e testes, **sem usar o Claude**. Testa com amigos.
- **Fale com ele em português.** Os prompts para o engenheiro vão em **inglês**.
- **Ele pediu explicitamente: "não quero que você concorde com tudo, fale a verdade".** Aponte riscos, discorde quando for preciso, e justifique.
- Ele muda de ideia com frequência enquanto explora. É saudável num protótipo, mas **ajude a travar as decisões** e registre-as nos documentos.
- **A arte é muito importante para ele:** "sem problemas em demorar os gráficos, quero algo bem feito".
- Gosta de respostas organizadas (tabelas, listas) e diretas.
- Ele bateu o **limite semanal de uso** na conta anterior. Por isso a troca de conta. Etapas automáticas longas (builds, testes com várias cópias do jogo) custam muito: prefira passos menores.

---

## 2. Como trabalhamos (funcionou bem, mantenha)

**Dois papéis:**
- **O planejador (você):** conversa com o Henrique, decide junto, escreve cada etapa como um **prompt em `.md`**, entrega ao engenheiro, revisa o resultado e dá o retorno ao Henrique **no fim de cada etapa**, em português.
- **O engenheiro:** uma sessão do Claude Code aberta em `C:/Users/henri/CampanhaRio` (na conta antiga, que ainda tem uso). Ele executa.

**O formato dos prompts** (veja os exemplos em `C:/Users/henri/CayaCozy 1.0/Assets/_Game/Docs/Prompts/`):
contexto e o que ler primeiro → objetivo → regras de processo → Partes (A, B, C…) com verificação → fora do escopo → relatório final curto → parar.

**Regras que sempre colocamos nos prompts:**
- Um plano curto antes de cada parte, **e um commit por parte**.
- **Verificar com gráficos de verdade:** uma build de release + prints + leitura do Player.log. **Nunca confiar em testes `-nographics` para algo visual** (um bug de tela azul passou assim).
- Arquivos binários do Unity no **Git LFS** (um `.gitattributes` errado já corrompeu um terreno).
- Ser honesto no relatório sobre o que ficou fraco.

**A entrega dos prompts:** salve em `C:/Users/henri/CampanhaRio/Assets/_Project/Docs/Prompts/` (crie a pasta). Se as mensagens entre sessões funcionarem (liste as sessões locais), mande por lá e peça o aviso de quando terminar. Senão, passe ao Henrique a frase para colar no engenheiro: `Leia e execute Assets/_Project/Docs/Prompts/<arquivo>.md`.

**A revisão:** leia o `git log`, o `git status`, os **prints (abra os PNGs)**, os logs e os documentos. Depois diga ao Henrique o que foi feito, o que você viu e se há problemas. Exemplo de boa revisão: na Fase 0 notamos que, ao pôr do sol, os objetos ficavam sem sombra (e depois com a sombra "descolada", o peter-panning). O engenheiro corrigiu com bias baixo + oclusão de contato suave.

**A arte (Fase 1 em diante) é interativa:** o Henrique fala **direto com o engenheiro**, que cria uma versão no Blender, **abre a folha de prévia e o `.blend` na tela dele** e espera o comentário. Você ajuda com briefings, revisa prévias quando ele pedir e planeja o resto.

---

## 3. A história do projeto (por que estamos aqui)

**O projeto antigo: CayaCozy** (`C:/Users/henri/CayaCozy 1.0`, GitHub `tinyrivergames/CayaCozy---Game`). Agora é só laboratório, sem desenvolvimento.

| Fase | O que aconteceu | O que aprendemos |
|---|---|---|
| Protótipo cozy | Um caiaque levado pela correnteza, controle lateral, remanso | O movimento base funcionou |
| Visual de aquarela | Shader pintado, filtro de aquarela, água estilizada | Bonito, mas depois foi trocado pelo estilo "macio" |
| Rio vivo | Campo de fluxo (inclinação, curvas, remoinhos), degraus e ondas naturais, troncos | **Reaproveitado** no projeto novo |
| Caiaque físico | Rigidbody, flutuação por pontos, capotagem e rolamento esquimó | Ficou "**zero diversão, só descer o rio**" |
| "Encontrar a diversão" | Controle instantâneo, velocidade, manobras que dão impulso, pista curta, medalhas, fantasma | **Mais rápido = muito mais divertido.** O Henrique aprovou |
| Multiplayer | NGO, IP direto/Tailscale, cada um manda no próprio caiaque | **Testado com amigos: "muito promissor"** |
| Polimento | Bugs, menu em português; tirar a "cara de Mario Kart" (sem botão de turbo, coletáveis naturais, HUD discreto) | **Evitar o visual de arcade e kart** |
| A pé + acampamento, a van | Andar, carregar caiaque, chamar a descida; a van com estrada, rack e rádio | A van deu a ideia da campanha |

**A virada para a campanha (03 a 04/10)**, as ideias e por que foram aceitas ou rejeitadas:
1. **"Corrida em pistas com amigos"** (Sledding Game): funcionou, mas **faltava motivo para continuar jogando**.
2. **"Viagem de van: a van se perde e vocês descem o rio para recuperá-la":** **rejeitada pelo Henrique**, por ser repetitiva ("de novo isso?").
3. **"Mudar a mecânica a cada fase, como It Takes Two":** analisada. **Minha recomendação, aceita:** não copiar o modelo (o custo é enorme, a rejogabilidade baixa, a identidade se dilui). Em vez disso, **"um núcleo excelente + variações"**.
4. **"Por que descer os rios?"** A ideia de "a Vó pediu" é **fraca como motivação**. Discutimos 7 premissas (acampamento da Vó, correio do rio, vale desbotado, agência de aventura, festival, cartógrafos, série de vídeos). **O Henrique escolheu juntar o Correio do Rio com a Agência de Aventura** → **a Agência do Rio**.

---

## 4. O jogo atual: "Agência do Rio" (o plano v3)

**O documento completo:** `C:/Users/henri/CampanhaRio/Assets/_Project/Docs/PLANO_CAMPANHA.md` (em português). **É a fonte da verdade.**

### A frase
> *Depois da tempestade, o rio virou o único caminho do vale. Quatro amigos reabrem a velha agência da Vó, levando passageiros malucos e encomendas impossíveis rio abaixo, até trazer o vale inteiro de volta à vida.*

### Os pilares
1. **Cada descida é uma surpresa:** "Rio × Trabalho".
2. **Juntos:** 4 amigos, com o progresso do host.
3. **O rio é o desafio:** chegar antes do pôr do sol, com a carga inteira e o passageiro feliz. **Bater o tempo da Vó** é a maestria.
4. **Ver o mundo melhorar:** a agência cresce e os vilarejos revivem.
5. **Bonito e macio.**

### Os personagens e o mundo
- **Os 4 amigos são animais**, no espírito de *Ultimate Chicken Horse*. O Henrique cria no MeshAI e o engenheiro limpa, rigga e integra.
- **A Vó Nina:** a lendária barqueira do vale, fundadora da agência. Está presente pela **caderneta** (anotações e fotos dela) e pelos **tempos dela entalhados** nas chegadas.
- **O Seu Alce:** amigo da Vó, que leva a van até o fim de cada rio. **Isso se explica uma vez só**, e cada chegada tem uma piada diferente.
- **Os moradores** (bichos do vale) fazem os pedidos e mostram os vilarejos revivendo.

### A variedade: Descida = Rio × Trabalho
- **O rio** = o lugar (corredeiras, degraus, remoinhos e um **momento marcante**: cachoeira, caverna, tronco oco…).
- **O trabalho** = a regra, implementada como um **modificador sobre a mesma física do caiaque**.
  - **Encomendas:** frágil, sorvete (derrete), galinhas (se espalham se capotar), lanterna (respingo apaga, ilumina à noite), balões (vento), piano (só no bote de 4), carga dividida, cartas soltas.
  - **Passageiros:** bode medroso (medidor de medo, pula fora), filhote radical (quer manobras), coruja fotógrafa (paradas), urso pesadão (raspa no raso), noivos (o bote não pode virar), pescador, cachorro agitado.
- **Condições do rio:** noite, neblina, cheia, caverna, só de boia.
- **Barcos (progressão):** caiaque → canoa dupla → bote de 4 → boia.

### O ciclo
Quadro de pedidos → escolher o trabalho e o rio no mapa do vale → van até o rio → descer → chegada antes do pôr do sol (a van já está lá) → avaliação ⭐ (pôr do sol + carga ou passageiro + estilo/tempo da Vó) → dinheiro e reputação → melhorar a agência, a van e os barcos → novos pedidos e rios.
- **Concluir** = pelo menos **metade do grupo** chega antes do pôr do sol. **Se falhar,** anoitece, a tela escurece, e volta-se à largada. Depois de 3 ou 4 falhas, o tempo afrouxa sem ninguém perceber.
- **O progresso é do host.** As roupas e os recordes são de cada jogador.

### A primeira região: o Vale da Floresta
- **O prólogo:** a tempestade, o galpão da Vó, a caderneta, a van, o Seu Alce, o lago de treino.
- **Os trabalhos:**
  1. Rio do Moinho × carta urgente;
  2. Rio das Pedras × encomenda frágil;
  3. Rio do Moinho × bode medroso;
  4. Rio da Garganta × sorvete;
  5. o especial: bote de 4 × o piano.
- **O vertical slice (a primeira versão jogável):** o prólogo + **2 trabalhos no mesmo rio**, para provar "o mesmo rio, outra experiência".

### O mundo e o desempenho
Uma cena núcleo + áreas carregadas aos poucos (carregamento aditivo, com o host decidindo). Cenários de fundo leves dão a sensação de mundo aberto. A meta é 60 FPS a 1080p numa GTX 1660 Super com 4 jogadores.

---

## 5. A arte: as regras do Henrique

- **A estética** é inspirada em *RV There Yet?*: estilizada, formas macias e arredondadas, pinheiros robustos, rochas e falésias laranja e lisas, água turquesa transparente, luz quente e sombras suaves. **Só a estética:** nada de HUD, van ou personagens desse jogo. As referências estão em `Assets/_Project/Docs/Reference/`.
- **Tudo é feito no Blender, um elemento por vez.** A cada versão, o engenheiro **abre a folha de prévia e o `.blend`** para o Henrique aprovar. O ciclo: briefing → v1 → prévia → ajustes → … → aprovado → Unity → conferência → variações da família. Os detalhes técnicos estão em `ART_PIPELINE.md`.
- **Árvores levíssimas** (o mínimo de triângulos), e **as do fundo são quase "borrões"** (silhuetas e impostores). Não é mundo aberto, então não precisa de milhares de árvores detalhadas.
- **O pôr do sol é o relógio de cada descida.** Os elementos precisam ficar bonitos na hora dourada e no pôr do sol (dá para ver a prévia em cada horário).
- **A ordem:** âncoras (**pinheiro → rocha → caiaque**) → natureza → terreno e água → personagens/passageiros → barcos, van e cargas → lugares (a agência em estágios, os vilarejos antes e depois).
- **Os personagens são o ponto fraco do Claude.** O caminho é conceito → MeshAI → retoques → limpeza, UV e rig pelo engenheiro.
- O Blender 5.2 está instalado. O controle é por **scripts Python**. **Não usamos Blender MCP** (opcional, de terceiros, não é necessário).

---

## 6. Onde está cada coisa

| O quê | Onde |
|---|---|
| **O projeto novo** | `C:/Users/henri/CampanhaRio` (Git local, **sem remoto ainda**; o Henrique vai criar no GitHub) |
| **O plano do jogo (fonte da verdade)** | `Assets/_Project/Docs/PLANO_CAMPANHA.md` |
| A passagem técnica (em inglês) | `Assets/_Project/Docs/HANDOFF.md` |
| Este briefing | `Assets/_Project/Docs/BRIEFING_PLANEJADOR.md` |
| O roadmap | `Assets/_Project/Docs/ROADMAP.md` (**desatualizado**: ainda é o plano v2, e precisa seguir a seção 8 do PLANO v3) |
| O processo de arte, o estilo, a técnica | `ART_PIPELINE.md`, `STYLE_GUIDE.md`, `TECH_DECISIONS.md`, `PORTED.md`, `SAVE_MODEL.md` (em `Assets/_Project/Docs/`) |
| As referências visuais | `Assets/_Project/Docs/Reference/` |
| O pipeline do Blender | `ArtSource/pipeline/` (com o `.blend` e as prévias em `ArtSource/<Família>/<Asset>/`) |
| O Blender | `C:/Program Files/Blender Foundation/Blender 5.2/blender.exe` |
| O projeto antigo (laboratório) | `C:/Users/henri/CayaCozy 1.0` (os prompts antigos em `Assets/_Game/Docs/Prompts/`, o roadmap antigo e o histórico) |

---

## 7. O estado atual (05/10/2026)

- **Fase 0 ✅ (03/10):**
  - o projeto novo, com Git e LFS;
  - os sistemas portados (caiaque, água, van, rede, bots, benchmark): a sensação do caiaque ficou igual à do projeto antigo;
  - o pipeline do Blender com a folha de prévia e o "abrir para revisão";
  - o shader macio, o ciclo do dia (tarde → hora dourada → pôr do sol → crepúsculo) e a cena de conferência;
  - as áreas carregadas aos poucos (testado com 2 jogadores);
  - o save do host e a regra do pôr do sol;
  - as sombras ao pôr do sol corrigidas e aprovadas.
- **Ainda sem commit:** o `PLANO_CAMPANHA.md` v3, o `HANDOFF.md` e este briefing. Peça ao engenheiro para fazer o commit e **atualizar o `ROADMAP.md` para o v3**.

## 8. Os próximos passos

1. **Fase 1, interativa:** o engenheiro propõe o **briefing do pinheiro** (tamanho, triângulos por LOD, cores, técnica das folhas), o Henrique confirma, e começa o ciclo de versões. Depois vêm a rocha e o caiaque.
2. **Fase 2, caixa cinza do vertical slice** (pode ser em paralelo, se o uso permitir):
   - o hub da agência, uma estrada curta, o Rio do Moinho, a chegada;
   - **o sistema de trabalhos** (o quadro, os modificadores de carga e passageiro, a avaliação ⭐);
   - **2 trabalhos no mesmo rio** (a carta urgente e o bode medroso);
   - um Seu Alce simples.

   O prompt ainda **não foi escrito**: é o seu primeiro trabalho de planejamento, quando o Henrique pedir.
3. As fases seguintes estão na seção 8 do PLANO.

## 9. As decisões em aberto (pergunte quando for relevante, não decida por ele)
- O **nome do jogo** (o de trabalho é "CampanhaRio").
- **Que bicho é cada um:** os 4 amigos, a Vó Nina e o Seu Alce.
- **O final da campanha:** a Vó volta para a festa? Uma última carta? Um rio secreto?
- O **nome da agência** ("Expresso do Rio" é provisório).

## 10. Os cuidados que eu manteria
- **A arte vai consumir semanas** de rodadas de aprovação. Isso é ok para ele, mas organize bem: âncoras primeiro, depois as famílias geradas a partir do mestre aprovado.
- **Caixa cinza antes de arte:** prove que os trabalhos são divertidos (Fase 2 + teste com amigos) antes de produzir arte em massa.
- **A parte de negócio da agência precisa ficar leve,** sem virar jogo de gerenciamento.
- **Os trabalhos não podem virar "missões de entrega" chatas:** variados, engraçados e nunca repetidos à força.
- **Evitar o visual de arcade e kart** (uma lição do teste com amigos).
- **Desempenho das florestas:** LOD e impostores desde o primeiro pinheiro.
