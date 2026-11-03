# Slime Verde — implementação e validação pendente

Data: 02/11/2026. Escopo: exclusivamente o Slime Verde. Sem commits, push, alterações de saves ou mudanças nos outros prefabs de inimigos.

## 1. Diagnóstico do terreno

O caminho usado pelo Slime Verde não era NavMesh: o prefab não tem agente. SlimeIA.MoverPara calculava um destino com Y igual ao atual e chamava Rigidbody.MovePosition a partir de Update. Não havia consulta de solo, normal, limite de inclinação ou tratamento de rota bloqueada. O collider físico era o MeshCollider convexo do modelo filho, sem material próprio. O Rigidbody era dinâmico, sem restrições de rotação, enquanto a IA também corrigia sua rotação pelo Transform.

Essas são as causas estruturais verificadas de controle inadequado em ondulações: movimento horizontal sem adaptação à superfície, forma de contato complexa, atrito padrão e disputa entre física e Transform. A contribuição exata de cada fator para o travamento observado ainda depende de reprodução na cena real; não foi comprovada por uma simulação física nativa nesta tarefa.

## 2. Estratégia

SlimeMovimento e SlimeDeteccao são componentes novos, adicionados somente ao prefab verde. SlimeIA mantém seus métodos públicos e o caminho anterior quando esses componentes estão ausentes. Não foi feita uma refatoração geral da IA.

Movimento terrestre usa velocidade do Rigidbody e rotação no FixedUpdate, direção projetada sobre a normal do solo e gravidade existente. O motor não escreve a posição Y para colar o inimigo ao chão. A tolerância de consulta não cancela a queda antes do contato: apenas contato próximo ao fundo do collider permite usar a tangente vertical. Perda momentânea do solo conserva a velocidade vertical.

## 3. Prefab e física

- CapsuleCollider no objeto raiz, dimensionado no Awake pelos bounds mundiais do MeshCollider original, conservando a altura de seu fundo. O mesh físico é desativado em runtime; o modelo visual é preservado.
- Material Slime Verde Solo: atrito estático 0, dinâmico 0.05, combinação Minimum, sem restituição.
- Restrições de rotação X/Z; Y continua controlado pelo movimento. Interpolação do Rigidbody ativada.
- Nenhum NavMeshAgent adicionado. usarNavMeshAgent desativado somente no prefab verde.
- SlimeAnimacao conectado; SlimeMovimento e SlimeDeteccao conectados.
- Máscara visual: Default e interactable (65), respeitando os sólidos das camadas usadas atualmente pela cena. Próprio Slime e jogador alvo são filtrados.
- Raios de ataque e verificação de investida do verde ajustados de 0.35 para 0.15 metro. Alcance, dano e cooldown permanecem configurados como antes. Origens dos casts passam a usar o centro do corpo físico em vez do pivô do chão.
- Posições da cena e pontos de patrulha manuais não foram editados.

## 4. Configurações novas no Inspector

SlimeMovimento: Camadas Solo, Inclinação Máxima (45 graus), Tolerância Solo (0.12 m), Tolerância Desnível (0.18 m), Queda Máxima (0.6 m), Tempo Rota Bloqueada (2 s), Collider Modelo.

SlimeDeteccao: Intervalo Visão (0.15 s). A lista de jogadores por tag é compartilhada entre os verdes e consultada, quando necessário, a cada 0.75 s.

Tolerância Desnível é margem de consulta, não uma promessa de transpor degraus verticais dessa altura. Paredes não são tratadas como rampas. A configuração precisa ser avaliada contra os colliders reais do terreno.

## 5. Patrulha e perseguição

Pequenos obstáculos recebem tentativa local de desvio à esquerda/direita. Consultas à frente rejeitam ausência de solo dentro da queda permitida e inclinações excessivas. Falta de progresso em direção ao destino tem tempo limite: a patrulha troca o ponto; a perseguição pausa antes de procurar novamente. Chegada à patrulha também exige altura compatível no verde.

Pontos automáticos pertencem à instância verde que os criou e são destruídos junto dela. No respawn, uma patrulha automática é regenerada; pontos manuais externos são preservados. Não foram modificadas coordenadas já configuradas.

Limite conhecido: desvio local não é um planejador de caminhos. Labirintos, corredores complexos e rotas que exigem afastar-se muito do destino não têm solução global garantida. O comportamento seguro nesses casos é pausar/trocar destino, sem teleportar nem escalar paredes.

## 6. Visão e VR

O verde calcula o alvo pelo corpo do AvatarBody, com fallback para a câmera do jogador e depois para a raiz. Não modifica o avatar, XR Origin ou câmera. Distância e ângulo configurados continuam aplicados.

