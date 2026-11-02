# HP/MP dos equipamentos e aba Status

## Causas

O StatusPlayer somava o atributo Mana dos equipamentos em GetManaTotal, mas a reserva realmente usada pelo HUD, consumo e regeneracao continuava limitada ao campo manaMaxima. A cena principal tambem possui Panel Status como filho de outro Panel Status desativado: MyMenu apontava para o filho, portanto SetActive(true) nele nao ativava a hierarquia visivel.

## Correcao

- Mana conserva ID 12 e passa a aparecer como MP (Mana) no Inspector e MP no popup. HP (Vida) foi acrescentado com ID 15, depois de Defesa (14), sem deslocar os IDs anteriores.
- HP maximo = vidaMaxima base + HP dos equipamentos vestidos. MP maximo = manaMaxima base + atributo Mana base + bonus de MP, incluindo equipamentos. A soma considera luvas, peitoral e elmo por meio do gerenciador do mesmo jogador.
- Os campos base de vidaMaxima/manaMaxima continuam sendo os usados no crescimento por nivel e no save. Os getters de exibicao retornam os limites efetivos.
- Consumo/regeneracao de mana, dano, sincronizacao da vida fracionaria, respawn e recuperacao de level-up respeitam os limites efetivos. A formula de defesa permanece a mesma.
- Aumento de capacidade nao concede cura ou mana instantanea: 403/403 com MP+4 passa a 403/407. Regeneracao pode preencher ate 407. Ao retirar o equipamento, o valor atual e limitado ao novo maximo. Equipar/retirar repetidamente nao cria recursos.
- GetVidaMaximaBase/GetManaMaximaBase sao usados pelo SaveManager para impedir incorporacao permanente de bonus ao save.
- MyMenu resolve o contêiner da aba sob seu botao para abrir/fechar a hierarquia correta. Tambem ativa o filho configurado ao abrir. Paineis externos a hierarquia do botao sao mantidos como estavam, e a API do inventario continua sendo chamada.
- MenuStatus e StatusUI exibem HP/MP nos rotulos, com busca automatica e aliases para nomes antigos BLOOD/MANNA. As referencias serializadas existentes permanecem validas. A busca de textos de vida ausente no StatusUI foi adicionada.

## Configurar HP nos itens

Nas listas de Atributos de cada classe em AtributosManoplas, AtributosPeitoral e AtributosElmo, adicione uma entrada HP (Vida) e defina seu minimo/maximo. MP (Mana) e o antigo Mana, com mesmo ID e valores salvos. As configuracoes ja serializadas dos prefabs nao sao sobrescritas: novas opcoes sao configuradas pelo usuario. Resultados de itens existentes nao sao sorteados novamente automaticamente. Cinco linhas e persistencia do slot equipado continuam fora de alteracoes.

## Verificacao

Tests/RecursosHPMP.Tests.ps1: 12 cenarios com metodos reais em host simulado, incluindo bonus, nao cura, consumo, regeneracao, retirada antes de dano/LateUpdate e limites base separados. Verifica tambem IDs antigos e captura dos limites base no save.
Tests/MenuStatusAninhado.Tests.ps1: 6 cenarios reais de abertura/fechamento em host simulado, incluindo pai desativado, filho desativado, integracao do inventario e paineis externos.
Tests de popup dos tres equipamentos: 35 verificacoes cada, incluindo HP/MP. Testes de atributos: 1011 de sorteio e 112 de soma. Estados individuais: 114 verificacoes aprovadas.
Compilacao pelo response file real do Unity sem adicionar fontes manualmente. Avisos CS0414 existentes continuam. Nao houve alteracoes em cenas, prefabs, saves reais ou commits.

Validar no Play/Quest: abrir Status na primeira abertura e depois de trocar de aba; equipar MP+4 e conferir maximo/regeneracao/consumo; retirar e conferir limite; gerar equipamento com HP; dano/respawn/level-up com bonus; salvar/carregar sem inflar limites base. Os testes simulam Unity/JsonUtility/XR e nao substituem esta verificacao nativa.
