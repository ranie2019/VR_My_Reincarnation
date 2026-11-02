# Popup do elmo sem torcao

O prefab Helmet Black possui escala de raiz (1.6097145, 1.2402285, 1.2083441), enquanto o popup recebia rotacao global da camera e continuava como descendente desta raiz. A combinacao de escala desigual e rotacao introduz cisalhamento: os eixos do painel deixam de ser ortogonais ao inclinar a camera. O prefab tambem usava rotacaoPopupNaCamera Y=0, enquanto a luva usa Y=180, explicando a leitura espelhada. Nao foi alterada a escala do modelo, do jogador ou de slots.

EquiparElmo captura a pose local e dimensoes mundiais originais do popup. Ao abrir, liga explicitamente os botoes ao elmo, coloca o painel em uma raiz temporaria com escala unitaria e sem pai, e continua usando LayoutPopupsEquipamentos para acompanhar a camera e organizar lateralmente os painéis. A raiz pertence a mesma cena do dono e nao e salva. Nao copia nem recria textos ou dados do item.

Ao fechar, desativar, equipar ou destruir, o painel e oculto, sai da fila e retorna a pose/localScale originais. A raiz temporaria e destruida com o dono. SlotInventario recebeu apenas a propriedade de leitura VisualEstaDisponivel; permite fechar um popup destacado quando seu item fica oculto na rolagem/pilha ou o inventario fecha. O vinculo explicito BotaoEquipar.DefinirElmoDoPopup evita perder a referencia do equipamento quando o Awake do botao ocorrer somente depois da separacao da hierarquia.

O prefab Helmet Black teve somente Y da rotacao do popup alterado de 0 para 180. A rotacao padrao para novos EquiparElmo tambem usa Y=180; os demais ajustes manuais continuam disponiveis. A escala e PosicaoEquipe do equipamento nao foram alteradas. Escala/orientacao interna de paineis filhos com modificacoes independentes continua sujeita a configuracao do usuario.

Validacao: compilacao com o response file real do Unity sem erros, avisos CS0414 existentes; 9 testes de isolamento/reabertura/restauracao/vinculo do botao e regressao numerica do cisalhamento; 23 testes de ocultacao local por camera; 35 testes das linhas e comparacao. Hosts simulados, sem renderizacao ou ciclo de vida nativo de UI/XR. Nao foram alterados saves nem criados commits.

No Play/Quest: girar a cabeca em yaw/pitch/roll; abrir popup junto com luvas e peitoral; fechar/reabrir; clicar Equipar na primeira abertura; guardar e fechar o inventario com popup aberto; rolar para ocultar o slot; destruir/trocar elmo; verificar ausencia de popup orfao e preservacao da ocultacao local. Reiniciar o Play para testar a inicializacao limpa.
