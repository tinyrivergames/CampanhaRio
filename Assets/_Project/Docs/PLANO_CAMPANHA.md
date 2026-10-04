# CayaCozy: Plano da Campanha (v2)

> Escrito em 2026-10-03. Substitui a direção "corrida em pistas" do ROADMAP.md (Etapas 8–12).
> **Casa deste documento:** o repositório CampanhaRio (`Assets/_Project/Docs/`), desde a Fase 0. A cópia no projeto antigo (CayaCozy) fica congelada.
> **Foco:** a campanha em fases e os **gráficos feitos elemento por elemento no Blender**, com aprovação do desenvolvedor a cada versão.

---

## 1. A visão em uma frase

**Quatro amigos numa viagem de van pela natureza. Sempre que a estrada acaba, o único caminho é o rio, e eles descem juntos de caiaque.**

### Pilares
1. **A jornada:** sempre existe um "depois". Cada rio vencido abre a próxima parte do caminho.
2. **Juntos:** feito para 4 amigos. Todos descem de caiaque, e o progresso é do host.
3. **O rio é o desafio:** terminar a descida dentro do tempo mínimo para avançar. As medalhas dão conquistas.
4. **A van é a casa e o diário:** adesivos, troféus e lembranças contam a viagem de vocês.
5. **Bonito e macio:** estilizado, formas suaves, cores vivas e naturais, luz quente (referência: *RV There Yet?*).

### O que NÃO é
- Não é um jogo sobre dirigir: a van leva a história, o rio é o coração.
- Não é mundo aberto: é uma sequência de trechos que **parece** aberta.
- Não tem aspecto de Roblox (lobby de menus, plataformas flutuando, cores de plástico).
- Não pune o grupo: falhar significa tentar de novo rápido, e nunca perder progresso.

---

## 2. A história (leve, contada pelo mundo)

A história serve de **motivo para seguir em frente**, e não é uma novela. Ela é contada por cartões-postais, o rádio, bilhetes e o mapa. Sem cenas longas.

**Premissas possíveis** (a escolher):
- **A) O mapa da avó (sugerida):** os 4 amigos acham, na casa da avó de um deles, um mapa antigo marcando um lugar especial onde o rio encontra o mar, com o recado *"sigam o rio"*. Eles pegam a velha van da família e partem.
- **B) O Festival do Mar:** uma banda de 4 amigos precisa chegar ao festival na costa, mas a estrada principal caiu. O único caminho é seguir o rio.
- **C) A garrafa:** os amigos encontram uma mensagem numa garrafa que desceu o rio, e decidem subir até a origem e depois descer até o fim para entregá-la.

**Os 4 personagens:** silhuetas bem diferentes, uma personalidade simples para cada (o planejador, o medroso, o aventureiro, o desligado) e roupas trocáveis depois. *Decisão pendente: humanos estilizados ou animais?*

---

## 3. A estrutura da campanha

### 3.1 Fluxo da Fase 1 (Floresta). É o escopo da primeira versão

```
[Ponto de partida] → [Estrada 1] → [Evento da van #1] → [Rio 1] → [Chegada / acampamento]
```

1. **Ponto de partida (o "lobby" físico):** a casa da avó na beira da floresta, ou um pequeno posto e camping. Todos nascem aqui a pé. Tem a van estacionada, **um lago calmo para treinar o caiaque** (o tutorial sem parecer tutorial), o mapa na parede e o primeiro cartão-postal. Quando o host está pronto, todos entram na van.
2. **Estrada 1 (de 3 a 5 min), desafiadora e exploratória:** estrada de terra pela floresta, com pequenos desafios em grupo. Por exemplo: um tronco caído que precisa de todos para empurrar, uma subida com lama em que alguém desce e empurra, uma ponte estreita, um desvio opcional que leva a um mirante ou a um adesivo escondido. **Termina num ponto em que a estrada acaba** (ponte caída, deslizamento), e o rio está logo abaixo.
3. **Evento da van #1 (de 10 a 20 s, engraçado, pulável depois da primeira vez):** a van precisa chegar ao fim do rio. **Cada rio tem um jeito diferente**:
   - Rio 1: um personagem local (um alce guincheiro?) engancha a van e desce por uma trilha de madeireiros fazendo barulho.
   - Ideias para os próximos: balsa no canal ao lado, penhasco + paraquedas, teleférico de carga, balão, uma tirolesa gigante, um castor que constrói uma rampa.
4. **Rio 1 (de 90 a 150 s):** a descida em grupo, com um **motivo para o tempo** dentro do mundo (o sol se pondo, a balsa partindo, a comporta fechando).
   - **Para avançar:** pelo menos **metade do grupo** chega dentro do tempo mínimo.
   - **Falhou:** volta rápido à largada do rio, sem repetir a estrada nem o evento. Depois de 3 ou 4 tentativas, o tempo afrouxa um pouco sem ninguém perceber.
   - **Medalhas** (bronze, prata, ouro) e **segredos no rio** dão **adesivos e troféus para a van**.
