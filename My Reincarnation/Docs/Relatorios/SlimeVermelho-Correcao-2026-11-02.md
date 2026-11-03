# Slime Vermelho — correções e validações

Data: 02/11/2026. Escopo desta etapa: somente o Vermelho. As modificações anteriores do Verde estavam sem commit e foram preservadas. Nenhum save real foi modificado. Sem commit ou push.

## 1. Componentes reais e diagnóstico

O prefab usa SlimeIA, SlimeAnimacao, Animator próprio, Rigidbody dinâmico e BoxCollider no objeto raiz. Não há NavMeshAgent. SlimeAtaque.cs, SlimeVida.cs e SlimeAudio.cs não existem neste projeto; essas responsabilidades estão em SlimeIA. SlimeMovimento/SlimeDeteccao já existiam da tarefa do Verde, mas estavam ausentes no Vermelho.

O caminho antigo de movimentação conserva Y, calcula avanço horizontal e usa Rigidbody.MovePosition no Update. Não consulta normal de solo ou desnível. O BoxCollider usa material padrão e quinas; o Rigidbody estava sem restrições de rotação. Esses fatores são incompatíveis com o controle pretendido em terrenos irregulares. A causa estrutural foi confirmada no código/prefab; a reprodução e a contribuição de cada fator no terreno específico dependem de Play Mode.

Também foram confirmados: máscara visual zero; ausência dos componentes que ativam as proteções de interrupção/finalização; agendamento antigo sem deduplicação; referências de visão apontando ao pivô; destruição em 0.2 s; ataque visual sem reinício por golpe; condições incorretas de transição no Animator.

## 2. Movimento físico e terreno

O Vermelho ativa SlimeMovimento com suas dimensões reais. Usa FixedUpdate, velocidade tangente ao solo e gravidade existente no deslocamento terrestre. Não altera Y por teleporte para acompanhar o terreno. Solo transitável, limite de queda e varredura de obstáculos continuam sendo consultados sem alocações por passo.

Um CapsuleCollider arredondado substitui o BoxCollider na simulação: o motor dimensiona a cápsula pelos bounds do BoxCollider e o desativa no Awake. O fundo físico é preservado. O BoxCollider fica referenciado para essa medição, sem duplicar os contatos durante o jogo.

Material exclusivo Slime Vermelho Solo: atrito estático/dinâmico 0.1, combinação Minimum e restituição zero. Não foi utilizado o material do Verde. Rigidbody recebe interpolação e restrições X/Z; não foi adicionado NavMeshAgent.

Inclinação máxima 45 graus, tolerância do solo 0.12 m, margem de desnível 0.18 m, queda máxima 0.6 m e limite de rota bloqueada 2 s são parâmetros geométricos em metros, independentes dos atributos de combate. Precisam ser avaliados contra os colliders reais da cena. A margem não garante transposição de degraus verticais dessa altura.

## 3. Patrulha e perseguição

Usa chegada com altura compatível, tentativa local de desvio e limite de falta de progresso. Se não há avanço em direção ao ponto, a patrulha troca destino; a perseguição pausa antes de procurar novamente. Não empurra uma parede indefinidamente.

Pontos manuais do prefab/cena e suas posições não foram editados. Pontos automáticos criados pelo motor ativado pertencem à instância, são limpos e regenerados no respawn, em vez de transferir referências destruídas.

O sistema é desvio local, não planejamento global de rotas. Não promete resolver labirintos ou caminhos que exigem grandes afastamentos do destino.

## 4. Detecção e VR

Adicionado SlimeDeteccao. Conservados raio 15 m e ângulo 210 graus. Máscara visual corrigida para Default/interactable (65), com filtragem do próprio corpo e do jogador visado. As layers existentes da cena não foram editadas.

O alvo é o corpo rastreado do AvatarBody, com fallback para câmera/raiz. XR Origin, câmera e AvatarBody não foram alterados. No Vermelho, o pivô raiz não é tratado como olhos: utiliza ponto de olhos separado quando válido ou a altura configurada de 0.8 m. Isso evita consultar a visão a partir do chão.

Consultas de visão usam intervalo 0.15 s e buffers; o registro de jogadores por tag é compartilhado com os consumidores existentes. Novas layers de cenário devem ser incluídas nas máscaras pelo Inspector.

## 5. Ataque próprio do Vermelho

O perfil do Vermelho preserva o arco senoidal original: distância 1.5 m, altura 0.6 m e duração 0.35 s, com quantização natural do passo de física. Não utiliza o tempo de voo balístico do Verde.

Durante o arco, a gravidade é suspensa temporariamente para não disputar com a trajetória autoral. Rigidbody.MovePosition é executado no passo de física; o volume da cápsula é varrido em todas as direções do deslocamento, incluindo teto/solo. Não há recuo por teleporte à origem ao bater numa parede.

