# Espada: popup, atributos e durabilidade

Implementacao concentrada em `Assets/Script/Armas/Espada.cs`. Nao e necessario adicionar um componente AtributosEspada.

## Configuracao no Inspector

- Em Classes, cada raridade possui chance independente (zero desativa), quantidade de atributos e lista de faixas por atributo. O desempate e a afinidade divina usam o mesmo sorteador das manoplas.
- Na secao Ataque, configurar Ataque Minimo/Maximo. Na secao Durabilidade, configurar Durabilidade Minima/Maxima. As faixas sao da arma, independentes da raridade; campos antigos por classe ficam ocultos para conservar a serializacao anterior. Novos sorteios usam as faixas dessas secoes.
- Em Restricao sorteada, escolher Perfil Restricao (Equilibrada/Pesada/Leve) e Requisito Minimo/Maximo para o patamar do modelo. Comum, Incomum e Raro sorteiam um unico requisito, independente da quantidade/valor dos bonus. Padrao inicial: Equilibrada, 5 a 8; os limites sao configuraveis e precisam de balanceamento em Play. Classes superiores usam Atributo Restricao e Valor Minimo Para Usar na secao manual. Associar os quatro textos: Texto Rotulo Restricao, Texto Atributo Restricao, Texto Valor Restricao e Texto Mensagem Restricao. Rotulo e mensagem podem ser traduzidos no Inspector. Valor igual ao requisito e suficiente. O bonus do atributo concedido pela propria espada e descontado dessa verificacao para impedir autoliberacao; os bonus enquanto empunhada continuam ativos conforme solicitado.
- Em Popup existente, associar o GameObject PopUp e os textos Nome, Classe e Descricao. O campo Texto Dano foi removido conforme solicitado; o ataque interno continua sorteado e o combate continua somando a Forca total do player.
- Manter Texto Valor Atual/Total associados aos dois numeros da durabilidade. Os nomes `Durabilidade atual`, `Durabilidade completa`, `Valor Atual` e `Valor Total` podem ser encontrados automaticamente dentro do popup.
- Textos Valores possui cinco linhas, de cima para baixo. Cada linha recebe somente texto do nome e texto do numero, sem comparacoes. Configurar essas referencias como nos equipamentos anteriores.
- Nao adicionar BotaoEquipar. Nenhum renderer do avatar e ocultado por esta implementacao. Efeito LED e criacao automatica do Canvas antigo foram removidos.

## Comportamento

- Cada instancia nova sorteia seu proprio resultado. Uma copia do resultado serializado no prefab nao e considerada um sorteio da nova instancia. Reabrir popup/reativar/guardar nao refaz valores validos.
- Bônus dos atributos contam somente enquanto empunhada por um player, inclusive duas armas diferentes. Inventario e sockets de reparo nao contam como empunhar. A formula anterior de defesa foi preservada.
- Popup abre somente por hover de XRRayInteractor (laser), excluindo gaze, mao direta e socket. Selecionar a arma com a mao/laser fecha imediatamente o painel. Enquanto houver selecao fora de socket, LateUpdate tambem impede sua exibicao. Soltar nao reabre o painel automaticamente.
- Popup usa o layout compartilhado com luvas, elmo e peitoral, fica isolado da escala nao uniforme do modelo enquanto aberto e e recolhido ao fechar/desativar/destruir. Fechar ou rolar o inventario oculta o popup de itens que deixaram de estar visiveis.
- O popup de arma nao compara atributos com outras armas. A lista mantem o limite de cinco atributos visiveis, cada um com nome e numero.
- Desgaste natural padrao: 0,5/minuto enquanto empunhada. Durabilidade agora aceita fracao; usa acumulador de precisao para evitar perder pequenos decrementos.
- Desgaste por acerto valido continua usando o campo existente. Chegar a zero registra consumo permanente, fecha popup, desativa e destroi a instancia. Reparo continua usando a durabilidade maxima individual.
- Save individual do inventario e estado no mundo preservam classe, afinidade, atributos, dano, vida maxima e vida atual. Nome/descricao sao editoriais e seguem a configuracao atual do componente/prefab.
- Saves anteriores sem atributos de espada conservam dano e durabilidade existentes, sem cura gratuita nem sorteio retroativo de bonus. A publicacao dos novos extras aguarda a verificacao da carga. Para testar os sorteios novos, criar espadas novas no ferreiro.

