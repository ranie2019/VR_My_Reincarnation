# Menu, rolagem, escala e persistencia do inventario

## Evidencias

- Video fornecido: 64,6 segundos. Quadros amostrados mostram inventario aberto, controles proximos das setas e madeira guardada.
- StatusVR preservava a posicao/rotacao resultante do pai em cada frame, em vez da pose de abertura no mundo. Girar o Player deslocava o Canvas filho.
- BotaoRolagemInventarioVR procurava interactors em todos os descendentes da raiz Player e repetia a rolagem em OnTriggerStay. Isso qualificava colliders que nao pertenciam a uma mao.
- SlotInventario ignorava uma nova verificacao dos limites quando a escala numerica coincidia com o cache. PosicionarItemNoPontoDoSlot realinha o pivot a cada frame; o centro visual de uma flecha pode nao coincidir com esse pivot.
- O log real registrou recuperacao de backup por `Restauracao de cena ambigua: ITEM_00ec612f560b4ddd91d2b1d3db3afe37`. O registro era Wood sem origem de cena. O arquivo preservado continha quatro entradas de inventario; o save atual consultado continha uma. Isso comprova recuperacao de uma geracao diferente, sem provar que todos os itens relatados foram apagados.

## Correcoes

- Menu aberto preserva X/Z e rotacao no mundo, acompanha altura do headset e volta a ser posicionado a frente quando reaberto.
- Rolagem rejeita a propria UI, corpo do Player e componentes genericos de pose/transformacao. Nao procura mais interactor em outro ramo da raiz. Repeticao por permanencia desativada nos dois prefabs Player e na cena atual.
- Itens com escala em cache continuam sujeitos ao limite real do slot. O centro visual e corrigido antes de recalcular a reducao. Nenhuma dimensao ou Transform dos slots foi alterado por esta correcao.
- Na resolucao de um item salvo sem origem, originais com origem comprovadamente reservada por outro registro do save nao sao adotados nem bloqueiam o clone. Objetos desconhecidos, candidatos ambiguos e IDs duplicados continuam protegidos. A regra vale tanto no preflight quanto na aplicacao.
- Alteracoes na quantidade dos slots solicitam save com atraso de um segundo. O agendamento aguarda transacoes criticas e chama o mesmo SalvarJogo protegido. Carga malsucedida continua impedindo gravacao. Pause/Quit permanecem ativos.

## Validacao

- Compilacao C# completa utilizando as referencias do Unity: zero erros.
- 206 verificacoes em hosts simulados: pose do menu (6), filtro XR (5), resolucao de clone/origem reservada (8), autosave (9), origem de cena (7), equipamentos vestidos (29), estados individuais do inventario (114), abas aninhadas (6) e arquivos seguros (22).
- Nao foram alterados saves reais, restauradas geracoes antigas, criados commits ou executados testes PlayMode/Quest.

## Teste no Unity/Quest

1. Sair do Play, aguardar recompilacao e abrir novamente. Conferir ausencia de erro de carregamento; se houver recuperacao de backup, guardar a mensagem completa.
2. Abrir inventario e girar o Player nos dois sentidos. Menu deve conservar a pose de abertura. Sentar/levantar deve preservar o acompanhamento vertical.
3. Manter corpo/objetos proximos das setas: nao deve haver rolagem. Tocar com a mao uma vez: rola uma linha; permanecer encostado nao deve repetir. Testar ambas as maos.
4. Guardar a mesma flecha em slots de linhas/colunas diferentes, rolar e girar o Player. Conferir centralizacao, limite visual e escala normal ao retirar.
5. Guardar materiais em varias pilhas, aguardar ao menos dois segundos, fechar inventario e sair. Reiniciar e conferir quantidades, ordem de retirada, atributos e durabilidade.
6. Repetir no Quest, incluindo suspender/retomar. Encerramento forcado antes da gravacao nao possui garantia de persistencia.