Ao completar ou interromper o arco, a gravidade original é restaurada. O cancelamento não cria impulso ascendente adicional. Dano, morte, saída do estado e desativação interrompem a rotina; impactos atrasados são rejeitados. A aterrissagem prevista precisa ter solo acessível.

Foram conservados dano 5, cooldown 1.2 s, distância de iniciar ataque 1.3 m, alcance 2 m, raio de ataque 0.62 m e sons originais. A varredura do deslocamento agora representa o volume físico do corpo, em vez de depender apenas de uma esfera horizontal.

## 6. Animação

O Vermelho já tinha Animator, SlimeAnimacao e sete clipes próprios corretamente vinculados. Essas referências foram preservadas; nenhum clipe foi criado, trocado ou copiado do Verde.

Corrigidas quatro transições comprovadas no controlador do Vermelho:

- Alerta → Ataque: exige Ataque verdadeiro, em vez de Alerta verdadeiro.
- Alerta → Dano: exige Dano verdadeiro, com entrada imediata.
- Ataque → Alerta: exige Alerta verdadeiro, em vez de Alerta falso.
- Dano → Alerta: exige Alerta verdadeiro, em vez de Dano verdadeiro.

O script possui agora uma opção desativada por padrão, ativada só no Vermelho: reiniciar ações por golpe. Estados lógicos selecionam seus estados visuais existentes; ataques sucessivos reiniciam Ataque, e novos golpes reiniciam Dano. Isso evita travar no fim de um clipe ou deixar o visual competir com a IA. A mudança visual/blending ainda exige Play Mode.

O clipe Morto existente foi associado ao cálculo do tempo mínimo de destruição. O valor base 0.2 s do prefab foi mantido; o tempo efetivo é o maior entre esse valor e duração do clipe ajustada pela velocidade do Animator + margem de 0.25 s. Assim, a morte não é cortada apenas por causa do valor antigo.

## 7. Dano e bloqueio

Não foi adicionado cooldown de dano recebido. O controle das armas foi mantido. Sword/Arrow continuam sendo as tags autorizadas; tags não receberam valores de dano. Espada/flecha/magia conservam suas fórmulas e autoria.

Ao ativar os componentes físicos, o Vermelho também utiliza as proteções já existentes do fluxo corrigido: não recebe um segundo impacto pela IA de armas/magias que já processam seu contato; flechas continuam sob responsabilidade de seu próprio script. Ataque resolve impacto apenas uma vez, com prioridade de bloqueio pelo escudo e rejeição depois de cancelamento/morte.

As listas de drops do Vermelho permanecem vazias. Ferramentas não autorizadas não passaram a causar dano automaticamente. IDano, ReceptorDanoInimigo e scripts de armas não foram modificados nesta etapa.

## 8. Morte e recompensas

O Vermelho passa a usar a finalização protegida disponível no código compartilhado. Morte é única; XP, REIN, missão, drops e agendamento são isolados contra exceções. Destruição permanece no finally e agora respeita o clipe de morte do Vermelho.

Falhas parciais de recompensa não são repetidas automaticamente, evitando duplicação. Valores de experiência, REIN 0.01–0.1, chances e quantidades não foram alterados.

## 9. Respawn

Cadastro existente conferido: Slime Vermelho, prefab correspondente, 15 s e offset Y +1. Cena não editada. Reaparece na posição da morte + offset, preservando rotação/configuração. Não foi usada a posição original de nascimento.

AgendarRespawnVermelho reutiliza a fila deduplicada e a recuperação após desativação/reativação, mas transmite raio, altura e centro físicos reais do Vermelho. O espaço livre é consultado como cápsula; não se assume o volume menor do Verde.

Se o local está ocupado, a correção permanece limitada a 0.30 m horizontal. Sem solo/espaço seguro, conserva uma única pendência e tenta novamente depois, sem instanciar clones de tentativa. Vida, patrulha e estados são reinicializados. Nenhuma pendência foi adicionada ao save.

## 10. Valores e arquivos protegidos

Comparação com o início desta tarefa confirmou 16 valores de gameplay preservados. O prefab/material do Verde, prefabs da Tartaruga/Goblin/Espírito e a cena mantêm os mesmos hashes. O controlador do Vermelho preserva as sete referências de movimento/clipe.

SlimeIA, SlimeMovimento, SlimeAnimacao e RespawnMonstro são compartilhados. As mudanças exclusivas do Vermelho são protegidas pelo Perfil Vermelho e pela opção de animação, ambos falsos por padrão. O prefab Verde não recebeu esses campos ativados. Seus testes de regressão continuam aprovados. Tartaruga e demais espécies não receberam componentes novos.

## 11. Arquivos e pontos principais

