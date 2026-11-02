# Slot de reparo do ferreiro

## Causa confirmada no prefab
Assets/NPC/Ferreiro/Prefab/Ferreiro.prefab tinha SlotInventario (6614907856614819496) no mesmo objeto Slot (3881159800803297619) do XRSocketInteractor de reparo. SlotInventario inicia com inventarioAberto falso e registra filtros de hover/selecao; o painel do ferreiro nao pertence ao InventarioVR do jogador e nao abre esse estado. A luva era rejeitada pelo filtro de inventario. Reparar.cs ja reconhecia AtributosManoplas, AtributosPeitoral e AtributosElmo como duraveis.

## Alteracoes
Removido apenas o componente SlotInventario desse slot de reparo no prefab. Socket XR, trigger, ponto de encaixe, visual e todos os transforms/escalas preservados. Nao foi alterado SlotInventario.cs nem o estado fechado por padrao dos slots do jogador.

Reparar.Update sincroniza tambem firstInteractableSelected para reconhecer selecoes anteriores ao Start ou a reabertura do painel, sem depender de o item virar filho do slot. Mantido fallback antigo por Transform para slots sem XR.

MoverItemParaSpawn encerra a selecao XR do proprio item antes de move-lo/solta-lo, evitando que o socket continue puxando o item reparado. Nao libera outro item selecionado. Preservados preco, pagamento, durabilidade, persistencia e tratamento de falha existentes. Ponto Spawn continua opcional como antes.

## Validacao
Tests/ReparoSocket.Tests.ps1: 12 verificacoes de metodos reais em host simulado (deteccao, preco, adapter de reparo, persistencia, outros equipamentos, liberacao XR antes do movimento, spawn/fisica, limpeza da UI e fallback). Verificacoes do prefab confirmam ausencia do controlador de inventario e preservacao do socket e trigger. Compilacao pelo response file do Unity passou sem erros; somente avisos CS0414 anteriores. Nenhum save real foi alterado.

## Teste no Play ainda necessario
Parar e reiniciar Play para aplicar a alteracao do prefab. Retirar uma luva danificada do inventario, segura-la e soltar no slot de reparo; conferir encaixe e preco. Pagar com saldo suficiente; conferir durabilidade completa, desconto unico e retirada/soltura do item. Testar tambem uma arma e abrir/fechar o inventario do jogador. Uma luva ainda vestida mantem as restricoes normais de equipamento: retirar antes de levar ao ferreiro.
