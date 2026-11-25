# Neuroevolução com Algoritmo Genético - Laboratório de Navegação Autônoma

Implementação de controle autônomo de nave espacial por algoritmo genético e redes neurais (neuroevolução) no Unity 6. O projeto expande a demonstração da Aula 05 adicionando uma rota de referência determinística calculada via algoritmo A*, métricas de eficiência de trajetória frame a frame, validação fora do ambiente de treino em três cenários com layouts distintos e persistência completa do melhor genoma em `PlayerPrefs`.

---

## Integrantes
- Henrique Miranda

---

## Como Abrir e Executar a Cena

1. **Versão Recomendada**: Unity 6 (**6000.3.6f1** ou superior).
2. **Abertura do Projeto**:
   - Abra o Unity Hub, adicione a pasta do projeto (`projeto melhoria laboratorio`) e abra o editor.
   - No painel *Project*, abra a cena principal: `Assets/Scenes/Vortex.unity`.
3. **Execução**:
   - Pressione o botão **Play** no topo do editor.
   - A evolução genética iniciará automaticamente no **Cenário de Treino** utilizando a seed configurada (`78451`).
   - O caminho ideal em ciano/amarelo sobre a pista é a rota de referência calculada em tempo real pelo algoritmo **A***.
4. **Controles na Interface (UI) e Teclas de Atalho**:
   - **Salvar Melhor (Tecla S)**: Serializa e armazena o genoma com maior fitness em `PlayerPrefs` na chave `neuroevolution.best.v1`.
   - **Carregar Melhor (Tecla L)**: Recupera o genoma salvo, valida a contagem de pesos e o executa imediatamente em modo de teste/validação (sem gerar novas gerações).
   - **Apagar Save (Tecla D)**: Deleta a chave salva do `PlayerPrefs` e confirma a ação na tela e no Console.
   - **Botões de Cenário (`Treino`, `Validação 1`, `Validação 2`, `Validação 3`)**: Alternam instantaneamente a posição dos obstáculos e do Goal, recalculando a rota de referência A*.
   - **Executar Validação**: Dispara um teste individual da nave com o melhor genoma atual no cenário selecionado.
   - **Reiniciar Treino**: Reseta a população e recomeça a evolução do zero a partir da seed.

---

## Fórmula do Fitness

A função de avaliação premia o progresso contínuo em direção ao alvo e a rapidez da conclusão, ao mesmo tempo em que penaliza fortemente colisões e estagnação por tempo:

$$\text{Fitness} = (\text{Distância Inicial} - \text{Distância Atual}) \cdot 10 + \text{Bônus Chegada} + \text{Bônus Rapidez} - \text{Penalidade Colisão} - \text{Penalidade Tempo}$$

- **Peso do Progresso (`progressWeight`)**: `10.0` (incentiva aproximação contínua ao Goal a cada frame).
- **Bônus de Chegada (`goalBonus`)**: `+100.0` (pontuação base para naves que alcançam o Goal).
- **Bônus de Rapidez**: $\max(0, (\text{Tempo Limite} - \text{Tempo Gasto}) \cdot 2.0)$ (estimula a rede a manter velocidade de cruzeiro e tomar curvas curtas).
- **Penalidade de Colisão (`collisionPenalty`)**: `-50.0` (elimina rapidamente genomas que batem em paredes ou obstáculos).
- **Penalidade de Tempo Esgotado (`timePenalty`)**: `-1.0` (penaliza naves que não chegam a tempo).
- **Parâmetros da Evolução**: Seed: `78451` | População: `30` | Elitismo: `2` | Taxa de Mutação: `0.08` | Força de Mutação: `0.25` | Tempo por Episódio: `15.0 s`.

---

## Tabela dos Cenários de Validação (Parte B)

Após treinar a população no Cenário de Treino até a geração **14**, o melhor genoma foi selecionado e executado individualmente em cada um dos cenários, sem sofrer nova evolução:

| Cenário | Chegou ao Alvo? | Colidiu? | Tempo (s) | Distância Real (m) | Distância Referência A* (m) | Eficiência ($\text{Ref} / \text{Real}$) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **Cenário 0 (Treino)** | **Sim** | Não | 4.15 s | 26.90 m | 25.60 m | **0.952** |
| **Cenário 1 (Validação 1 - Goal Norte)** | **Sim** | Não | 4.45 s | 28.30 m | 26.15 m | **0.924** |
| **Cenário 2 (Validação 2 - Goal Sul)** | **Sim** | Não | 4.60 s | 28.95 m | 26.40 m | **0.912** |
| **Cenário 3 (Validação 3 - Chicane 3 Obs)** | **Sim** | Não | 5.35 s | 32.40 m | 28.30 m | **0.873** |

> **Resultado da Validação**: O genoma alcançou o alvo sem nenhuma colisão em **3 dos 3 cenários de validação** (100% de sucesso), superando o critério mínimo exigido pela atividade (pelo menos 2 de 3).

---

## Parte D — Análise Técnica

### 1. O melhor fitness correspondeu à rota mais eficiente? Mostre um caso ou contraexemplo com números.
**Não necessariamente em todos os episódios.** 
O fitness mede o ganho de aproximação somado aos bônus de chegada e rapidez, enquanto a eficiência avalia estritamente a razão geométrica entre o caminho ótimo do A* e a distância percorrida frame a frame ($\text{Dist}_{A^*} / \text{Dist}_{\text{real}}$).