- Assets/INIMIGOS/Monster/Prefabs/Slime Vermelho.prefab:637, :1208 e :1230 — morte e componentes físicos/detecção.
- Assets/INIMIGOS/Monster/Prefabs/Slime Vermelho Solo.physicMaterial e meta — material próprio.
- Assets/INIMIGOS/Monster/Animators/Slime Vermelho.controller — quatro transições corrigidas; clipes preservados.
- Assets/Script/Enemy/Slime/SlimeMovimento.cs:88, :95 e :100 — perfil, arco e varredura volumétrica.
- Assets/Script/Enemy/Slime/SlimeIA.cs:997 — investida do Vermelho; integração com animação, visão e morte.
- Assets/Script/Enemy/Slime/SlimeAnimacao.cs:47 e :53 — ações por golpe e duração da morte.
- Assets/Script/Respawn/RespawnMonstro.cs:51 — entrada específica de respawn do Vermelho.
- Tests/SlimeVermelho.Tests.ps1, SlimeVermelhoAnimacao.Tests.ps1 e SlimeVermelhoRespawn.Tests.ps1.

## 12. Inspector

No motor: Perfil Vermelho ativado; Collider Modelo aponta ao BoxCollider antigo; parâmetros geométricos acima disponíveis. No SlimeAnimacao: Reiniciar Ações Por Golpe ativado, Clip Morte aponta ao Morto original e Margem Transição Morte é 0.25 s. Detecção usa Intervalo Visão 0.15 s.

O BoxCollider de referência é desativado pelo motor no Awake. Isso é esperado; o CapsuleCollider deve permanecer como corpo físico. Usar NavMeshAgent está desativado somente neste prefab porque não há agente configurado nele.

## 13. Testes executados

Em hosts simulados, executando métodos extraídos dos scripts reais:

- 9 verificações específicas do Vermelho: arco, distância/altura/duração, impacto único, interrupção, escudo, gravidade e morte protegida.
- 9 verificações do SlimeAnimacao: padrão anterior preservado, estados do Vermelho, repetição e tempo de morte.
- 16 verificações do respawn Vermelho: três ciclos, tempo, ponto da morte + offset, vida/patrulha, volume próprio e local bloqueado.
- 22 verificações de regressão de dano, escudo, morte e matemática de solo do Verde.
- 20 verificações do respawn Verde, incluindo três ciclos e desativação/reativação.
- 9 verificações de detecção e cache de alvo VR.
- 9 verificações existentes de receptor/impacto.
- 7 verificações da fórmula de dano da espada; fórmula de defesa também conferida.

Total: 101 verificações de lógica aprovadas. Compilação Assembly-CSharp com referências Unity: sem erros. Diff revisado sem erros de whitespace. Esses testes não executam o solver de física nem avaliam visualmente os clipes.

## 14. Classificação

| Ponto | Situação |
|---|---|
| Movimento horizontal sem normal, BoxCollider/material/rotação | Corrigido na implementação; terreno real pendente de validação |
| Patrulha, altura e rotas bloqueadas | Correções disponíveis ativadas no Vermelho; Play pendente |
| Máscara zero e alvo na raiz XR | Corrigido na configuração/lógica; VR real pendente |
| Animator e clipes vinculados | Já corretos; preservados |
| Condições invertidas e repetição de ataque/dano | Corrigido; lógica simulada aprovada; visual pendente |
| Destruição em 0.2 s cortando morte | Corrigido por duração mínima do clipe; visual pendente |
| Ataque concorrente após dano/morte | Corrigido; cancelamento, gravidade e impacto tardio testados |
| Fórmulas, multiplicadores, autoria e flecha parada | Proteções existentes preservadas; armas não alteradas |
| Finalização após erro de recompensa | Correção compartilhada ativada no Vermelho; testes aprovados |
| Respawn duplicado/local ocupado/inicialização | Corrigido; três ciclos simulados aprovados |
| Estabilidade física, animação visual e Quest 2 | Pendente de validação nativa |

## 15. Testes nativos pendentes e riscos

Executar em Play/Quest: plano, ondulações, rampas transitáveis e excessivas, depressões, paredes e corredores; patrulha e perda de visão; deslocamento físico do headset; investida de 0.35 s, escudo e interrupção durante subida/pico/descida; espada/flecha/magia e autoria; morte completa; três ciclos reais de respawn e local temporariamente ocupado; vários Vermelhos simultâneos; comparação do Verde e Tartaruga.

Principais riscos para observar: contato da cápsula em quinas/rampas, falta de planejamento global de rota, ajuste de layers, blending/reinício visual das ações, gravidade ao interromper no ar e saturação conservadora dos buffers. O corpo morto permanece até terminar o clipe, por mais tempo que os 0.2 s antigos; conferir bloqueio físico e quantidade de cadáveres em combate intenso.

Não foram executados Play Mode nem testes no Meta Quest 2. Não há medição de FPS/GC ou afirmação de ausência de jitter. O motor reutiliza consultas NonAlloc e o cache de jogadores; nenhum Physics.SyncTransforms foi adicionado. Confirmar custo real com Profiler antes de considerar o desempenho aprovado.
