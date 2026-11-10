# Auditoria do Cajado de Ferro — 06/11/2026

## Componentes
- CajadoMagico: selecao, desenho/reconhecimento runico, preparacao/lancamento, direcao e mana.
- CajadoMagicoVoz: permissao/microfone/Voice SDK, sessao da requisicao, cancelamento, normalizacao e eventos de transcricao.
- ComandosMagiaVoz: dicionario portugues/ingles, tolerancia fonica, identificacao de magia e solicitacao ao CajadoMagico.
- CajadoPopup: dados individuais, sorteios, persistencia, bonus enquanto empunhado e popup via nucleo compartilhado Espada.

Os dois componentes de voz nao executam a mesma tarefa. A captura nao conjura; o interpretador nao abre o microfone. Remover um quebraria a cadeia atual e suas referencias serializadas. Foram mantidos nomes, GUIDs, eventos publicos, comandos e referencias do prefab. Nenhuma cena/prefab/save foi editado.

## Falhas corrigidas
1. Texto era entregue por OnPartial/OnFull da requisicao e tambem por VoiceEvents globais, sem ID de sessao nos globais. Agora transcricoes usam apenas os eventos da requisicao com sessao; eventos globais de diagnostico de audio/erro permanecem. Callback final antigo tambem deixa de alterar o diagnostico antes de validar a sessao.
2. Repeticao era comparada pela frase (fireball e bola de fogo podiam contornar o intervalo), e um final atrasado podia repetir uma parcial depois do cooldown. Agora a chave e o ID da magia e a sessao reconhecida. Nova fala pode executar apos o intervalo.
3. Tentativa sem sucesso consumia o cooldown. Agora o comando e marcado aceito somente se a preparacao retorna sucesso; bloqueios de mana continuam no nucleo e nao receberam novas formulas.
4. Metodos publicos do interpretador podiam executar apesar do componente ou da magia/voz estarem desativados. Agora esses estados sao verificados, alem das protecoes existentes de selecao/desenho/inventario.
5. Trocar a referencia do componente de voz podia deixar listeners na fonte anterior. O interpretador armazena a fonte efetivamente registrada para remover/revincular corretamente e impedir inscricoes duplicadas.
6. O reconhecedor reunia palavras distantes e aceitava substring interna. Agora procura sequencias contiguas de palavras, preservando tolerancia fonica por palavra. Isso nao representa um interpretador semantico de negacoes ou conversacao.
7. Excecao em Activate deixava o dono global reservado. O tratamento de falha cancela/libera o estado local e informa erro, sem consumir mana.
8. CajadoPopup exibia configuracoes herdadas de golpe de espada sem uso (Forca, perfil fisico, tags, cooldown e audio de hit). Inspector especifico oculta esses campos; dados serializados e audio de quebra permanecem. Nao ha migracao de dados de prefab.

## Limites preservados
O ataque sorteado do CajadoPopup e um dado do item e ainda nao alimenta o dano de BolaDeFogo/EfeitoFogoArea. A restricao do popup ainda nao bloqueia o lancamento. Isso ja estava explicitado na implementacao anterior; esta revisao nao inventa uma nova formula de dano ou rebalanceia magias. Cai no escopo de uma integracao de atributos e combate magico. Runas e mana continuam sob CajadoMagico. Por sua vez, os bonus gerais dos atributos continuam sendo somados pelo nucleo de equipamentos.

## Validacao
46 scripts de testes passaram. O teste novo inclui 18 casos do interpretador real: parcial/final, aliases PT/EN, repeticao por sessao, nova fala, falha e nova tentativa, componentes desativados, mao/desenho, listeners repetidos e troca de fonte, palavras contiguas e substring sem tolerancia. Checagem adicional garante uma origem de texto e preserva callbacks por requisicao. Apos a ultima mudanca do reconhecedor, seus testes foram repetidos. Compilacoes runtime e Editor completas sem erros.

Hosts simulados nao comprovam comportamento do microfone real, atrasos do SDK, permissao Android ou cancelamento durante transporte. Nao foi executado PlayMode/Quest.

## Testes manuais
1. Segurar cajado e falar bola de fogo/fireball; conferir uma preparacao e um unico consumo de mana por fala.
2. Receber parcial, lancar rapidamente e esperar o final; ele nao deve preparar outra magia na mesma sessao.
3. Falar novamente apos a reativacao e o cooldown; deve funcionar.
4. Soltar/guardar ou trocar o cajado enquanto o SDK responde; nenhuma magia deve ser criada pela resposta antiga.
5. Desativar/reabilitar interpretador e componente de voz; repetir fala sem listeners extras.
6. Testar sem mana, recuperar mana e tentar novamente; nao deve perder mana nem manter um cooldown de tentativa frustrada.
7. Desenhar runa e testar voz; preservar a prioridade do desenho e comportamento existente.
8. Testar popup, inventario, save, reparo e desgaste independentemente de audio.
