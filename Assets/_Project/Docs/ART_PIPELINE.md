# Pipeline de arte (Blender → Unity)

Todo asset é feito no **Blender 5.2 por scripts Python**, **um elemento de cada vez**, com a **aprovação do desenvolvedor
a cada versão**. Nada é importado de lojas ou bibliotecas. As referências de estilo estão em `Docs/Reference/` e no
`STYLE_GUIDE.md`.

## 1. O protocolo de revisão (vale da Fase 1 em diante)

1. **Briefing.** O Claude escreve o briefing no `NOTES.md` do asset: referências, tamanho real (m), orçamento de
   triângulos por LOD, cores (nomes da paleta) e o que o asset precisa comunicar. **O desenvolvedor confirma** antes de
   qualquer modelagem.
2. **v001 por um gerador paramétrico.** Cada família (pinheiro, rocha, caiaque…) ganha **um** script gerador com
   parâmetros (`ArtSource/<Família>/<familia>_gen.py`), para que as variações saiam baratas depois que o mestre for aprovado.
3. **Prévia + abrir.** O gerador renderiza a folha de prévia. O Claude roda `art review <Família> <Asset>`, que **abre
   o PNG** no visualizador de imagens e **o `.blend` no Blender** (para girar e olhar à vontade). **O Claude para e espera
   o feedback.**
4. **Feedback → v002, v003…** O feedback vai **literal, em português** no `NOTES.md` da versão, junto com o que mudou.
   **Versões antigas nunca são sobrescritas** (o script se recusa a sobrescrever).
5. **Aprovado → Unity.** `art export` gera o FBX e o sidecar de material. O Unity importa sozinho (LODs, material
   SoftToon, colisor). O Claude tira um print na cena `LookDev` e mostra. **O desenvolvedor dá o OK final.**
6. **Variações.** Com o mestre aprovado, o gerador cria a família (por exemplo 5 pinheiros, 6 rochas), e há **uma**
   revisão do conjunto inteiro.

## 2. Comandos

Tudo pelo `ArtSource/art.cmd` (Windows):

| Comando | O que faz |
|---|---|
| `art build Test\pebble_gen.py --asset Pebble --seed 3` | Cria a **próxima** versão (`Pebble_v00N.blend`), renderiza `Pebble_v00N_preview.png` e acrescenta a versão no `NOTES.md` |
| `art preview Test\Pebble\Pebble_v001.blend` | Renderiza de novo a folha de prévia de uma versão (o `.blend` não muda) |
| `art review Test Pebble [v001]` | Abre a prévia + o `.blend` no Blender (a última versão se não disser qual) |
| `art export Test\Pebble\Pebble_v003.blend` | Exporta a versão aprovada para `Assets/_Project/Art/Models/Test/` |

Por baixo, cada comando é `blender.exe --background --python <script> -- <args>` (EEVEE). Os módulos ficam em
`ArtSource/pipeline/`:

- `common.py`: reset da cena, unidades (1 unidade = 1 m), a paleta, os eixos Unity↔Blender, o material de prévia
  SoftToon, cores de vértice, gradientes, regras de nome, LODs (decimar, juntar, normais de um proxy suave) e versões.
- `lookdev.py`: o rig padrão de prévia (sol, ambiente, chão neutro, a referência de escala de 0,9 m, câmeras).
- `preview.py`: a folha de prévia (4 ângulos, close, silhueta, escala, LODs, e um cabeçalho com triângulos e tamanho).
- `export.py`: FBX para o Unity + `<Asset>.softtoon.json` (os parâmetros do material).
- `open_for_review.cmd`: abre a prévia e o `.blend` sem travar o terminal.
- `compare.py`: Blender x Unity lado a lado (o último passo do protocolo).

A folha de prévia aplica a **mesma cadeia de pós-processamento do Unity** (exposição, balanço de branco, contraste,
saturação e o tonemapping Neutral do URP), então as cores da prévia são as cores do jogo.

**A luz é uma só:** `Assets/_Project/Art/LookDev/lookdev_rig.json` define o sol (ângulo, cor, intensidade), o ambiente
(céu, horizonte, chão), a cor da sombra, a paleta e os ângulos das câmeras. O Blender e a cena `LookDev` do Unity leem
o mesmo arquivo. Para mudar a luz, mude ali e renderize de novo dos dois lados.

## 3. Pastas e nomes

