# Nome e descricao atuais no popup

AtributosManoplas, AtributosPeitoral e AtributosElmo exibiam resultado.nome e resultado.descricao, gravados na geracao e restaurados do save. Estes campos continuam no snapshot para preservar compatibilidade e o payload esperado na validacao, mas nao sao usados como fonte dos textos visuais.

O popup agora usa Nome Exibicao (se preenchido) ou ObterNomePrefab atual, e Descricao atual do componente. Atualiza na abertura/restauracao e durante os frames em que o popup esta visivel. Alterar estes textos nao modifica classe, afinidade, atributos, durabilidade ou IDs e nao refaz o sorteio. Nao altera o NomeItem usado por inventario/crafting.

Para traduzir ou editar: configurar Nome Exibicao e Descricao no componente do equipamento; aplicar ao prefab para futuros clones. Vazio em Nome Exibicao usa o nome atual do Prefab Objeto, depois a referencia do ItemPersistente e finalmente o nome da instancia sem Clone. Overrides existentes de uma instancia continuam seguindo as regras normais do Unity: a configuracao da propria instancia prevalece sobre a configuracao herdada do prefab.

Tests/IdentidadePopupEquipamentos.Tests.ps1 executa os metodos reais dos tres scripts em host simulado: 21 verificacoes de textos atuais, mudancas ao vivo, override, descricao vazia, fallbacks e imutabilidade do resultado/payload. Compilacao via response file real do Unity, sem erros (avisos CS0414 existentes). Renderizacao e persistencia nativa ainda exigem Play/Quest. Nao foram modificados saves reais nem criados commits.
