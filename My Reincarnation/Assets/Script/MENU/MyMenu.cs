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
            if (abas[i].painel != null)     abas[i].painel.SetActive(false);
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
            if (abas[i].painel != null)     abas[i].painel.SetActive(false);
        }

        abaAtiva = -1;
    }

    // ── Selecao ──────────────────────────────────────────────

    private void SelecionarAba(int indice)
    {
        // Clicou na aba ja ativa: fecha o painel e desmarca
        if (abaAtiva == indice)
        {
            FecharPainel(indice);
            abaAtiva = -1;
            return;
        }

        // Fecha o painel anterior
        if (abaAtiva >= 0)
            FecharPainel(abaAtiva);

        // Abre o novo
        abaAtiva = indice;
        AbrirPainel(indice);
    }

    private void AbrirPainel(int indice)
    {
        if (abas[indice].painel != null)
            abas[indice].painel.SetActive(true);
    }

    private void FecharPainel(int indice)
    {
        if (abas[indice].painel != null)
            abas[indice].painel.SetActive(false);
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