## Validacao

Compilacao C# completa com referencias do Unity: zero erros. Testes com metodos reais em hosts simulados:

- Espada: 116 verificacoes de sorteio, limites, independencia, serializacao, migracao legada, desgaste e bonus.
- Inventario individual: 121 verificacoes, incluindo duas espadas distintas e restauracao no mundo.
- Ferreiro: 19 verificacoes, incluindo devolucao dos materiais quando o sorteio da espada falha.
- Regressao: sorteio/soma de manoplas, HP/MP, formula da espada, receptores de combate e snapshots runtime passaram.

Nao foram alterados os saves reais nem executados testes PlayMode/Quest. Os testes simulados nao comprovam eventos XR, orientacao visual ou leitura no headset.

## Testar no Unity

1. Associar os textos/linhas ao novo popup e aplicar o prefab quando desejado. Configurar ataque e durabilidade nas secoes da arma, e perfil/faixa na secao de restricao sorteada; para classes superiores, atributo/valor na secao manual. A mensagem de valor insuficiente e exibida apenas quando o jogador conhecido esta abaixo do minimo; sem player conhecido, o grupo de restricao fica oculto. O grupo Restricao e procurado automaticamente entre os filhos do popup (inclusive inativos), podendo ser associado manualmente em Grupo Restricao. Todo o grupo e os quatro textos ficam ocultos quando o requisito e atendido e reaparecem se o atributo cair abaixo do minimo enquanto o popup estiver aberto. Essa validacao tambem ocorre antes da conclusao de uma migracao legada de atributos.
2. Criar cinco espadas; conferir faixas/classes/atributos. Resultados iguais por coincidencia sao permitidos.
3. Abrir popup de espada junto ao de armadura e inclinar a cabeca: conferir leitura e separacao lateral.
4. Empunhar/soltar/guardar e conferir os bonus verdes; testar duas maos e o slot do ferreiro.
5. Conferir desgaste natural, acerto valido, reparo e destruicao a zero. Arma guardada nao deve desgastar.
6. Com requisito 10, testar Forca 9 (nenhum dano), 10 e 11 (dano normal). Repetir com Velocidade, Resistencia e os demais atributos; HP/MP usam limites maximos. Repetir com bonus da propria espada para confirmar que ela nao libera seu proprio uso.
7. Guardar duas espadas com durabilidades diferentes, sair e carregar. Conferir valores individuais e ausencia de novo sorteio/cura.

## Restricoes por perfil — atualizacao

- Pesada: Forca 80%, Destreza 20%.
- Equilibrada: Forca 60%, Destreza 40%.
- Leve: Destreza 60%, Agilidade 25%, Velocidade 15%.
- Sao pesos exclusivos do requisito; o pool de bonus e a formula de dano/defesa permanecem iguais. FORCE no painel corresponde a Vigor, nao a Forca/Strength.
- Requisito e atributo sorteados ficam no snapshot individual e prevalecem sobre alteracoes futuras do prefab. Nao ha media de dano/HP/bonus nem escalonamento automatico por raridade.
- Snapshots antigos sem requisito continuam com a configuracao manual anterior, sem novo sorteio. Seu JSON original e preservado na exportacao para nao invalidar a verificacao transacional da restauracao. Saves ainda anteriores, sem snapshot de espada, conservam a migracao existente e recebem a restricao manual.
- Nenhuma cena, prefab, save real ou formula de combate foi alterado por esta atualizacao.
- Validacao: 116 verificacoes de espada, 121 de inventario individual, 19 do ferreiro, 7 da formula de dano e 9 de receptores/impacto; compilacao completa sem erros. Hosts simulados; falta PlayMode/Quest.
- Teste manual adicional: criar espadas das tres raridades iniciais em cada perfil, conferir atributo permitido/faixa, salvar/carregar e confirmar que o resultado nao muda. Modificar a faixa do prefab e verificar que apenas espadas novas adotam a nova faixa. Conferir classe superior com configuracao manual e espada antiga sem sorteio retroativo.
