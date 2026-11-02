# Correções dos bugs graves — 29/10/2026

## Resultado

Implementadas as correções autorizadas de persistência, economia, combate e interação VR. Nenhum commit ou push foi executado. Os preços, requisitos, multiplicadores e dimensões dos slots não foram alterados.

| Problema | Status da implementação |
|---|---|
| Confirmação prematura do inventário | Destruição adiada até a confirmação global; recuperação reforçada. Rollback global integral permanece parcial. |
| Itens consumidos reaparecendo | Registro explícito de origem e remoções permanentes, preservando recursos com respawn. |
| Reparo gratuito | Débito correto, verificação do reparo, estorno em falha e bloqueio de reentrada. |
| Missão sem materiais | Conferência atual, reserva reversível, consumo exato e recompensa somente após confirmação. |
| Flecha com dano duplicado | Flecha é responsável pelo impacto; Slime, Goblin e Player ignoram seu caminho paralelo de contato. |
| Magia atingindo inimigo errado | Busca somente nos ancestrais do Collider, com suporte a Slime, Goblin e Espírito. |
| Escala dos equipamentos | Luvas e elmo deixam o inventário controlar a escala enquanto armazenados; escala original capturada em Awake. |
| Input compartilhado | Controle de consumidores por ação, liberação no último consumidor e preservação de ações já habilitadas externamente. |

Todos os oito problemas exigiam mudanças no código atual. Proteções anteriores que já existiam e foram mantidas: bloqueio central do autosave após falha, validação dos dados individuais, limite de 99, ordem LIFO, filtro de flecha não lançada e trava de colisão já processada.

## Integridade dos dados

O adaptador guarda as mesmas instâncias antigas até o SaveManager confirmar o término da aplicação. Foram acrescentados snapshots de hierarquia, transforms, escala alvo XR, campos serializados, Rigidbody, Colliders, renderers e referências de seleção. As cópias temporárias são removidas na recuperação; uma falha continua bloqueando o salvamento.

O rollback dos itens e slots foi ampliado, incluindo o modo que acrescenta itens sem limpar o inventário. O jogador também possui tentativa de recuperação do estado anterior.

**Limitação:** isto não torna todos os sistemas participantes de uma transação integral. Efeitos externos de callbacks, missões e implementações arbitrárias de ISalvavel não têm um contrato geral de desfazer. Objetos destruídos por callbacks externos não podem ser recriados como a mesma instância. Uma recuperação incompleta deve ser tratada como falha, mantendo o save bloqueado. Essa reestruturação ampla ficou fora desta implementação, conforme solicitado.

A identificação de origem acompanha cada unidade da pilha e o estado de cena. O registro de remoções não apaga objetos por simples ausência de uma lista: acompanha originais conhecidos destruídos e o consumo explícito. Componentes Respawnitem e Cocumelo são preservados como recursos de respawn intencional.

O formato atual passou para **0.5-remocoes-permanentes**. A leitura das versões anteriores continua permitida. Na migração, a origem pode ser transferida do original já identificado para a cópia restaurada pelo mesmo GUID. Não é possível reconstruir retroativamente uma remoção que nunca foi registrada por um save antigo.

Se um original não possui GUID persistente, sua origem usa uma chave derivada da cena e da hierarquia. Renomear ou reorganizar esses objetos entre versões exige validar a migração. Objetos de cenas adicionais e seus ciclos de unload também precisam de teste específico.

## Economia

O reparo utiliza TentarRemoverReinUnidades. O custo mantém a fórmula existente. O estado anterior do equipamento é preservado e a cobrança é devolvida quando o reparo falha. Reentrada pelo botão ou por callback da carteira é bloqueada. Exceções de um assinante de atualização da carteira são registradas sem transformar um débito aplicado em aparente falha.

A entrega de missão planeja as quantidades por slot, mantém os materiais reservados vivos e só os destrói depois da recompensa. Uma falha de reserva recupera os mesmos objetos e a ordem da pilha. Uma falha durante a recompensa tenta recuperar materiais, XP e saldo. Se a própria recuperação falhar, o salvamento é bloqueado.

O salvamento fica suspenso durante essas operações síncronas para impedir snapshots intermediários.

## Combate

