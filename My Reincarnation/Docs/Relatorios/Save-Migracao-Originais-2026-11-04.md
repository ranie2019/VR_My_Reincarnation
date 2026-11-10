# Restauracao de originais com IDs regenerados

## Causa verificada
O save principal referencia a flecha ITEM_7056e8431eba452b814d63148be91c24, origem 63c3eb42ffdae9e1c12d2af1fe3e2cfd. Na Cena Principal atual, o prefab Flexa Simples possui instancia/origem 6dc1de5efe5c49a694e33fc363d9757a. O peitoral salvo tem origem e0503536589046fc875768a4243f6455, enquanto a instancia atual possui bac5d26f3c37485c9a59c663de1b4426. A busca por identidade falhava e o fallback recusava originais com IDs persistentes, bloqueando principal e backups.

## Correcao
SaveManager permite migracao conservadora quando a origem salva e valida, existe apenas um registro daquele tipo no save da cena e apenas um candidato disponivel do mesmo tipo. Objetos usados, guardados ou reservados por ID/origem para outros registros sao excluidos. A regra e usada tanto na validacao quanto na aplicacao. Mais de um candidato permanece bloqueado; nao se escolhe arbitrariamente nem se cria uma duplicata para contornar a ambiguidade. O estado individual e aplicado pelo caminho existente, preservando identidade salva, atributos e durabilidade.

## Validacao
18 verificacoes dos resolvedores reais de migracao e reservas; 7 por origem; 121 de inventario individual; 29 de equipamentos vestidos. Compilacao completa C# com referencias Unity: zero erros. Testes executados em hosts simulados, sem PlayMode ou Quest. Saves reais foram apenas lidos, sem edicao ou exclusao. A comparacao estatica confirmou um candidato persistente livre para cada um dos dois tipos envolvidos.

## Verificacao no Unity
Sair do Play e iniciar novamente apos a recompilacao. Conferir ausencia de bloqueio, inventario preservado, peitoral restaurado e ausencia de flecha/peitoral duplicados. Encerrar e iniciar novamente para confirmar persistencia das identidades restauradas. Se surgir outra falha, preservar o diagnostico completo: os demais bloqueios de integridade continuam ativos.
