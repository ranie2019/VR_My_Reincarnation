# Popups de equipamentos lado a lado

## Causa
Luvas, elmo e peitoral ja chamavam LayoutPopupsEquipamentos, em EquiparManoplas.cs. O layout compartilhado usava apenas distancia fixa entre centros (padrao 0.45), sem considerar largura visual. Paineis maiores continuavam sobrepostos mesmo registrados juntos.

## Correcao
O organizador mede os cantos mundiais dos Graphics ativos (inclusive botoes), depois de aplicar a rotacao final, projetados no eixo direito da camera. Distribui os centros pelas larguras reais, mantendo folga de 0.03 unidade de mundo entre bordas. Espacamento Horizontal Popups continua como distancia minima entre centros: valores maiores configurados sao respeitados. O conjunto inteiro fica centralizado, corrigindo tambem pivots assimetricos. Ao fechar paineis, os restantes sao reorganizados; um unico painel fica centralizado. Cameras diferentes continuam independentes.

A alteracao e compartilhada pelos tres equipamentos, sem alterar escalas, prefabs, inventario, atributos, durabilidade, equipar ou a isolacao do popup do elmo.

## Validacao
Tests/LayoutPopupsEquipamentos.Tests.ps1: 11 verificacoes executando o organizador real em host simulado. Inclui larguras diferentes, tres tipos, pivot fora do centro, atualizacoes repetidas sem deriva, fechamento, cameras distintas, camera inclinada, espacamento maior e Graphic desativado. PopupElmoEscala: 9 aprovadas; ElmoVisibilidadeLocal: 23 aprovadas. Compilacao real via response file do Unity sem erros, com avisos CS0414 preexistentes. Renderizacao nativa nao executada.

No Play: abrir elmo e peitoral e depois ambas as luvas; verificar separacao lateral ao inclinar/girar a cabeca; fechar paineis ate sobrar um, que deve recentralizar. Conferir botoes e textos com o tamanho real dos prefabs.
