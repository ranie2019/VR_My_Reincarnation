# Atributos das manoplas

O componente deve ficar na raiz do prefab, junto de EquiparManoplas e ItemPersistente. O nome da classe C# foi corrigido de Atributos para AtributosManoplas, conservando o arquivo .meta existente.

## Configuracao

1. Informe Prefab Objeto e Descricao.
2. Em Classes existem oito entradas: Comum, Incomum, Raro, Epico, Unico, Lendario, Mitico e Divino. Zero desativa; chances positivas tem limite efetivo de 0,01 a 100. Valores iniciais ilustrativos: 70, 25, 5 e zero nas demais.
3. Cada classe tem Quantidade Minima/Maxima e uma lista Atributos. Retire da lista os atributos que esse modelo de manopla nao deve ter e configure Minimo/Maximo de cada entrada. Quantidade Maxima nao pode superar o tamanho da lista; tipos repetidos sao rejeitados. Os limites inteiros sao inclusivos, e atributos selecionados nao se repetem.
4. As quantidades iniciais seguem 0-1, 1-2, 2-3, 3-4, 4-5 e 6-8. Mitico e Divino usam inicialmente todos os 15 atributos disponiveis. Estes numeros e as faixas 1-5 sao pontos de partida editaveis, nao balanceamento final.
5. Associe o Popup existente e os TMP_Text de nome, classe e descricao. Textos Valores possui cinco linhas fixas: Elemento 0 e o topo, seguido dos Elementos 1 a 4. Em cada linha associe Texto Nome e Texto Valor; o tipo do atributo vem do resultado sorteado, sem dropdown de tipo nesta parte. As linhas sao preenchidas em ordem e sobras sao limpas. O campo antigo Texto e migrado para Texto Valor; as referencias dos primeiros cinco elementos sao preservadas. O texto agregado Texto Atributos foi removido. O componente apenas preenche textos; EquiparManoplas continua controlando abertura, tempo e organizacao lateral. Associe o mesmo popup nos dois componentes. O limite visual de cinco nao altera atributos/bonus persistidos quando um item possui mais de cinco.
6. Aplique a configuracao ao prefab para alcancar futuros clones. Nao alteramos prefabs/cena automaticamente.

## Sorteio e identidade

Cada classe faz sua tentativa independente. Quando varias passam, o desempate e uniforme. Rodadas sem vencedores sao descartadas por um algoritmo condicionado equivalente a repetir a rodada ate obter pelo menos uma aprovacao, sem um loop ilimitado no frame. As porcentagens configuradas sao chances de passar a tentativa, nao a frequencia final depois dos desempates. Todas zero e configuracoes invalidas geram erro explicativo e nao geram um item com resultado falso.

Awake gera uma vez por instancia, e hover/equip cobrem o caso de uso antes do primeiro Start. Reativar e reequipar nao mudam o resultado. ItemPersistente grava o resultado nos extras individuais com um envelope versionado que conserva os extras anteriores. A restauracao nao depende das chances/faixas atuais: reaplica o resultado salvo. Saves antigos sem atributos permanecem aceitos; recebem seu primeiro resultado depois da verificacao sincronica. Cada unidade da pilha conserva seu proprio resultado, sem compartilhar o sorteio por prefab.

StatusPlayer consulta somente as manoplas atuais marcadas JaEquipado no gerenciador do mesmo jogador. Os bonus existentes sao preservados. GetBonus* fornece a soma e Get*Total fornece base mais bonus, aproveitando os campos verdes existentes. Nao soma sobre os campos base nem grava bonus derivados no save do jogador. O peitoral fornece sua parcela por AtributosPeitoral; este componente continua exclusivo das manoplas.

Afinidade Divina e sorteada automaticamente, 50% Luz e 50% Trevas, apenas depois de sair Divino. Nao ha seletor manual ou campo de cor; a cor dos textos fica sob controle do proprio popup. Um campo interno legado de cor permanece apenas para conservar o JSON dos saves existentes. Bloqueio por alinhamento do jogador, passivas, conjuntos, multiplicadores e habilidades continuam fora desta implementacao; falta um modelo de afinidade do jogador. O componente nao implementa persistencia do slot de equipamento vestido, que continua com as limitacoes anteriores do sistema de equipamentos.

## Verificacao