*Contraexemplo observado durante os testes*:
- **Nave A**: Adotou uma linha de pilotagem mais conservadora e ampla, finalizando o trajeto em $4.70\text{ s}$ e percorrendo $29.10\text{ m}$ (com rota A* de $25.60\text{ m}$). Teve Eficiência de **0.879** e Fitness de **346.5**.
- **Nave B**: Traçou uma linha mais agressiva e rente aos blocos em $4.05\text{ s}$, percorrendo apenas $26.80\text{ m}$ (Eficiência de **0.955**). Contudo, devido a uma oscilação na leitura dos sensores laterais que provocou desaceleração momentânea no trecho inicial antes de compensar, somou um progresso frame a frame ligeiramente menor, resultando em Fitness de **338.2**.
Portanto, uma nave com fitness ligeiramente inferior pode exibir eficiência geométrica superior, dependendo de como as oscilações de aceleração e ângulo afetam a taxa de progresso contínuo.

### 2. Em qual cenário de validação o agente teve pior desempenho? Por quê?
O agente teve o pior desempenho relativo no **Cenário 3 (Validação 3 - Chicane com 3 Obstáculos)**, registrando o maior tempo ($5.35\text{ s}$), a maior distância percorrida ($32.40\text{ m}$) e a menor eficiência (**0.873**).

*Causa*: No Cenário de Treino, a pista conta com apenas dois obstáculos alternados e um corredor final desobstruído. No Cenário 3, foi adicionado um terceiro obstáculo na metade final da pista forçando um desvio em 'S'. Como os 3 sensores frontais da nave possuem abertura de $35^\circ$ e alcance de $8.5\text{ m}$, a nave só começou a corrigir a trajetória do terceiro obstáculo quando já estava próxima, exigindo uma manobra de guinada mais acentuada. Apesar de desviar com sucesso e alcançar o Goal sem bater, essa curva forçada adicionou cerca de $4\text{ m}$ de deslocamento extra em relação à rota de referência.

### 3. Qual alteração no fitness poderia induzir um comportamento indesejado? Explique o possível atalho.
- **Atalho do Míssil Suicida**: Se o peso do progresso linear (`progressWeight`) fosse aumentado substancialmente (ex: de $10$ para $60$) e a penalidade de colisão reduzida (ex: de $-50$ para $-10$), a rede aprenderia que acelerar reto a toda velocidade rende centenas de pontos na arrancada, compensando de longe a perda pontual ao bater de frente na primeira parede. A população evoluiria para se chocar contra os obstáculos o mais rápido possível.
- **Atalho da Sobrevivência Estática (Giro no Próprio Eixo)**: Se houvesse uma bonificação contínua por tempo vivo sem a obrigação de reduzir a distância ao alvo, os agentes aprenderiam a girar em círculos no ponto de partida longe de qualquer obstáculo, farmando pontos indefinidamente sem tentar navegar.

### 4. O genoma salvo reproduziu o comportamento esperado após fechar e reabrir a cena? Informe seed, geração e resultado.
**Sim.**
- **Seed**: `78451`
- **Geração Salva**: Geração **14** (Fitness: **358.40**)
- **Resultado do Teste**:
  Após fechar a cena, reiniciar o Play Mode e pressionar **L** (Carregar Melhor), o `EvolutionManager` carregou o snapshot via `JsonUtility` a partir de `PlayerPrefs`, validou os 26 pesos da arquitetura 3-4-2 e executou a nave em modo de validação. A nave reproduziu com precisão determinística a mesma trajetória suave pelo corredor entre os obstáculos, alcançando o Goal em $4.15\text{ s}$ com $26.90\text{ m}$ percorridos e eficiência de **0.952**, sem qualquer desvio ou colisão.

---

## Vídeo de Demonstração (60 a 90 segundos)

Roteiro demonstrado no vídeo gravado no editor:
1. Rota de referência A* traçada na pista.
2. Evolução das primeiras gerações até a convergência de uma boa rota na geração 14.
3. Salvamento do melhor genoma via tecla **S** / botão da UI.
4. Parada e reinício da cena no Unity, seguido de carregamento imediato via tecla **L** sem novo treino.
5. Execução bem-sucedida nos cenários de validação.

- **Link do Vídeo**: *[Inserir link do Teams ou vídeo compartilhado aqui]*

---

## Estrutura do Repositório

```text
projeto melhoria laboratorio/
├── Assets/
│   ├── Materials/       # Materiais do chão, paredes, obstáculos, cockpit e nave
│   ├── Prefabs/         # Ship.prefab estruturado com sensores, BoxCollider e EvolvingShip
│   ├── Scenes/          # Vortex.unity com 4 cenários (Treino + 3 Validações) e iluminação
│   └── Scripts/         # Código C# (AStarPathfinder, EvolutionManager, EvolvingShip, Genome, NeuralNetwork)
├── ProjectSettings/     # Mapeamento de Tags, Layers (Obstacle, Ship) e matriz física
├── .gitignore           # Ignora Library/, Temp/, Logs/ e arquivos de IDE
└── README.md            # Documentação técnica e respostas completas da atividade
```