O caminho de contato de Slime, Goblin e Player deixa o dano da flecha para o próprio script Flecha. Foram mantidos os filtros existentes de lançamento, impacto processado, autoria e multiplicador.

A resolução dos inimigos para flecha e magia considera somente a linhagem do objeto atingido. Foi removida a busca de Slime nos filhos de uma raiz compartilhada. Slime e Goblin recebem a origem pelo método existente; o Espírito usa seu método de dano existente e registra o primeiro atacante no componente de experiência quando presente. Seu ataque e respawn não foram alterados.

## Interação VR

As luvas e o elmo não reaplicam escala de mundo em LateUpdate quando estão no inventário. A captura inicial ocorre antes de Start, evitando capturar uma escala já reduzida pela carga do inventário.

O serviço UsoAcaoCompartilhada mantém consumidores por InputAction. Ele só desabilita uma ação habilitada por ele depois da saída do último consumidor. Ações que já estavam habilitadas por um controlador externo são preservadas. Eventos externos ao serviço, especialmente InputActionManager, ainda precisam de validação da ordem de ativação no Unity.

## Testes executados

Compilação Assembly-CSharp com o compilador e referências do Unity 6000.3.10f1: aprovada, sem erros. Permanecem avisos CS0414 de campos não utilizados.

| Suite | Verificações aprovadas |
|---|---:|
| InventarioEstadosIndividuais.Tests.ps1 | 88 |
| ArquivoSaveSeguro.Tests.ps1 | 19 |
| EconomiaInput.Tests.ps1 | 11 |
| MissoesMateriais.Tests.ps1 | 10 |
| CombateAlvos.Tests.ps1 | 9 |
| Total | 137 |

As suites novas executam métodos reais extraídos das fontes e serviços reais em um host Unity simulado. Incluem saldo insuficiente, reparo com falha e estorno, reentrada, materiais insuficientes, reserva parcial, falha após recompensa, repetição de entrega, consumidores de input, alvo em raiz compartilhada e flecha não lançada/impacto repetido.

A suite de inventário também confere a propagação da origem por unidade, DTO, clone restaurado e estado de cena. Arquivos usados pelos testes e saídas do compilador ficaram em diretórios temporários. Nenhum save real foi modificado.

**Não foram executados Play Mode, física real, callbacks XR reais ou testes no Quest.**

## Validação pendente no Unity e Quest

1. Usar exclusivamente uma sessão/save descartável.
2. Carregar pilhas com IDs e durabilidades distintos; conferir LIFO e limite 99.
3. Injetar falha após aplicar o inventário e conferir mesmas instâncias, pais, escala, estado físico e sockets; conferir bloqueio de autosave.
4. Consumir um original sem respawn; salvar e reiniciar. Conferir que não reaparece.
5. Coletar e consumir recurso com respawn autorizado; conferir o respawn normal.
6. Migrar um save anterior e repetir consumo/reinício.
7. Reparar com saldo exato, insuficiente e cliques repetidos; conferir débito único e ausência de cobrança quando não repara.
8. Completar coleta, gastar os materiais e tentar entregar. Conferir ausência de recompensa.
9. Entregar materiais distribuídos em vários slots; conferir quantidade exata e os objetos restantes.
10. Testar flecha parada, lançada, vários Colliders, autoria e multiplicador.
11. Colocar Slime, Goblin e Espírito sob o mesmo pai; testar dano direto e em área, inclusive inimigo fora do raio.
12. Guardar, retirar, equipar e desequipar luvas e elmo; salvar/carregar e conferir escala, física e cabeça visível.
13. Alternar painéis e popups; testar ambas as mãos e a ordem de ativação do InputActionManager.
14. Repetir os fluxos principais no Quest, incluindo pausa e retomada.

## Possíveis regressões que exigem atenção

- Callbacks XR durante reativação e recuperação das seleções.
- Efeitos externos de OnEnable/OnDisable e coroutines interrompidas durante rollback.
- Origem de itens em saves antigos que não tenham um original identificável.
- Inicialização dos scripts de escala e dos controladores externos de input.
- Operações de missão que executem callbacks adicionais durante XP, recompensa ou mudança de saldo.

## Arquivos de produção alterados/adicionados