Linha de visão usa RaycastNonAlloc e ignora os colliders do próprio Slime e do jogador visado. Consultas repetidas compartilham resultado por intervalo curto; deslocamento relevante do alvo invalida o cache. Saturação do buffer é tratada como bloqueio, não como passagem livre.

Mudanças futuras de layers precisam atualizar as máscaras do prefab. Não foi criada uma layer global nova nem alterada a classificação de objetos da cena.

## 7. Investida e interrupção

O verde conserva salto e avanço, mas usa velocidade física e gravidade em vez da posição senoidal escrita pelo Update. Altura determina a velocidade vertical inicial; duração é um mínimo. Quando a gravidade exige mais tempo para a altura configurada, o voo dura mais que o antigo valor senoidal. Distância continua distribuída sobre esse tempo de voo.

A aterrissagem precisa ter solo acessível dentro do limite de queda. A rotina verifica obstáculos durante os passos de física, sem regressar por teleporte à posição inicial ao bater. Dano recebido, saída do estado de ataque, desativação e morte cancelam a rotina e sua velocidade horizontal. A velocidade vertical é conservada para que a gravidade conclua a queda.

Impactos após cancelamento/morte são rejeitados. O primeiro impacto resolve o ataque uma única vez, incluindo múltiplos colliders. Escudo e espada continuam passando pelas regras de bloqueio existentes. Sobreposição inicial também é consultada, evitando depender apenas de SphereCast quando o alvo já está encostado.

## 8. Dano

Não foi adicionado cooldown de dano recebido. Espada, Flecha, BolaDeFogo e EfeitoFogoArea mantêm seus valores, multiplicadores e autoria. O contato da IA verde não aplica um segundo dano das fontes que já processam seu próprio impacto. A exclusão de Flecha já existia e foi preservada.

As tags continuam autorizando tipos, sem carregar valores de dano. O prefab verde permanece com Sword e Arrow; não se passou a autorizar Machado/Pick automaticamente. Ferramentas não autorizadas continuam sem causar dano por esse caminho. IDano e ReceptorDanoInimigo não foram alterados. Não foi introduzido um cooldown na IA para compensar armas futuras que não controlem seus impactos.

## 9. Animação — parcialmente corrigida

O prefab atual contém malhas estáticas; não possui Animator/Controller configurados nem SlimeAnimacao. A referência ao script esperado pela IA foi criada e conectada, mas não foram inventados clipes nem reutilizado o controlador de outro monstro.

Parado, patrulha, perseguição, dano e morte continuam sendo estados lógicos; o salto é visível pelo movimento físico. Animações específicas desses estados permanecem pendentes de assets/configuração artística no Editor. A existência de takes importáveis do arquivo Blender deve ser conferida no importador; nenhum foi assumido disponível nesta implementação.

## 10. Morte, XP, REIN e drops

O bloqueio de morte única e as flags atuais de recompensa foram mantidos. No verde, XP, REIN, missão, drops e agendamento são etapas protegidas individualmente; uma exceção é registrada e não impede as seguintes. A destruição e ocultação do canvas ficam em finally.

Falha de uma recompensa não significa repetição automática: uma entrega parcialmente executada não é tentada novamente, evitando duplicação. As fórmulas e regras de pagamento não foram alteradas.

prefabsSpawnMissao está vazio no verde. O TODO de consulta de missão não foi ampliado nesta tarefa, conforme o escopo. Drops comuns existentes foram preservados.

## 11. Respawn

AgendarRespawnVerde é um caminho novo usado somente pelo verde, com deduplicação por identidade da instância morta. Mantém posição da morte + offset e tempo configurado de 15 s na cena. Não usa a posição original do prefab.

O local é verificado antes da instanciação. Se ocupado, tenta deslocamentos horizontais limitados a 0.30 m e uma pequena correção de altura quando necessária para não nascer enterrado. Sem local seguro, mantém a mesma solicitação e consulta novamente após 1 s, sem criar tentativas de clones no mundo.

Desativar o gerenciador interrompe as rotinas verdes e conserva suas descrições em memória; reativar registra a instância e retoma as pendências uma vez. A nova IA recebe patrulha e reinicialização de vida. Pendências não foram adicionadas ao save.

## 12. Arquivos principais

- Assets/Script/Enemy/Slime/SlimeMovimento.cs:28 e :120 — preparação física e FixedUpdate.
- Assets/Script/Enemy/Slime/SlimeDeteccao.cs:38 e :54 — alvo VR e linha de visão.
- Assets/Script/Enemy/Slime/SlimeIA.cs:219, :474, :990 e :1896 — integração opt-in, impacto, investida física e finalização.
- Assets/Script/Respawn/RespawnMonstro.cs:32 e :39 — registro e fila exclusiva do verde.
- Assets/INIMIGOS/Slime/Slime Verde.prefab:1094 — componentes e parâmetros locais.
- Assets/INIMIGOS/Slime/Slime Verde Solo.physicMaterial e respectivos metas.
- Tests/SlimeVerde.Tests.ps1, SlimeVerdeDeteccao.Tests.ps1 e SlimeVerdeRespawn.Tests.ps1.

