# Correcoes da auditoria - 2026-11-01

## Escopo autorizado

Foram mantidos fora do escopo: persistencia do slot equipado e ampliacao das cinco linhas de atributos (itens 4 e 9 da auditoria). Nao foram criados commits ou alterados saves reais. As modificacoes preexistentes de cenas, prefabs e outros scripts foram preservadas.

## Correcoes

- EquiparManoplas: velocidades sao zeradas somente em corpos dinamicos antes de torna-los cinematicos. Interactables, colliders, rigidbodies e visibilidade da mao passam a recuperar os estados capturados ao equipar, em vez de habilitar tudo.
- AtributosManoplas: a comparacao passa a considerar a uniao dos tipos da candidata e da equipada. Um atributo perdido e exibido como valor zero com diferenca negativa, sem alterar os atributos reais do item. O limite visual continua sendo cinco linhas.
- Espada: Dano = danoEspada + Forca total do dono * multiplicadorForca. Multiplicador configuravel, inicial 1; zero conserva somente o dano da arma. Sem dono usa zero de Forca. O contato da espada e a interface IDano usam o mesmo calculo. Arredondamento inteiro consistente e saturacao evitam estouro.
- StatusPlayer: formula de defesa original preservada, ataque * ataque / (ataque + ConstituicaoTotal + ResistenciaTotal). Somente as entradas passaram a incluir bonus. O atributo separado Defesa nao foi acrescentado como novo termo, conforme orientacao do usuario.
- CriarItem/BotaoCriarItem: custos sao agrupados sem diferenciar maiusculas e espacos nas bordas. A operacao reserva as unidades de forma reversivel, bloqueia save/reentrada, cria e valida o resultado antes de confirmar consumo e destruicao. Falhas recuperam as mesmas referencias e pilhas. Item com configuracao de atributos invalida nao confirma consumo.
- ItemPersistente/SaveManager: dados de atributos sao validados sem alterar instancias, antes de escolher uma geracao. A validacao abrange itens de inventario e de cena. Backups ainda sao avaliados enquanto nenhuma mutacao comecou.
- SaveManager: recuperacao passa a incluir snapshots dos componentes ISalvavel existentes e dos estados/progresso/indice e visuais filhos das missoes. Falha anterior ao inicio da aplicacao nao reaplica snapshots, evitando callbacks de recuperacao desnecessarios. IDs genericos vazios/duplicados geram erro explicito em vez de selecionar um objeto arbitrario.
- ArquivoSaveSeguro: depois de publicar e validar o principal, conserva ate 20 arquivos gerados por familia tmp.preservado e bak.previous.preservado. Nomes que nao terminam em GUID nao sao removidos. Principal, bak, previous, tmp e arquivos corrompido nao entram na limpeza. Falha de limpeza nao invalida publicacao.
- SpawnOnDestroy/RespawnOnDestroy: protecoes contra encerramento da aplicacao, cena descarregada, destruicao de item guardado e cargas nao concluidas. Estes scripts nao tinham referencias na cena principal/prefabs examinados.

## Validacao

Compilacao usando o response file real do Unity e saida temporaria, sem adicionar fontes manualmente. Permanecem avisos CS0414 de campos nao utilizados.

Testes em host simulado:
- AtributosManoplas: 1011 verificacoes de sorteio + 106 de soma.
- PopupAtributosManoplas: 33, incluindo perda de atributo.
- InventarioEstadosIndividuais: 98, incluindo rejeicao antecipada de extras invalidos/componente ausente.
- ArquivoSaveSeguro: 22, incluindo limite de retencao e preservacao das geracoes.
- MissoesMateriais: 10, falhas injetadas na entrega.
- FerreiroTransacao: 17, custos duplicados, insuficiencia, falha parcial, spawn nulo/excecao, atributos invalidos e reentrada.
- LuvasFisica: 4 cenarios, incluindo mao originalmente oculta e rigidbody originalmente cinematico.
- ForcaDanoEspada: 7 verificacoes numericas e verificacao da preservacao da formula de defesa.
- RecuperacaoMissoes: 3 cenarios, identidade da missao, progresso e indice/visual.

Os testes extraem metodos reais, mas simulam Unity, JsonUtility e XR. Nao validam o comportamento nativo de callbacks, registro dos sockets, fisica, descarregamento de cenas, IO Android ou efeitos externos de scripts futuros. A recuperacao cobre o estado dos componentes atuais; nao e uma transacao capaz de desfazer arbitrariamente efeitos externos produzidos por callbacks de terceiros.

## Testes no Unity

1. Equipar/desequipar luvas de ambas as maos; verificar colliders, interacao, visibilidade e ausencia do aviso cinematico.
2. Comparar candidata com Forca e equipada com Forca/Resistencia; conferir perda de Resistencia na linha adicional, dentro das cinco linhas existentes.
3. Ajustar multiplicador de Forca da espada para 0, 1 e 2; confirmar dano e defesa com/sem bonus.
4. Ferreiro: duas entradas do mesmo custo, quantidade insuficiente, receita dividida em slots e falha de configuracao do prefab; conferir materiais e identidades.
5. Em copias descartaveis de saves, invalidar o payload de atributos do principal mantendo backup valido; confirmar selecao do backup antes de aplicar.
6. Injetar falha apos mudanca de objetos/missoes; confirmar estado original, indice, progresso e bloqueio de autosave. Teste exige Unity para cobrir eventos reais.
7. Testar Pause/Quit e retomada no Quest; manter os saves reais preservados durante testes destrutivos.
