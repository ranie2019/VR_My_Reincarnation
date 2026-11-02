# AtributosElmo

Implementado no script existente, preservando o .meta. Mesmo comportamento das manoplas e peitoral: configuracao independente de raridades/chances, faixas e quantidade de atributos por classe; Divino sorteia Luz/Trevas 50/50; cinco linhas com nome/valor e indicadores de diferenca; durabilidade atual/maxima, desgaste natural de 0,5 por minuto equipado, reparo e quebra. Efeitos passivos, bloqueio por afinidade e persistencia do slot vestido continuam fora de escopo.

## Configurar

1. Na raiz do prefab, usar AtributosElmo junto de EquiparElmo e ItemPersistente. Nao adicionar Equipamento generico para controlar a mesma durabilidade.
2. Associar Prefab Objeto, Descricao e chances/faixas de cada raridade.
3. Associar o mesmo PopUp usado em EquiparElmo, textos gerais e os cinco elementos ordenados do topo para baixo. Cada linha recebe nome, valor, setas, magnitude da diferenca e sinais.
4. Associar Texto Durabilidade Atual/Total. Valores iniciais de vida: 100/100; desgaste: 0,5/min. Guardado ou solto nao sofre desgaste natural. Aplicar configuracao ao prefab para novos clones.

A comparacao considera somente ElmoAtual do gerenciador local, incluindo perdas de atributos ausentes na candidata. Os bonus somam-se aos de peitoral e ambas as manoplas no StatusPlayer, sem alterar valores base. A ocultacao da cabeca nao foi adicionada: a cabeca do avatar permanece visivel. O sistema de ocultacao do elmo somente para a camera proprietaria continua funcionando; a quebra encerra seus callbacks e libera a referencia do gerenciador antes de destruir a raiz.

Persistencia: envelope proprio my-reincarnation.elmo.v1 para resultado individual. Vida atual/maxima sao preservadas no inventario e na cena; campos opcionais mantem compatibilidade com saves anteriores. Preflight valida componente/payload antes de aplicar. Reparar e CriarItem reconhecem AtributosElmo. Algoritmos de sorteio/afinidade/desgaste delegam aos metodos compartilhados usados pelas manoplas. Nao foram editados prefabs, cenas ou saves reais, nem criados commits.

## Validacao

Compilacao pelo response file real do Unity, sem fontes adicionadas manualmente. Testes simulados: 114 verificacoes de estados individuais (incluindo elmo, peitoral e manoplas), 112 de bonus combinados, 1011 de sorteio, 33 das linhas do elmo, 11 de desgaste do elmo, 23 de ocultacao por camera e 17 de consumo/criacao. Nao executam renderizacao, callbacks, JsonUtility ou XR nativos.

Validar no Play/Quest: comparar dois elmos; equipar com peitoral e luvas; retirar/trocar e conferir bonus; salvar/carregar guardado e solto; reparar; quebrar um elmo equipado e conferir ausencia de bonus e de referencia; verificar que a cabeca permanece visivel e que o elmo equipado continua oculto apenas na camera local, aparecendo em cameras secundarias/espelho.