- [Save/SaveManager.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Save/SaveManager.cs:205>)
- [Save/InventarioSaveAdapter.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Save/InventarioSaveAdapter.cs:202>)
- [Save/RecuperacaoItemCarga.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Save/RecuperacaoItemCarga.cs:8>)
- [Save/ItemPersistente.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Save/ItemPersistente.cs:39>)
- [Save/SaveData.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Save/SaveData.cs:16>)
- [Save/InventorySaveData.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Save/InventorySaveData.cs:10>)
- [MENU/Inventario/SlotInventario.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/MENU/Inventario/SlotInventario.cs:1462>)
- [NPC/Ferreiro/Reparar.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/NPC/Ferreiro/Reparar.cs:158>)
- [MENU/Status/CarteiraReinPlayer.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/MENU/Status/CarteiraReinPlayer.cs:40>)
- [NPC/Sacerdote/GerenciadorMissoes.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/NPC/Sacerdote/GerenciadorMissoes.cs:593>)
- [Armas/Flecha.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Armas/Flecha.cs:313>)
- [Armas/ReceptorDanoInimigo.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Armas/ReceptorDanoInimigo.cs:4>)
- [Armas/BolaDeFogo.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Armas/BolaDeFogo.cs:527>)
- [Armas/EfeitoFogoArea.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Armas/EfeitoFogoArea.cs:188>)
- [Enemy/Slime/SlimeIA.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Enemy/Slime/SlimeIA.cs:455>)
- [Enemy/Slime/GoblinIA.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Enemy/Slime/GoblinIA.cs:456>)
- [Enemy/Slime/EsperitoDeFogoIA.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Enemy/Slime/EsperitoDeFogoIA.cs:70>)
- [MENU/Status/StatusPlayer.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/MENU/Status/StatusPlayer.cs:248>)
- [Equipamento/EquiparManoplas.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Equipamento/EquiparManoplas.cs:140>)
- [Equipamento/EquiparElmo.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Equipamento/EquiparElmo.cs:117>)
- [MENU/UsoAcaoCompartilhada.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/NPC/Sacerdote/BotaoMissao.cs:120>)
- [NPC/Ferreiro/BotaoCriarItem.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/NPC/Ferreiro/BotaoCriarItem.cs:55>)
- [NPC/Sacerdote/BotaoMissao.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/NPC/Sacerdote/BotaoMissao.cs:37>)
- [Equipamento/Botaoequipar.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Equipamento/Botaoequipar.cs:37>)
- [NPC/Ferreiro/NPCPopup.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/NPC/Ferreiro/NPCPopup.cs:54>)
- [MENU/Inventario/InventarioArcoUI.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/MENU/Inventario/InventarioArcoUI.cs:672>)
- [MENU/Status/StatusVR.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/MENU/Status/StatusVR.cs:60>)
- [Armas/Arco.cs](<D:/Unity Projetos/VR_My_Reincarnation/My Reincarnation/Assets/Script/Armas/Arco.cs:457>)

Os dois scripts auxiliares separados possuem seus arquivos .meta; UsoAcaoCompartilhada está incorporado em BotaoMissao.cs. Os três testes novos estão na pasta Tests; o teste de estados individuais foi ampliado.

## Arquivos do usuário preservados

Cena Principal, SampleScene.scenetemplate, peitoral e Tower Diffuse.psd permaneceram fora das edições desta implementação. Durante o trabalho também apareceram alterações nos prefabs Machado Velho e Picareta Metal; elas não foram feitas por comandos desta implementação e foram preservadas. Nenhum desses arquivos foi descartado ou incluído em commit.

## Fora do escopo

Não foram implementadas correções da missão atual após carregar, retenção de arquivos, desempenho/URP, animação e respawn do Goblin, ataque do Espírito, temporizadores dos painéis ou mudanças estéticas.

## Ajuste após erro de compilação no Editor

UsoAcaoCompartilhada existia no disco, mas não constava na lista de fontes gerada pelo Unity. A classe foi incorporada a BotaoMissao.cs e o arquivo separado removido, evitando duplicidade quando ocorrer uma nova importação. A validação passa a usar a lista de fontes do Unity sem adicionar arquivos manualmente.
