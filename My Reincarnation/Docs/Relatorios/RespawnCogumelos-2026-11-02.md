# Respawn dos cogumelos

> Relatorio inicial substituido pela correcao documentada em CorrecaoInicializacaoCogumelos-2026-11-02.md. A criacao automatica de pasta descrita abaixo foi removida; ponto vazio ou movimento de fisica, isoladamente, nao autorizam reposicao.

## Comportamento
Cocumelo agora possui campos publicos Distancia Para Respawn (1 unidade de mundo), Tempo Respawn (30 segundos de jogo) e Altura Respawn (0.5 unidade). O tempo autoritativo dos cogumelos fica neste componente; o tooltip da configuracao generica de RespawnNatureza esclarece essa separacao.

Mudar somente o pai nao solicita reposicao. Cada local original tem um ponto registrado, identificado por tipo de respawn e coordenadas originais. Sair do raio configurado inicia uma unica reposicao para esse ponto; guardar no inventario tambem libera a ocupacao do mundo. O item coletado fica desarmado para nao gerar outras solicitacoes. O responsavel pela espera e RespawnNatureza, portanto desativar o item ao guarda-lo nao cancela sua reposicao.

O clone nasce em pontoOriginal + Vector3.up * alturaRespawn, com a rotacao original. A origem transmitida a todas as geracoes continua sendo o mesmo ponto, nunca a posicao ja elevada. O assentamento vertical entre a altura de nascimento e o solo nao conta como nova coleta. Pontos diferentes do mesmo tipo funcionam independentemente. Se o item retornar antes do prazo, a espera e cancelada; uma nova saida inicia o prazo completo.

## Hierarquia
RespawnNatureza possui Pasta Clones Cogumelos, opcional. Sem referencia explicita, encontra Clone Cogumelo na mesma cena; se nao existir, cria a pasta, preferindo deixa-la sob COGUMELO. SetParent preserva a pose mundial. Copias restauradas do save tambem sao organizadas, sem tirar itens do inventario. Nenhuma escala ou posicao original da cena foi alterada.

## Protecao de carregamento e compatibilidade
O processamento dos cogumelos fica suspenso durante carga e apos falha. Os pontos originais sao capturados no Awake do gerenciador, antes de o carregamento remover/mover as instancias originais. Uma copia ja restaurada pode ocupar seu ponto mesmo antes de receber Start, evitando agendamento fantasma por ordem de callbacks.

Save 0.7-pontos-cogumelos guarda ponto, rotacao, configuracao, flag de coleta e flag de reposicao. Versoes ate 0.6 continuam aceitas. Campos novos sao validados quanto a valores finitos/limites tanto na validacao do arquivo quanto no preflight de aplicacao. Prefab de estado novo precisa ter Cocumelo. Snapshots de rollback preservam os campos privados de respawn. As ancoras runtime nao sao campos serializados no prefab, para evitar gravar um ponto de uma instancia em todas as copias por Apply de alteracoes de Play.

Itens restaurados pelo inventario sao desarmados. Itens de cena usam o ponto salvo; para copias runtime antigas sem ponto confiavel, preserva-se o item utilizavel, mas sem permitir criar um ponto em zero/posicao temporaria de Instantiate. Originais da cena mantem seus pontos editoriais. Nao foram apagados clones antigos nem alterados saves reais. Um ponto vazio apos carregar pode iniciar uma nova espera; o tempo pendente/offline nao e persistido.

Os demais recursos continuam pelo caminho generico de respawn (tempo, offset, rotacao e inicializacao existentes). O manager cancela coroutines ao ser desativado e pode voltar a ser habilitado. Referencias de cogumelos destruidos sao removidas do registro, sem destruir itens vivos.

## IDs corrigidos
Tres prefabs tinham respawnId diferente da tabela: Mushroom Orange, Mushroom 01 e Mushroom 02. Corrigidos para Mushroom_Orange, Mushroom_01 e Mushroom_02. O getter reconhece os aliases antigos e remove espacos externos. Nomes de itens, GUIDs, dados de atributos e identidades individuais nao foram modificados.

## Arquivos principais
- Assets/Script/Natureza/Cocumelo.cs
- Assets/Script/Respawn/RespawnNatureza.cs
- Assets/Script/Save/ItemPersistente.cs
- Assets/Script/Save/SaveManager.cs
- Assets/Script/Save/SaveData.cs
- Assets/Script/Save/RecuperacaoItemCarga.cs
- Tres prefabs com IDs de respawn inconsistentes.

## Validacao
- Tests/CogumelosRespawn.Tests.ps1: 39 verificacoes executando as classes reais com relogio, coroutines e Instantiate simulados. Cobre limites, espera, pontos distintos, cancelamento/novo prazo, delay zero sem loop, elevacao sem acumulo, assentamento, folder, metadados, carga/falha, aliases e caminho generico de outros recursos.
- Tests/Save/InventarioEstadosIndividuais.Tests.ps1: 114 verificacoes aprovadas, com host atualizado para o campo Vector3 e a interface de Cocumelo.
- Tests/Save/EquipamentosVestidos.Tests.ps1: 29 verificacoes aprovadas.
- Total: 182 verificacoes/cenarios simulados.
- Compilacao pelos parametros reais do Unity sem erros, somente avisos CS0414 preexistentes. Diff check sem erros de whitespace.
- Nenhum commit criado. Play, fisica nativa, Unity JsonUtility e Meta Quest nao foram executados.

## Teste manual
1. Parar/reiniciar Play. Selecionar um cogumelo original e conferir Distancia Para Respawn = 1, Tempo Respawn = 30 e Altura Respawn = 0.5 no componente Cocumelo.
2. Afastar menos de 1 unidade: nao repoe. Afastar mais de 1: antes de 30s nao nasce; apos 30s nasce uma unidade em X/Z originais e Y original + 0.5.
3. Conferir a pasta Clone Cogumelo. Coletar esse clone e repetir: o proximo nasce na mesma altura, sem somar outros 0.5.
4. Retornar o item antes do prazo e afasta-lo novamente: deve esperar um prazo novo completo.
5. Coletar dois tipos e dois locais do mesmo tipo: cada ponto deve repor uma unidade.
6. Guardar, salvar e reiniciar: pilhas/IDs continuam corretos, cogumelos coletados nao criam farms extras; pontos vazios podem repor apos nova espera. Clones antigos permanecem itens existentes.
7. Conferir assentamento e colisao real no terreno e testar no Quest. Elevar o nascimento evita iniciar dentro da malha, mas a fisica precisa de verificacao nativa.
