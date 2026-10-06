# PARA APROVAR ÀS 20h

Tudo o que precisa do seu olho ou da sua decisão. O resto segue sem você.

## 1. Jogar o vertical slice (Fase 2, caixa cinza)
O ciclo inteiro funciona em rede: agência → quadro de pedidos → van do Seu Alce → Rio do Moinho → avaliação ⭐ →
vila → volta à agência. Os 2 pedidos: **carta urgente** e **bode medroso**.

**Como jogar** (pasta `Builds/CampanhaRio`):
- Sozinho: `CampanhaRio.exe`
- Com amigos: quem cria a sala abre `CampanhaRio.exe -cc-host`; os outros, `CampanhaRio.exe -cc-join <ip>`.
- **WASD** anda, **Shift** corre, **E** usa o quadro (só quem criou a sala escolhe) e entra na van.
- No caiaque, os controles de sempre.

**O que eu queria saber:**
- O **mesmo rio** pareceu outra descida com o bode? (é o critério da Fase 2)
- **Bode:** o medo enche rápido ou devagar demais? Hoje ele se assusta acima de 4,5 m/s, com pulos e pancadas fortes;
  capotar faz ele pular na hora. Cada pânico custa 25% de felicidade.
- **Carta:** capotar molha 35%; quedas e pancadas fortes molham um pouco. A estrela pede a carta 60% seca.
- **Prazos:** carta 4 min, bode 5 min (o rio inteiro tem 1,1 km).
- **As piadas do Seu Alce** (uma por chegada, em ordem): ver `Scripts/Campaign/SeuAlce.cs` e o português em `Loc.cs`.
  Troque à vontade.
- A **agência** e a **vila** são caixas cinza: o layout (galpão, quadro, van, cais) serve de ponto de partida?

Prints: `Assets/_Project/Docs/Screenshots/2026-10-06_fase2_*`.

## 2. Decisões que eu tomei sozinho (dá para mudar)
- A van é dirigida pelo Seu Alce (ninguém dirige). A van dirigível volta quando a estrada tiver arte.
- A volta da vila para a agência é um escurecer de tela (o Seu Alce leva vocês).
- O Rio do Moinho reaproveita o rio de teste (corredeiras, degrau, remansos). A "primeira cachoeirinha" ainda não
  existe: vem com o rio de verdade.
- Um amigo pode entrar a qualquer momento: durante a viagem vai direto para a van; durante a descida ganha um caiaque
  logo atrás do grupo (Fase 7, já feito).

## 3. As melhorias da agência e da van (Fase 7, primeiro rascunho)
Compradas no **quadro azul ao lado da porta do galpão** (E; só quem criou a sala compra), com as moedas dos pedidos e
liberadas pela reputação. As 3 de agora são provisórias (`Scripts/Campaign/AgencyUpgrades.cs`):
| Melhoria | Custo | Reputação | O que faz |
|---|---|---|---|
| Bagageiro na van | 60 | 2 | a van do Seu Alce 25% mais rápida |
| Um quadro de pedidos maior | 90 | 3 | 1 pedido a mais no quadro (3 → 4) |
| Uma placa nova para a agência | 40 | 1 | a placa do galpão maior e dourada (só visual) |

**Aprovar:** a lista, os preços e o que você quer que as melhorias façam (barcos novos? mais assentos na van? a
agência crescendo em estágios?). Hoje um pedido paga 40 a 86 moedas.

## 4. O mapa do vale (Fase 7, primeiro rascunho)
**M** abre o mapa (todo mundo). Ele se revela com o save do host: começa com a agência, a estrada e o Rio do Moinho;
a **Vila do Moinho** aparece depois da carta urgente; o **Rio das Pedras** com reputação 4 e a **Vila das Pedras** com
6 (os dois "em breve"). Cada lugar novo avisa uma vez ("Novo no mapa: ..."). Lugares e regras em
`Scripts/Campaign/ValleyMap.cs`. **Aprovar:** os lugares, os nomes e o que revela cada um. O desenho é caixa cinza.

## 5. Variações do pinheiro e da pedra (Fase 3, adiantado)
Feitas com os mesmos geradores e o mesmo estilo das versões aprovadas (só forma e tamanho mudam). Ainda **não** estão
no jogo: entram depois do seu ok. Pranchas em `ArtSource/<Família>/<Asset>/<Asset>_v001_preview.png`; todas lado a lado
em `Docs/Screenshots/2026-10-06_variacoes/variacoes_3-4.png` (na ordem: PinheiroA de referência, B, C; RochaA de
referência, B, C, D).
- **PinheiroB:** jovem, 6,5 m, mais fino.
- **PinheiroC:** alto e velho, 11 m, mais largo, um pouco inclinado.
- **RochaB:** seixo pequeno (~1 m).
- **RochaC:** laje baixa e larga (~3 m).
- **RochaD:** pedra alta (~2,3 m).

**Aprovar:** quais entram (e o que mudar). Aprovadas, elas vão para a floresta e as margens do Rio do Moinho.
