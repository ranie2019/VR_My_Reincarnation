# Revisao dos paineis do ferreiro

Escopo: saudacao, categorias, criar, reparar, melhorar e missao no prefab Ferreiro e respectivas referencias na Cena Principal. Preservadas as alteracoes anteriores do projeto e os saves reais.

## Falhas e correcoes

- NPCPopup e FerreiroUI controlavam a mesma interface por caminhos diferentes. NPCPopup delega abertura ao FerreiroUI quando este existe, mantendo o comportamento antigo nos NPCs sem FerreiroUI. Seu Update legado nao executa confirmacao/timer concorrentes neste caso. Os botoes Yes do prefab ja chamam FerreiroUI.OnClickYes.
- NPCMissao podia abrir a missao diretamente ao passar o laser no ferreiro, antes da escolha da categoria. Quando existe FerreiroUI, o hover abre apenas a saudacao. NPCs sem essa interface mantem a abertura direta anterior.
- FerreiroUI fechava incondicionalmente apos WaitForSeconds, mesmo durante o uso. Agora registra atividade de hover nos interactables ativos e equipamentos selecionados nos sockets. Usa o tempo desde a ultima atividade. Equipamento no socket mantem o atendimento aberto. O fechamento por X continua permitido.
- CriarItem e GerenciadorMissoes mantinham timers independentes. No ferreiro, usam a autoridade de FerreiroUI. Fora do ferreiro preservam seus timers. BotaoCriarItem informa atividade ao CriarItem tambem para uso independente.
- BotaoX desativava somente o Canvas e deixava referencias/timer/missoes pendentes. Dentro do ferreiro chama OnClickFechar; demais interfaces continuam com o fechamento generico. AbrirPopupSaudacao verifica se painelAtivoAtual esta realmente ativo na hierarquia, evitando bloqueio quando a missao fecha o container externamente.
- BotaoCriarItem procurava primeiro qualquer CriarItem ativo da cena. Agora prefere o controlador do proprio painel, preservando referencia explicita e fallback antigo.
- Os dois slots de Melhorar tinham SlotInventario, com o mesmo bloqueio de inventario fechado identificado no reparo. Removidos somente esses componentes; mantidos sockets, triggers, visuais e transforms. O inventario do jogador nao foi alterado.
- Melhorar.cs esta vazio: nenhuma regra de melhoria/custo foi inventada. A confirmacao Button Atualizar foi desabilitada no Button, XRSimpleInteractable e BotaoMissao. Removida sua chamada persistente vazia, cujo alvo era um GameObject identificado incorretamente como Reparar. O painel permanece como visualizacao; ainda nao executa melhorias.
- Corrigidas oito referencias m_ParentInteractableObject que apontavam para o proprio componente XR no prefab. Nenhum collider compartilhado entre interactables foi identificado na verificacao do prefab.

## Validacao

- FerreiroPaineis.Tests.ps1: 12 verificacoes da classe FerreiroUI real em host simulado (navegacao, missao, fechar, estado externo, hover, equipamento no socket e inatividade). Verificacao adicional do prefab: ausencia de SlotInventario, confirmacao pendente desabilitada e ausencia de parent XR autorreferente.
- FerreiroTransacao: 17 verificacoes aprovadas.
- MissoesMateriais: 10 verificacoes aprovadas.
- RecuperacaoMissoes: 3 cenarios aprovados.
- ReparoSocket: 12 verificacoes aprovadas.
- Total: 54 verificacoes/cenarios em hosts simulados, mais verificacoes estruturais do prefab.
- Compilacao via response file real do Unity sem erros, somente avisos CS0414 preexistentes. Diff check sem erros de whitespace. Nenhum commit criado.
- A Cena Principal nao possui overrides para os componentes desabilitados de confirmacao de Melhorar nem para os dois SlotInventario removidos.

## Conferencia no Unity ainda necessaria

1. Parar e reiniciar Play. Hover no ferreiro deve abrir apenas a saudacao; Yes abre categorias sem conteudo antigo. Testar X e reabertura em cada painel.
2. Alternar Criar/Reparar/Melhorar/Missao: somente o conteudo escolhido deve permanecer aberto; sair de Missao deve fechar seus popups internos.
3. Manter laser num botao ativo por mais de 30 segundos: atendimento permanece aberto. Afastar laser e aguardar o periodo de inatividade: fecha.
4. Colocar equipamento no reparo, aguardar mais de 30 segundos, reparar e retirar; conferir preco, desconto unico, durabilidade e soltura. O atendimento nao fecha sozinho com equipamento selecionado.
5. Criar item com recursos suficientes e conferir consumo por unidade; sem recursos, confirmar que o botao fica bloqueado.
6. Missao: aceitar, reabrir, acompanhar progresso, entregar uma vez e conferir materiais/recompensa. Conferir tambem o Sacerdote, que mantem o fluxo direto sem FerreiroUI.
7. Melhorar: visualizar painel e retirar os itens manualmente; confirmacao permanece indisponivel ate existir implementacao. Nenhum custo ou bonus deve ser aplicado.

Renderizacao, ordem real dos callbacks XR e interacao fisica exigem Play/Quest e nao foram validadas nativamente nesta revisao.
