using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MyMenu : MonoBehaviour
{
    [System.Serializable]
    public class AbaMenuDados
    {
        [Tooltip("Botao desta aba.")]
        public Button botao;

        [Tooltip("Image de fundo do botao que muda de cor. Nunca aponte para o Logo.")]
        public Image fundoBotao;

        [Tooltip("Painel de conteudo desta aba. Comeca desativado.")]
        public GameObject painel;
    }

    [Header("Abas do Menu")]
    [SerializeField] private AbaMenuDados[] abas;

    [Header("Cores")]
    [SerializeField] private Color corNormal  = Color.black;
    [SerializeField] private Color corSelecao = new Color(0.3f, 0.3f, 0.3f, 1f);

    private int abaAtiva = -1;

    private void Awake()
    {
        for (int i = 0; i < abas.Length; i++)
        {
            if (abas[i] == null) continue;
            if (abas[i].fundoBotao != null) abas[i].fundoBotao.color = corNormal;
            FecharPainelInterno(abas[i]);
        }
    }

    private void OnEnable()
    {
        for (int i = 0; i < abas.Length; i++)
        {
            if (abas[i] == null || abas[i].botao == null) continue;

            int indice = i;

            // Hover via EventTrigger
            EventTrigger trigger = abas[i].botao.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = abas[i].botao.gameObject.AddComponent<EventTrigger>();

            trigger.triggers.Clear();

            EventTrigger.Entry entrar = new EventTrigger.Entry();
            entrar.eventID = EventTriggerType.PointerEnter;
            entrar.callback.AddListener(_ => AplicarCorHover(indice));
            trigger.triggers.Add(entrar);

            EventTrigger.Entry sair = new EventTrigger.Entry();
            sair.eventID = EventTriggerType.PointerExit;
            sair.callback.AddListener(_ => AplicarCorNormal(indice));
            trigger.triggers.Add(sair);

            // Clique via Button.onClick
            abas[i].botao.onClick.RemoveAllListeners();
            abas[i].botao.onClick.AddListener(() => SelecionarAba(indice));
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < abas.Length; i++)
        {
            if (abas[i] == null) continue;
            if (abas[i].fundoBotao != null) abas[i].fundoBotao.color = corNormal;
            FecharPainelInterno(abas[i]);
        }

        abaAtiva = -1;
    }

    // ── Selecao ──────────────────────────────────────────────

    private void SelecionarAba(int indice)
    {
        // Clicou na aba ja ativa: fecha e desmarca
        if (abaAtiva == indice)
        {
            FecharPainelInterno(abas[indice]);
            abaAtiva = -1;
            return;
        }

        // Fecha o painel anterior
        if (abaAtiva >= 0)
            FecharPainelInterno(abas[abaAtiva]);

        // Abre o novo
        abaAtiva = indice;
        AbrirPainelInterno(abas[indice]);
    }

    // ── Abrir / Fechar ────────────────────────────────────────

    private static void AbrirPainelInterno(AbaMenuDados aba)
    {
        if (aba == null || aba.painel == null) return;

        // Se o painel contiver um InventarioVR, usa a API dele para
        // garantir que todos os filhos e slots sejam reativados corretamente.
        // Em alguns layouts o InventarioVR fica no objeto-pai do painel
        // (por exemplo, no Canvas Menu), e não dentro do próprio painel.
        InventarioVR inv = EncontrarInventario(aba.painel);
        if (inv != null)
        {
            aba.painel.SetActive(true);
            inv.AbrirInventario();
            return;
        }

        aba.painel.SetActive(true);
    }

    private static void FecharPainelInterno(AbaMenuDados aba)
    {
        if (aba == null || aba.painel == null) return;

        InventarioVR inv = EncontrarInventario(aba.painel);
        if (inv != null)
        {
            inv.FecharInventario();
            aba.painel.SetActive(false);
            return;
        }

        aba.painel.SetActive(false);
    }

    private static InventarioVR EncontrarInventario(GameObject painel)
    {
        if (painel == null) return null;

        // Primeiro procura no próprio painel e nos pais. Isso cobre a
        // configuração atual, em que o componente está no Canvas Menu.
        InventarioVR inv = painel.GetComponentInParent<InventarioVR>(true);
        if (inv != null && inv.ControlaPainel(painel)) return inv;

        // Mantém compatibilidade com painéis que tenham o componente em um
        // objeto filho.
        inv = painel.GetComponentInChildren<InventarioVR>(true);
        return inv != null && inv.ControlaPainel(painel) ? inv : null;
    }

    // ── Cores ─────────────────────────────────────────────────

    private void AplicarCorHover(int indice)
    {
        if (abas[indice] == null || abas[indice].fundoBotao == null) return;
        abas[indice].fundoBotao.color = corSelecao;
    }

    private void AplicarCorNormal(int indice)
    {
        if (abas[indice] == null || abas[indice].fundoBotao == null) return;
        abas[indice].fundoBotao.color = corNormal;
    }
}
