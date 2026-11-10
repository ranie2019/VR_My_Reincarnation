# Revisao de falhas criticas — 04/11/2026

## Escopo
Revisao dos caminhos de save/carga, limpeza de originais do inventario e soma de atributos, com regressao automatizada das suites existentes de inventario, equipamentos, combate, inimigos, recursos e paineis. Nao equivale a demonstrar ausencia de todos os bugs do jogo.

## Falhas corrigidas

1. Perda de objeto do mundo ao carregar flechas do inventario. InventarioSaveAdapter.RemoverOriginalSoltoCorrespondenteAoItemRestauradoDireto usava fallback por tipo e selecionava o primeiro objeto, mesmo para um clone salvo com identidade individual diferente. Agora EncontrarOriginalParaLimpezaDireta exige tipo e identidade/origem correspondentes nos saves atuais. Duas correspondencias exatas abortam a carga reversivel. Saves antigos sem estados individuais e sem origem so usam fallback se ha um unico candidato. Itens em inventario/slot e originais ja usados nao sao removidos. Os antigos resolvedores exclusivos desse caminho, agora sem chamadas, foram removidos.

2. Overflow na agregacao de atributos. Bonus de duas luvas e demais equipamentos, bonus HP e total base+bonus eram somados em int, podendo virar negativos mesmo com entradas individuais validas. As somas agora promovem o primeiro operando para long e saturam no limite de int. Valores usuais, formula de dano e formula de defesa permanecem iguais; os campos e IDs serializados nao foram alterados.

## Validacao
44 scripts de testes foram executados: 43 passaram na bateria inicial e AtributosManoplas precisou incluir o novo helper real no host de teste. Apos o ajuste passou com 1011 verificacoes de sorteio e 114 de agregacao. Os 15 casos novos de limpeza/overflow e os 121 de inventario individual foram repetidos apos os ajustes finais. Compilacao completa com referencias Unity sem erros. Os hosts simulam Unity; nao comprovam callbacks de fisica/XR nem execucao no Quest.

## Limites e testes em Play
- Guardar uma flecha criada pelo arco/ferreiro, mantendo outra do mesmo tipo no mundo; salvar/carregar e conferir que a solta permanece.
- Guardar uma flecha original da cena; salvar/carregar e conferir que ela existe apenas no inventario.
- Conferir quantidades e ordem de retirada das pilhas e atributos/durabilidades individuais apos reiniciar.
- Equipar/retirar luvas, peitoral e elmo; conferir soma, HP/MP e ausencia de cura gratuita.
- Testar dano/defesa e restricao de espada abaixo, igual e acima do requisito.
- Persistem riscos dependentes do ambiente: callbacks durante commit/rollback, encerramento Android e selecao fisica XR. Nenhum save real, cena ou prefab foi editado nesta revisao. Nao foram criados commits.
