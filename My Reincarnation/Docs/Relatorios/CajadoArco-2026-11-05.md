# Arcopopup

Componente para arco e flechas conforme confirmado pelo usuario. Especializa o mesmo nucleo Espada usado por CajadoPopup: sorteio de classe, ataque, durabilidade e atributos, save individual, cinco linhas nome/numero, restricao visual, hover XRRay/NearFar, tempo publico 5 segundos, popup lateral e bloqueio enquanto empunhado. Nao adicionar Espada ou CajadoPopup no mesmo objeto.

Requisito inicial: Destreza 80%, Agilidade 20%, ajustavel em Chance Destreza. Faixa Requisito Minimo/Maximo compartilhada; perfil fisico herdado nao altera o tipo escolhido no arco. Classes superiores usam a configuracao manual. Ataque/requisito exibidos nao mudam o dano da flecha nem bloqueiam o disparo nesta etapa, seguindo a separacao adotada no CajadoPopup. Arco conserva corda, selecao de municao, velocidade de disparo e mecanica de flechas.

## Durabilidade unica
Quando Arcopopup esta presente, propriedades DurabilidadeAtual/Maxima e Quebrado de Arco consultam o nucleo compartilhado. Dano recebido e reparo delegam para esse nucleo. UI antiga de durabilidade nao cria textos concorrentes. Sem Arcopopup, Arco preserva seus campos e fluxo anterior. O componente e reconhecido no reparo; save ja encontra a classe derivada por Espada.

## Configurar e validar
Adicionar Arcopopup ao mesmo objeto raiz de Arco e do XRGrabInteractable, mantendo ItemPersistente/ItemInventarioDados configurados. Arrastar Popup, textos Nome/Classe/Descricao/Ataque/Durabilidade e cinco linhas. Definir chances e faixas do modelo. Criar duas unidades, conferir resultados individuais, pegar/soltar/guardar e conferir bonus. Salvar/reiniciar e conferir atributos e desgaste. Testar dano recebido, reparo, quebra, disparo, selecao de flechas e popup com inventario fechado. Nenhum prefab, cena ou save real foi editado.

Compilacao completa sem erros. Hosts simulados: 128 verificacoes de armas/cajado/arco, 7 de durabilidade compartilhada/legada, 12 de reparo e 121 de estados individuais. PlayMode/Quest nao executados.