5. **Chegada:** a van está esperando (com a piada do evento), o grupo acampa, ganha o cartão-postal seguinte, e o host decide continuar. Fica salvo como checkpoint.

### 3.2 A Fase 1 completa (depois da primeira versão)
3 rios e 3 estradas na floresta, ficando mais difíceis, terminando num **grande acampamento de fim de fase**. Daí a próxima região (cânion, outono, pântano, neve) segue o mesmo molde.

### 3.3 Progresso e grupo
- **O progresso é do host:** região, checkpoint, rios vencidos, os adesivos e troféus da van.
- **Cada jogador guarda** as suas medalhas, recordes e roupas.
- **Amigo entrando no meio:** nasce no checkpoint atual, junto do grupo.
- **Recompensas:** adesivos na lataria, troféus na prateleira, enfeites no painel, uma rádio nova, roupas e cores de caiaque. **Só visual**, sem deixar ninguém mais rápido.

---

## 4. O mundo carregado por trechos (desempenho)

Você está certo: carregar tudo de uma vez seria pesado. A decisão:

- **Uma cena "núcleo" sempre carregada:** jogadores, van, rede, áudio, interface e o save.
- **Cada trecho é uma cena separada:** `Partida`, `Estrada_1`, `Rio_1`, `Chegada_1`… São carregadas por **carregamento aditivo**: o próximo trecho carrega enquanto o grupo ainda está no atual, e o trecho anterior é descarregado quando ninguém está mais nele.
- **O host decide o que carregar**, e todos os clientes carregam junto (o Netcode sincroniza as cenas).
- **Sensação de mundo aberto:** cada trecho tem um **cenário de fundo leve** (montanhas e florestas distantes, em baixo detalhe) que mostra o caminho já percorrido e o que vem pela frente, sem carregar nada de verdade.
- **Orçamento:** 60 FPS em 1080p numa GTX 1660 Super, com 4 jogadores. Florestas densas exigem LODs e "impostores" (árvores distantes viram imagens) desde o primeiro asset.

---

## 5. Os gráficos: elemento por elemento, no Blender

### 5.1 A estética-alvo (das imagens de referência)
- **Estilizado e macio:** formas arredondadas e "infladas", nada pontiagudo nem realista.
- **Pinheiros robustos** com galhos em camadas e copa cheia, verde médio com variação suave.
- **Falésias e rochas laranja/areia**, lisas, com grandes planos e gradiente suave. Pouco detalhe de textura.
- **Água turquesa limpa e transparente**, com ondinhas suaves e o fundo de areia visível na beira.
- **Grama e flores em tufos**, terra batida, céu azul com nuvens macias.
- **Luz quente do sol, sombras suaves**, sem contornos pretos.
- **Personagens "chibi":** cabeça grande, formas simples, rosto expressivo.

**Isso substitui o visual de aquarela atual:** sai o filtro de aquarela (papel, bordas de pigmento) e entra um **shader "toon macio"** no Unity (luz que envolve as formas, sombras suaves e coloridas, um leve brilho na borda) com correção de cor e névoa de distância.

### 5.2 O ciclo de cada elemento (o processo que você pediu)

```
Briefing → Blender (v1) → Prévia aberta para você → Seus ajustes → v2, v3… → Aprovado → Unity → Conferência no jogo → Final
```

1. **Briefing:** referência, tamanho real, limite de polígonos, LODs, material e cores. O Claude escreve, e você confirma.
2. **Criação no Blender:** o Claude cria o elemento **por scripts Python** no Blender. Cada família (pinheiro, rocha…) ganha um **gerador com parâmetros**, para gerar variações rápido depois que o "mestre" for aprovado. Os scripts e os `.blend` ficam no repositório (`Art/Blender/`).
3. **Prévia para você (a cada versão):**
   - uma **folha de prévia** (PNG) com 4 ângulos, um close, a silhueta e o elemento **ao lado do personagem para dar escala**, renderizada com a mesma luz do jogo. **O Claude abre essa imagem na sua tela;**
   - e **abre o próprio `.blend` no Blender**, para você girar e olhar à vontade.
4. **Você diz o que ajustar** ("copa mais cheia", "laranja mais quente", "menos detalhe na base"). O Claude faz a v2, e assim por diante. As versões ficam guardadas (`Pinheiro_A_v03.blend` + a prévia).
5. **Aprovado:** o Claude exporta (FBX + texturas) e integra no Unity, coloca na **cena de conferência** (a mesma luz do jogo) e mostra um print. **Você dá o OK final.**
6. **Variações:** com o mestre aprovado, o gerador cria a família (5 pinheiros, 6 rochas…), e você aprova o conjunto de uma vez.

*Opcional:* o complemento comunitário "Blender MCP" deixa o Claude mexer no seu Blender aberto, ao vivo. Não é necessário, porque scripts + prévias + abrir o arquivo já cobrem o ciclo.

### 5.3 Ordem dos elementos (do que define o estilo para o que depende dele)

