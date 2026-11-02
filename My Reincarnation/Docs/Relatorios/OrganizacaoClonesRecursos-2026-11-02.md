# Organizacao de clones de recursos

RespawnNatureza.ConfiguracaoRespawnNatureza possui Pasta Clones opcional. Para Stone, Wood e Rocha Metalica/Metallic Rock, procura os grupos correspondentes apenas sob ---STATIC---/NATUREZA/RECURSOS na mesma cena. Procura um filho existente com nome do grupo + Clone, Clone + nome do grupo ou clone. Aceita a pasta vazia Metallic Rock dentro do grupo Metallic Rock apresentada pelo usuario. Nao cria GameObjects para substituir pastas ausentes.

Clones novos recebem esse pai preservando a pose mundial, antes de PrepararItensRespawnados recapturar a base. Recursos soltos restaurados pelo save tambem sao organizados: originais com origem de cena ficam no grupo original, copias sem origem ficam na pasta clone. Itens no inventario sao ignorados. O fluxo dos cogumelos continua separado.

Sem uma pasta de destino para os tres tipos, interrompe o nascimento e mostra o erro de configuracao em vez de criar um clone na raiz. Pasta Clones pode ser associada explicitamente no Inspector, se outro nome for escolhido. Nenhum ajuste de escala, posicao, tempo ou coleta foi feito nesta revisao.

Validacao: 57 verificacoes de respawn/hierarquia em host simulado, incluindo as tres categorias e exclusao dos itens do inventario; 29 verificacoes da restauracao de equipamentos. Compilacao real da Assembly-CSharp concluida sem erros. Validacao visual em Play/Quest pendente.