## 13. Validações executadas

- Compilação Assembly-CSharp com referências do projeto/Unity 6: sem erros; avisos de campos não utilizados.
- 22 verificações de métodos reais de dano, cancelamento, bloqueio, morte com falhas e matemática de inclinação/velocidade.
- 9 verificações da detecção real, com colliders e consultas físicas simulados: parede, próprio corpo, alvo VR, distância/ângulo e cache.
- 20 verificações do RespawnMonstro real com relógio e física simulados, incluindo três ciclos, deduplicação, local ocupado, ausência de solo e desativação/reativação.
- Teste existente de combate: 9 verificações aprovadas.
- Fórmula da espada: 7 verificações aprovadas; fórmula de defesa preservada.
- Prefab sem IDs YAML duplicados, referências dos componentes conectadas, nenhuma inclusão dos componentes verdes nos outros prefabs.
- Diff revisado: não há alterações nos prefabs do Vermelho, Tartaruga, Goblin e Espírito, nem na cena/patrulhas/saves.

Os 67 testes de lógica não executam o solver físico, importador do Blender ou Play Mode. Não comprovam aderência real, ausência de jitter ou desempenho no Quest.

## 14. Situação por problema

| Problema | Situação |
|---|---|
| Controle horizontal em Update, sem normal de solo | Corrigido na implementação; terreno real pendente de validação |
| Formato/atrito/rotação física do verde | Corrigido no prefab/motor; medidas reais pendentes |
| Rota bloqueada sem reação e pontos automáticos órfãos | Corrigido na lógica; comportamento em rotas reais pendente |
| Máscara visual zero e alvo fixo na raiz XR | Corrigido; lógica simulada aprovada; Play VR pendente |
| Investida concorrente com reação ao dano | Corrigido; cancelamento e impacto tardio testados |
| Flecha parada/impacto repetido de flecha | Já protegido no fluxo existente; preservado |
| Deduplicação de magia em área e fórmulas das armas | Já protegidas no fluxo existente; preservadas |
| Finalização interrompida por erro em recompensas | Corrigido; falhas simuladas aprovadas |
| Respawn duplicado e perda de registro ao reativar | Corrigido para a fila verde; três ciclos simulados aprovados |
| Animações específicas do modelo verde | Parcialmente corrigido: conexão pronta; assets/configuração pendentes |
| Drops de missão | Não usados pelo prefab; sem expansão de escopo |
| Estabilidade e desempenho Quest 2 | Pendente de validação nativa |

## 15. Testes nativos pendentes (A–P)

1. Plano, ondulações e rampas dentro/fora de 45 graus: verificar subida/descida, repouso, jitter e collider visual.
2. Pequenos desníveis, buracos, paredes e corredores: verificar margem de consulta, desvio, pausa e troca de ponto.
3. Pontos manuais em alturas distintas e patrulha após perseguição; conferir que nenhum ponto da cena foi deslocado.
4. Visão através de parede e por cima de decoração baixa; revisar layers se necessário.
5. Deslocamento físico do headset, em pé e sentado, sem joystick: conferir alvo corporal e alcance.
6. Salto/avanço/cooldown, colisão de corpo, escudo e espada; dano durante subida, pico e descida; morte durante a investida.
7. Espada, flecha lançada e parada, ferramentas autorizadas/não autorizadas e magias; conferir autoria e dano exato por impacto.
8. XP, REIN e drop uma vez por morte; verificar os tempos visuais quando houver clipes válidos.
9. Três ciclos reais de respawn, local ocupado e gerenciador desativado/reativado. Conferir vida, física, patrulha e população.
10. Vários verdes no Quest 2, medindo CPU/física/GC no Profiler. Comparar Vermelho e Tartaruga antes/depois em Play.

## 16. Riscos e compatibilidade

Os ramos novos dependem de SlimeMovimento/SlimeDeteccao, ausentes nos outros prefabs. SlimeIA e RespawnMonstro são compartilhados, portanto o teste nativo de regressão ainda é necessário. O ajuste OnEnable do gerenciador também restaura seu registro global após reativação; os agendamentos antigos de outras espécies não foram substituídos.

O salto físico pode durar mais que o antigo arco senoidal. Collider arredondado e material com pouco atrito mudam o contato do verde com rampas. Esses são os principais pontos para ajuste no Editor. Os buffers saturados falham de modo conservador, podendo suspender uma ação em locais muito densos, em vez de permitir atravessar obstáculos.

Não há alegação de implementação já validada no Meta Quest. A entrega é código compilável e testes de lógica, com limitações visuais e validações físicas explicitamente registradas.
