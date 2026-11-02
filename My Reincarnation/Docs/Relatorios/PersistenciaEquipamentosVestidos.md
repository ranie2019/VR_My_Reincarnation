# Persistencia dos equipamentos vestidos

## Causa
O SaveManager capturava os itens vestidos como objetos de cena, mas nao registrava a posicao de equipamento. A restauracao aplicava apenas transform, atributos e durabilidade, marcava o item como solto e ativava fisica. O GerenciadorEquipamentosPlayer ficava sem as referencias, sem encaixe e sem bonus.

## Implementacao
SaveData agora usa versao 0.6-equipamentos-vestidos. O leitor aceita as versoes anteriores ate 0.6. EstadoCenaItem possui posicaoEquipamento opcional: mao-direita, mao-esquerda, cabeca ou peitoral. Esses tokens nao dependem de nomes, descricao ou traducao.

SaveManager registra a posicao pelo estado JaEquipado dos controladores. O item continua tendo um unico registro de cena, com os mesmos IDs, origem, extras dos atributos e durabilidade; nao e duplicado no inventario. Um item vestido e incluido mesmo quando salvarQuandoSoltoNaCena esta desabilitado.

Antes de aplicar qualquer geracao, a carga rejeita posicao desconhecida/repetida, equipamento inativo/quebrado, durabilidade invalida, tipo/lado incompativel, mais de um controlador e destino/gerenciador ausente. Permanecem as validacoes de IDs, inventario, origem, extras, removidos permanentes e backups existentes. Uma falha de preflight pode tentar outra geracao, sem equipar nem desequipar objetos.

Na aplicacao: limpa referencias vestidas atuais sem devolver itens ao inventario; restaura inventario, transform do jogador e dados dos itens; equipa os itens pelo gerenciador usando o fluxo normal e os pontos de encaixe existentes; so entao restaura HP/MP e missoes. Isso evita limitar os recursos aos maximos sem os bonus. Prefabs/instancias inativos sao inicializados antes de aplicar o transform salvo do equipamento, preservando a captura da escala original.

Luvas usam o encaixe normal da mao; elmo/peitoral usam PosicaoEquipe e configuracao atual. Mantidas a opcao de esconder mao/corpo e a ocultacao local do elmo, sem esconder a cabeca. Bonus sao consultados das referencias equipadas, sem soma nos atributos base. Desgaste continua com a regra atual, sem desgaste offline novo.

## Recuperacao de falha
Snapshots dos itens incluem agora os flags privados de equipamento e callbacks originais. O gerenciador captura as referencias e visibilidade do avatar. Em erro, libera inclusive um encaixe parcial que falhou antes de registrar a referencia, recupera inventario/mundo/itens, limpa novas instancias, recupera o gerenciador e restaura o jogador por ultimo. Mantido o bloqueio de salvamento apos carga malsucedida. Nenhum save real foi reescrito nos testes.

Esta protecao nao demonstra reversibilidade de efeitos arbitrarios futuros em callbacks externos. A ordem nativa de callbacks e renderizacao ainda requer Play/Quest.

## Compatibilidade
Saves ate 0.5 nao registravam onde os itens estavam vestidos. Continuam sendo lidos sem inventar um destino pela posicao no mundo. Equipar e salvar uma vez apos esta atualizacao cria o novo registro; nas cargas seguintes o equipamento volta vestido automaticamente. Sem alterar arquivos reais nem apagar saves para migrar.

Escopo atual: duas luvas, elmo e peitoral. Nao inclui restaurar armas seguradas pelos controladores, equipamentos de NPCs ou multiplayer.

## Validacao executada
- EquipamentosVestidos.Tests.ps1: 29 verificacoes de metodos reais em host simulado, DTO real com JSON simulado, quatro posicoes, dados individuais, saves antigos, preflight, slots duplicados, destinos ausentes, falha/limpeza parcial e recuperacao do gerenciador.
- EquipamentosRuntimeRollback.Tests.ps1: 4 cenarios dos snapshots privados reais de luvas/elmo/peitoral.
- InventarioEstadosIndividuais: 114 verificacoes aprovadas.
- EquiparPeitoral: 12 verificacoes aprovadas.
- LuvasFisica: 4 cenarios aprovados.
- ElmoVisibilidadeLocal: 23 verificacoes aprovadas.
- RecursosHPMP: 12 cenarios aprovados, mais verificacoes de limites base e IDs.
- ArquivoSaveSeguro: 22 verificacoes em arquivos descartaveis dentro de Temp do projeto.
- Total numerico: 220 verificacoes/cenarios, alem de verificacoes estaticas.
- Compilacao via response file real do Unity sem erros, somente avisos CS0414 existentes. Diff check sem erros de whitespace. Nenhum commit criado.

## Teste manual no Unity/Quest
1. Carregar o save existente; equipar luvas D/E, elmo e peitoral. Anotar IDs, classe, atributos, vida atual/maxima, HP/MP e bonus.
2. Sair normalmente/parar Play (ou usar o salvamento normal existente) e iniciar novamente. Conferir os quatro encaixes, cabeca visivel e elmo oculto somente na camera local, corpo/maos conforme configurados. Nenhum equipamento deve ficar solto.
3. Conferir bonus e HP/MP sem duplicacao, identidade e durabilidade preservadas.
4. Trocar uma luva e o elmo, salvar e reiniciar: apenas o conjunto novo deve voltar vestido; os anteriores devem continuar nos slots do inventario.
5. Com inventario cheio, carregar um conjunto vestido: restauracao nao deve exigir espaco extra no inventario. Retirar depois segue as regras atuais de espaco.
6. Desequipar, guardar/soltar e salvar: o proximo inicio deve respeitar esse novo destino, sem reequipar automaticamente o item antigo.
7. No Quest, verificar pausa/retomada e fechar/reabrir aplicativo. Em copia descartavel do save, testar posicao duplicada/destino ausente para confirmar fallback/bloqueio sem sobrescrita.

Play e Meta Quest nao foram executados por esta revisao.
