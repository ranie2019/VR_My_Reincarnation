# Correcao da inicializacao dos cogumelos

## Causa e tratamento

O save inspecionado continha centenas de copias antigas com ponto de reposicao (0,0,0), marcadas como coletadas/reposicao e sem origem de cena ou coleta confirmada. A restauracao dessas copias explicava a pilha imediatamente ao iniciar. Corrigir apenas o temporizador nao corrigia esses registros persistidos.

SaveManager agora isola essa assinatura especifica antes de procurar prefab ou instanciar. Preserva os registros brutos e IDs em quarentena nas capturas seguintes, sem os exibir automaticamente. Pontos originais conhecidos, equipamentos vestidos e registros do inventario nao sao descartados por essa migracao. O arquivo de save do jogador nao foi editado manualmente.

## Reposicao e hierarquia

Respawn exige coleta confirmada por interacao do jogador ou entrada no inventario. Movimento causado apenas pela fisica e ponto vazio nao iniciam uma cadeia de clones. A distancia padrao e 1 metro, espera publica de 30 segundos e nascimento 0.5 metro acima do ponto original. Um retorno ao ponto cancela a espera. Pendencias confirmadas sao persistidas; apos carregar, a espera recomeca se o ponto continuar vazio.

A cena referencia diretamente ---STATIC---/NATUREZA/COGUMELO/Clone Cogumelo. A busca alternativa aceita diferencas de maiusculas e o nome antigo CAGUMELO, mas exige a pasta de clones dentro do grupo correto. Nenhuma pasta e criada. Sem a pasta existente, o nascimento e interrompido com um diagnostico unico. Originais ficam no grupo e reposicoes na pasta de clones; itens guardados continuam sob controle do inventario.

Foi removido somente o bit BatchingStatic dos prefabs de cogumelos e overrides correspondentes. Colecionaveis moveis nao devem usar as malhas combinadas estaticamente vistas na imagem. As posicoes e escalas dos slots nao foram alteradas.

## Validacao

- Compilacao da Assembly-CSharp com referencias do projeto: sem erros; avisos de campos nao utilizados.
- Respawn: 45 verificacoes em host simulado.
- Classificador de migracao: 9 verificacoes.
- Persistencia individual do inventario: 114 verificacoes.
- Equipamentos vestidos: 29 verificacoes.
- Unity Play, fisica real, JsonUtility e Quest ainda precisam de validacao nativa.

## Teste no Unity

1. Encerrar Play e iniciar novamente para carregar as alteracoes e a cena.
2. Sem tocar nos cogumelos, aguardar mais de 30 segundos: nao deve nascer reposicao.
3. Coletar um cogumelo e afasta-lo mais de 1 metro: deve surgir uma unica reposicao apos 30 segundos no ponto original mais 0.5 em Y.
4. Conferir que a reposicao esta na pasta existente e que nenhuma pasta nova apareceu.
5. Repetir guardando no inventario, retornando antes do prazo e reiniciando durante uma espera.
6. Conferir quantidades, retirada do inventario e equipamentos apos salvar/carregar.


## Revisao: mundo definido pela cena, apenas inventario persistente

A regra foi substituida a pedido do usuario: nenhum cogumelo solto no mundo, original ou clone, deve reaparecer por restauracao de save. O filtro RemoverCogumelosDoMundoSalvo remove esses registros do documento carregado antes da validacao de aplicacao. Reconhece metadados, quarentena antiga, tipos conhecidos da cena e prefabs com Cocumelo. A lista do inventario permanece intacta.

As capturas de cena deixam de incluir qualquer ItemPersistente com Cocumelo e de anexar os registros antigos isolados. O proximo salvamento normal grava o documento limpo; nenhum arquivo do usuario foi reescrito manualmente. Backups continuam sob a politica existente e passam pelo mesmo filtro ao carregar.

Pendencias antigas de respawn tambem sao ignoradas e nao sao mais gravadas, evitando nascimentos por coordenadas anteriores. Assim, as poses atuais dos cogumelos originais na cena nao recebem os Transforms do save; editar o layout nao exige apagar o inventario. A fisica configurada nos prefabs permanece inalterada.

Validacao desta revisao: compilacao sem erros, 7 testes do filtro real, 45 testes de respawn, 29 de equipamentos vestidos e 114 de persistencia individual em hosts simulados. O filtro verifica preservacao do inventario, demais equipamentos/sistemas, idempotencia e descarte de temporizadores antigos. A pasta Clone Cogumelo da cena em disco foi conferida vazia. Play/Quest ainda precisam de teste nativo.