| Lote | Elementos | Por quê |
|---|---|---|
| **0. Fundação** | Shader toon macio no Unity, a cena de conferência, a luz-padrão, o modelo de prévia no Blender, a paleta, a escala (1 un = 1 m, altura dos personagens) | Para tudo combinar entre si |
| **1. Âncoras de estilo** | **Pinheiro**, **rocha**, **caiaque** | Aprovados esses três, o estilo está definido |
| **2. Natureza** | Variações de pinheiro, árvores de folha, arbustos, tufos de grama e flores, troncos e galhos, rochas, **falésias modulares**, **montanhas de fundo** | O grosso da floresta |
| **3. Terreno e água** | Texturas do terreno (grama, terra, areia, rocha), margens, seixos, o ajuste da água turquesa | O chão do mundo |
| **4. Personagens** | Os 4 personagens + animações | Veja o aviso abaixo |
| **5. Van e props** | **A van** (por dentro e por fora, com rack), remo, coisas de acampamento, placas, ponte, cercas | A "casa" da viagem |
| **6. Lugares** | Ponto de partida (casa da avó/camping), os sets da Estrada 1 e do Rio 1 | Montados com os lotes acima |

⚠️ **Personagens são onde o Claude é mais fraco.** O caminho que funciona: **conceito** (desenho seu, do seu amigo ou de uma IA de imagem) → **base no MeshAI** → **retoques no Blender** (você, com o Claude ajudando) → o Claude limpa, reduz os polígonos e faz as UVs → **rig** (Mixamo ou o do MeshAI) → animações no Unity. O ciclo de aprovação é o mesmo.

---

## 6. As fases do desenvolvimento

**Princípio:** **jogabilidade em "caixa cinza" primeiro, arte depois, em paralelo.** Cada estrada e cada rio é montado e testado com formas simples antes de receber a arte final. Assim não se gasta semanas de arte num trecho que não diverte.

| Fase | O quê | Pronto quando |
|---|---|---|
| **0. Fundação nova** | Decidir o que fica do projeto atual (física do caiaque, água e correnteza, rede, controle da van). Pipeline do Blender (scripts, modelo de prévia, exportação). Shader toon macio + cena de conferência. Desenho do save e do carregamento por trechos | Um cubo feito no Blender aparece no Unity com a luz certa, pelo pipeline |
| **1. Âncoras de estilo** | Pinheiro, rocha e caiaque pelo ciclo de aprovação | Você aprova os 3 no Blender e no jogo |
| **2. Caixa cinza da Fase 1** | Partida, Estrada 1, evento da van (simples), Rio 1 e chegada, com formas simples, em trechos carregados aos poucos, jogável em grupo | Dá para jogar o prólogo inteiro com os amigos |
| **3. Kit da floresta** | Lotes 2 e 3 | A floresta inteira aprovada |
| **4. Personagens** | Lote 4 | 4 personagens animados no jogo |
| **5. Van e props** | Lote 5, incluindo a van nova | A van aprovada, por dentro e por fora |
| **6. Montagem do prólogo** | Trocar a caixa cinza pela arte, a primeira cena do evento da van, a história (postais, rádio, mapa) | O prólogo bonito, do início ao fim |
| **7. Sistemas da campanha** | Save do host, checkpoints, recompensas na van, tempo mínimo + ajuda escondida, amigo entrando no meio | Fechar, voltar e continuar de onde parou |
| **8. 🧪 Teste com amigos** | "Prólogo + Rio 1" | A pergunta: **querem ver o que vem depois?** |
| **9. Completar a Fase 1** | Rios 2 e 3, Estradas 2 e 3, eventos da van 2 e 3, o grande acampamento | A floresta completa |

---

## 7. Decisões (tomadas em 2026-10-03)
1. **História:** **o mapa da avó.**
2. **Personagens:** **animais**, no espírito de *Ultimate Chicken Horse* (silhuetas simples, carismáticas e engraçadas). O desenvolvedor cria no MeshAI (ou parecido) e passa para o Claude limpar, riggar e integrar.
3. **Projeto:** **um projeto Unity novo.** Só os sistemas que funcionam vêm do atual (caiaque, água e correnteza, rede, van). O projeto atual vira laboratório.
4. **Nome:** vai mudar, a definir. Por enquanto o nome de trabalho é **"CampanhaRio"**.
5. **Tempo mínimo:** **chegar antes do pôr do sol.** O céu escurece durante a descida e, se o tempo acaba, anoitece e a tela escurece (ninguém navega no escuro). Isso exige que **a luz e o céu mudem ao longo da descida**.

### Diretrizes de arte confirmadas
- **Sem pressa nos gráficos:** a qualidade vem antes da velocidade.
- **A estética das referências é só inspiração:** nada de HUD, van ou personagens delas.
- **Árvores leves:** o mínimo de triângulos possível, feitas para serem repetidas muitas vezes. Como não é mundo aberto, não são milhares de árvores detalhadas. **As do fundo são quase "borrões"** (silhuetas e impostores) que dão a impressão de floresta.
- **O ciclo de aprovação por elemento e a caixa cinza antes da arte** estão confirmados.
