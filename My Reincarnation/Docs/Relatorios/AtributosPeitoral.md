# AtributosPeitoral

Componente implementado no arquivo existente, mantendo seu .meta. Adicionar na raiz do prefab do peitoral, junto de EquiparPeitoral e ItemPersistente. Nao adicionar Equipamento generico para controlar a mesma durabilidade.

## Configuracao

- Prefab Objeto e Descricao.
- Classes: chance independente, zero desativa, tipos compartilhados com manoplas. Cada peitoral tem sua propria configuracao de chances, quantidade de atributos e faixas por classe.
- Luz/Trevas: sorteio 50/50 apenas em Divino. Bloqueio por afinidade do jogador continua fora de escopo, assim como passivas e conjuntos.
- Popup: mesmo objeto associado em EquiparPeitoral; textos de nome, classe e descricao.
- Cinco linhas: Texto Nome, Texto Valor, Seta Up/Down, Texto Diferenca (magnitude), Sinal Positivo/Negativo. Ordem de cima para baixo. A comparacao usa somente o peitoral equipado e mostra tambem perdas de atributos que faltam na candidata. Nao compara com luvas ou elmo. Mao/slot sem peitoral usa zero como base; dados indisponiveis ocultam a comparacao.
- Durabilidade: vida atual/maxima 100, desgaste 0,5 por minuto enquanto equipado; associe textos de atual e total. Nao desgasta guardado ou solto, nem offline. AplicarDesgasteCombate existe, sem evento automatico.

O StatusPlayer soma peitoral e manoplas, conservando valores base e bonus anteriores. Ao quebrar, o peitoral libera a visibilidade original do corpo, remove sua referencia do gerenciador e seus bonus, registra consumo permanente e destroi a propria raiz. O gerenciador e o script EquiparPeitoral continuam responsaveis pela troca, alinhamento e popup lateral; a configuracao PosicaoEquipe nao foi alterada.

O save grava o resultado individual em envelope my-reincarnation.peitoral.v1, preservando extras anteriores. Atual/maxima de durabilidade sao preservadas no inventario e em itens soltos. Saves antigos sem dados de peitoral permanecem aceitos e recebem a configuracao inicial do prefab. A validacao antecipada detecta payloads incompativeis antes da aplicacao. Reparar e CriarItem reconhecem o novo componente.

Os algoritmos de sorteio de raridade/afinidade e calculo de desgaste delegam aos metodos ja utilizados por AtributosManoplas, mantendo o mesmo comportamento. Nao foram alterados enum existentes ou a persistencia de equipamentos vestidos (continua fora de escopo). O limite visual de cinco linhas continua inalterado.

## Verificacao

Compilacao via response file real do Unity: sem erros, avisos CS0414 preexistentes.
- 108 verificacoes de estados individuais, incluindo peitoral: atributos, durabilidade fracionaria/maxima, cena, payload estavel, migracao e rejeicao de componente errado.
- 110 verificacoes de soma de bonus, incluindo peitoral e duas manoplas.
- 11 cenarios de desgaste do peitoral e 33 verificacoes de linhas/comparacao, metodos reais em host simulado.
- 17 verificacoes do ferreiro e 12 de troca/visibilidade do peitoral.

Estes testes simulam Unity/JsonUtility/XR. Validar em Play/Quest: novos prefabs e clones, popup com referencias configuradas, troca com luvas equipadas, diferencas positivas/negativas, desgaste, reparo, quebra com corpo oculto, save/load do item guardado e solto. Aplicar as configuracoes ao prefab para os proximos clones. Nao foram editados saves reais nem feitos commits.
