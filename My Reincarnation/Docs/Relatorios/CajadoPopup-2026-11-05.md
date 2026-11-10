# CajadoPopup

CajadoPopup especializa Espada para reutilizar um unico nucleo de sorteio, UI, bonus enquanto empunhado, desgaste natural, quebra, layout lateral, ferreiro e persistencia individual. Nao adicionar Espada e CajadoPopup juntos. Os callbacks herdados agora sao protegidos; as rotinas da espada mantem o comportamento anterior. Vida atual/maxima protegidas permitem acesso pelos adaptadores de durabilidade em componentes derivados. ItemPersistente resolve a arma por GetComponent<Espada>, cobrindo subclasses. O reparo reconhece CajadoPopup.

## Configurar
Adicionar CajadoPopup ao objeto raiz com o XRGrabInteractable, ItemPersistente e ItemInventarioDados existentes. Associar Popup, Camera, textos de nome/classe/descricao/ataque, atual/maximo de durabilidade e as cinco linhas nome/valor. Configurar chances das classes, faixas de atributos, ataque e durabilidade. Chance Inteligencia padrao 70%; restante Espirito. Classes iniciais usam Requisito Minimo/Maximo; Perfil Restricao fisico herdado nao altera os atributos magicos escolhidos no cajado. Classes superiores permanecem na restricao manual. Popup por laser XRRay/NearFar, 5 segundos configuraveis, bloqueado quando selecionado por mao/laser; sockets nao contam como empunhado. Sem comparacao nem botao equipar. Inventario continua fechando o popup pelo nucleo compartilhado.

## Dados e limites de escopo
Dados sorteados sao salvos pelo envelope de arma ja existente e restaurados por instancia, inclusive pilhas. Nao ha sorteio novo ao reabrir o popup. Bonus entram nos totais do player somente enquanto empunhado e saem ao guardar/soltar. Desgaste natural e quebra usam o nucleo de durabilidade; consumo permanente segue ItemPersistente. Ataque exibido e o valor base sorteado. Runas, voz, mana, dano das magias e seus criterios de lancamento nao foram modificados. O requisito exibido e informativo para o cajado nesta etapa: nao bloqueia CajadoMagico. Dano de contato do componente CajadoPopup e sempre zero, sem callbacks de golpe de espada. Integrar o ataque/requisito do item ao dano e lancamento dos feiticos exige uma alteracao especifica no sistema magico.

## Validacao
123 verificacoes de arma/cajado (geracao, requisito magico, idempotencia, roundtrip, contato zero); 121 de persistencia individual; 19 de ferreiro; 12 de socket/reparo; 7 da formula da espada. Hosts simulados; PlayMode/Quest nao executados. Compilacao completa com referencias Unity sem erros. Nenhum prefab/cena/save real foi alterado nesta implementacao.

## Testar em Play
Criar dois cajados novos e conferir faixas e independencia. Apontar laser, esperar fechamento e segurar para confirmar bloqueio. Guardar ambos com desgaste diferente, reiniciar e comparar valores. Conferir bonus verdes ao pegar/soltar e a soma com armaduras. Testar reparo e quebra. Confirmar que as runas/voz e feiticos mantem o funcionamento anterior.