```
ArtSource/                         fora de Assets (o Unity não importa .blend)
  art.cmd                          os comandos acima
  pipeline/                        os módulos Python compartilhados
  <Família>/<familia>_gen.py       o gerador paramétrico da família
  <Família>/<Asset>/<Asset>_v001.blend, <Asset>_v001_preview.png, NOTES.md
  _tmp/                            renders intermediários (ignorado pelo Git)
Assets/_Project/Art/Models/<Família>/<Asset>.fbx + <Asset>.softtoon.json      só versões aprovadas
Assets/_Project/Art/Materials/<Família>/M_<Asset>.mat                          criado pelo importador
```

- Objetos do asset: `<Asset>_LOD0`, `<Asset>_LOD1`, `<Asset>_LOD2` e, opcional, `<Asset>_COL` (colisor simples,
  convexo, invisível no jogo). O exportador recusa outros nomes.
- Nomes em PascalCase sem espaço (`PinheiroA`, `RochaGrande`). As versões são `_v001`, `_v002`…
- A origem fica **no centro da base** do asset (assim ele "pousa" no chão no Unity).
- Frente do asset no Blender = **-Y** (a vista *Front*). No Unity ela vira **+Z**.
- `.blend` e `.png` vão para o Git LFS; `*.blend1` (backup automático) é ignorado.

## 4. Orçamentos (meta: 60 FPS em 1080p numa GTX 1660 Super, com 4 jogadores)

| Elemento | LOD0 | LOD1 | LOD2 / longe | Observação |
|---|---|---|---|---|
| **Pinheiro herói** | ≤ 1.500 tris | ≤ 500 | billboard (2–4 tris) | o primeiro asset da Fase 1; os números são revistos depois que ele for aprovado |
| Árvore de folha / arbusto grande | ≤ 1.200 | ≤ 400 | billboard | |
| Arbusto / tufo | ≤ 300 | ≤ 80 | some | |
| Rocha média | ≤ 800 | ≤ 250 | ≤ 80 | normais suaves de um proxy |
| Falésia modular (peça de ~10 m) | ≤ 3.000 | ≤ 1.000 | ≤ 300 | |
| Caiaque | ≤ 2.500 | ≤ 800 | ≤ 200 | |
| Personagem (animal) | ≤ 8.000 | ≤ 3.000 | ≤ 1.000 | skinned |
| Floresta de fundo ("borrão") | cartões de silhueta, poucos tris cada | | | |
| Seixo de teste (Fase 0) | ≤ 600 | ≤ 150 | – | colisor ≤ 40 |

Distâncias de troca (altura na tela): LOD0 → LOD1 a ~40%, LOD1 → LOD2 a ~15%, e some abaixo de ~2%, com
*cross-fade* por dither (o shader SoftToon já suporta). O importador aplica isso sozinho.

## 5. Estratégia de folhagem (decidida agora: árvores **muito** leves)

1. **Perto: cartões e tufos.** As copas são tufos de **cartões com textura de folhas pintadas e alpha clip** (sem
   transparência real) em troncos low-poly. As **normais dos cartões vêm de um proxy suave** (uma esfera ou cone
   inflado que envolve a copa), copiadas com *Data Transfer*. É isso que dá a luz macia e "inflada" das referências, em vez
   do aspecto de "cartões soltos". No shader: variante **Foliage** (alpha clip, translucência quando a luz vem por trás,
   balanço do vento pela cor de vértice).
2. **Meia distância: impostores.** Árvores distantes viram **impostores**: um billboard virado para a câmera (ou
   octaédrico, se for preciso girar em volta), assado do próprio asset no Blender com a luz-padrão. 2 a 4 triângulos.
3. **Fundo: "borrões".** A floresta do fundo é feita de **cartões de silhueta** largos (várias árvores num cartão só),
   em 2 ou 3 camadas com a névoa de distância. Alguns triângulos cada. Eles dão a sensação de floresta sem custo.
4. **Texturas:** atlas pequeno de folhas pintadas (512–1024 px) por família, compartilhado. Sem mapas normais nas
   folhas (as normais do proxy bastam).

## 6. A prévia contra o jogo

A folha de prévia usa a **mesma fórmula** do shader SoftToon do Unity (wrap + rampa suave + sombra colorida + ambiente em
gradiente + borda), calculada no material do Blender. As diferenças conhecidas estão em `TECH_DECISIONS.md`
(teste de comparação: esfera e cubo).