Tests/AtributosManoplas.Tests.ps1 executa o algoritmo real extraido e os getters reais em um host isolado: sorteio com zeros, chance minima, empate de dois/tres, distribuicao de 70/5, soma 21+5+3=29, retirada, leituras repetidas e preservacao de bonus anteriores.
Tests/Save/InventarioEstadosIndividuais.Tests.ps1 verifica o ItemPersistente/DTO reais em host simulado, incluindo extras de duas manoplas, retorno ao mundo, migracao legada e invariancia do envelope durante verificacao.
Estes testes nao executam JsonUtility nativo, ciclo de vida, UI ou XR. Validar no Unity e Quest: instanciar dois clones, conferir limites/classes, abrir os popups juntos, equipar ambas, retirar uma, guardar e carregar a pilha, salvar/carregar item solto, testar save legado e todas chances zero. Nao foram alterados saves reais nem criados commits.

O Inspector mostra uma explicacao fixa acima de Quantidade Minima/Maxima: esses campos controlam quantos tipos sao sorteados. Os Minimo/Maximo internos de cada tipo controlam os pontos. Tests/PopupAtributosManoplas.Tests.ps1 valida o preenchimento real em host simulado, incluindo ordem, limpeza, referencias ausentes, migracao da lista e limite apenas visual.

## Comparacao por linha

Cada elemento de Textos Valores possui tambem Seta Up, Seta Down, Texto Diferenca, Sinal Positivo e Sinal Negativo. Associe GameObjects individuais para setas/sinais (nao o popup inteiro) e um TMP_Text para a magnitude. O usuario define visual e texto dos sinais +/-. A magnitude nunca inclui o sinal: 7 contra 5 mostra numero 2 com positivo/up; 3 contra 5 mostra 2 com negativo/down; empate mostra 0 sem sinais ou setas. Linhas vazias limpam tudo.

A comparacao usa somente a manopla equipada do mesmo lado, identificada por EquiparManoplas.LadoMao, via gerenciador ancestral ou gerenciador singleton do jogador local. Mao vazia equivale a zero; atributo ausente em uma manopla com dados vale zero. Ausencia de gerenciador, componente de equipamento ou dados gerados da manopla atual oculta a comparacao. O proprio item equipado compara consigo e mostra empate. Nao altera sorteio, save, quantidades ou bonus. LateUpdate atualiza as linhas enquanto o popup estiver aberto para refletir trocas em tempo real. Testes simulados cobrem valores maior/menor/igual, tipos ausentes, referencias faltantes e limpeza. Validar no Unity as referencias de cada linha, ambas as maos, troca com popup aberto e fechamento/reabertura.

## Durabilidade das manoplas

Em Durabilidade / vida da manopla, configure Vida Maxima (padrao 100), Vida Atual (100) e Desgaste Natural Por Minuto (0,5). Associe Texto Durabilidade Atual e Texto Durabilidade Total aos dois TMP_Text do popup. Valores fracionarios sao exibidos com ate duas casas em pt-BR, sem arredondar a vida real para inteiro.

O desgaste natural ocorre apenas durante tempo de jogo enquanto equipada, nunca dentro do inventario ou solta. Pausa com Time.timeScale zero nao conta e nao existe desgaste offline. Um acumulador double evita que subtracoes pequenas por frame desaparecam devido a precisao float. Ao chegar a zero a raiz da manopla e desativada e destruida; a mao nua e restaurada ao estado anterior e o gerenciador deixa de contabilizar seus bonus. O consumo permanente e registrado para nao reaparecer como original da cena.

O ItemPersistente reconhece a durabilidade float do componente e salva/restaura atual/maxima por unidade da pilha. Para itens soltos, um marcador opcional no estado de cena conserva tambem a maxima. Saves anteriores sem durabilidade de manopla sao aceitos e recebem os valores configurados no prefab, sem refazer atributos ja sorteados. O Reparar reconhece AtributosManoplas e usa RepararCompleto. Nao adicionar Equipamento generico para fazer a manopla durar: isso criaria dois controladores de vida; a logica nova fica no proprio AtributosManoplas.

Foi preparado AplicarDesgasteCombate(quantidade), mas nenhum evento de combate o chama automaticamente. O custo e o evento adequado de combate permanecem para uma proxima etapa; nao existe penalidade duplicada por contato/colisao.

Tests/DurabilidadeManoplas.Tests.ps1 exercita os metodos reais em host simulado: 0,5/min, equivalencia entre um passo de 60 s e 3600 frames, inventario/objeto solto, reparo, valores invalidos e destruicao unica. Os testes de estado individual cobrem durabilidade fracionaria, maxima e migracao legada. Ainda testar no Unity/Quest: uma luva equipada por dois minutos perde 1; guardar pausa desgaste; reparar restaura; quebra libera somente a mao correta e remove bonus; recarregar mantem os valores individuais.
