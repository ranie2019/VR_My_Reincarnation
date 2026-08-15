using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class Reparar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform slot;
    [SerializeField] private Button botaoReparar;
    [SerializeField] private TextMeshProUGUI textoValor;

    [Header("Mensagens")]
    [SerializeField] private GameObject mensagemSucesso;
    [SerializeField] private GameObject mensagemSemDinheiro;
    [SerializeField] private GameObject mensagemJaEstaInteiro;

    [Header("Preço")]
    [SerializeField] private float precoPorPontoDeVida = 0.1f;

    private GameObject itemNoSlot;
    private Equipamento equipamentoNoSlot;
    private float valorAtualDoReparo;
    private StatusPlayer statusPlayer;
    private XRSocketInteractor socketSlot;

    private void Start()
    {
        EsconderTodasMensagens();

        statusPlayer = FindFirstObjectByType<StatusPlayer>();

        if (botaoReparar != null)
        {
            botaoReparar.onClick.RemoveListener(TentarReparar);
            botaoReparar.onClick.AddListener(TentarReparar);
        }

        if (slot != null)
        {
            socketSlot = slot.GetComponent<XRSocketInteractor>();
            if (socketSlot != null)
            {
                socketSlot.selectEntered.AddListener(OnItemEncaixado);
                socketSlot.selectExited.AddListener(OnItemRemovido);
            }
        }

        AtualizarValorNaTela(0f);
    }

    private void OnDestroy()
    {
        if (botaoReparar != null)
            botaoReparar.onClick.RemoveListener(TentarReparar);

        if (socketSlot != null)
        {
            socketSlot.selectEntered.RemoveListener(OnItemEncaixado);
            socketSlot.selectExited.RemoveListener(OnItemRemovido);
        }
    }

    private void OnItemEncaixado(SelectEnterEventArgs args)
    {
        GameObject item = args.interactableObject != null
            ? args.interactableObject.transform.gameObject
            : null;

        ProcessarItemDetectado(item);
    }

    private void OnItemRemovido(SelectExitEventArgs args)
    {
        GameObject item = args.interactableObject != null
            ? args.interactableObject.transform.gameObject
            : null;

        if (item == itemNoSlot)
            ProcessarItemDetectado(null);
    }

    private void Update()
    {
        if (socketSlot == null)
            VerificarItemNoSlot();
    }

    private void VerificarItemNoSlot()
    {
        if (slot == null) return;

        GameObject itemAtual = slot.childCount > 0 ? slot.GetChild(0).gameObject : null;
        ProcessarItemDetectado(itemAtual);
    }

    private void ProcessarItemDetectado(GameObject itemAtual)
    {
        if (itemAtual == itemNoSlot) return;

        itemNoSlot = itemAtual;

        if (itemAtual == null)
        {
            equipamentoNoSlot = null;
            AtualizarValorNaTela(0f);
            return;
        }

        equipamentoNoSlot = itemAtual.GetComponentInChildren<Equipamento>(true);

        if (equipamentoNoSlot != null)
        {
            float faltando = equipamentoNoSlot.VidaFaltando;
            valorAtualDoReparo = faltando * precoPorPontoDeVida;
            AtualizarValorNaTela(valorAtualDoReparo);
        }
        else
        {
            valorAtualDoReparo = 0f;
            AtualizarValorNaTela(0f);
        }
    }

    private void AtualizarValorNaTela(float valor)
    {
        valorAtualDoReparo = valor;
        if (textoValor != null)
            textoValor.text = valor.ToString("F2");
    }

    private void EsconderTodasMensagens()
    {
        if (mensagemSucesso != null) mensagemSucesso.SetActive(false);
        if (mensagemSemDinheiro != null) mensagemSemDinheiro.SetActive(false);
        if (mensagemJaEstaInteiro != null) mensagemJaEstaInteiro.SetActive(false);
    }

    public void TentarReparar()
    {
        EsconderTodasMensagens();

        if (itemNoSlot == null)
            return;

        Equipamento equip = equipamentoNoSlot;
        float preco = valorAtualDoReparo;
        float faltando = equip != null ? equip.VidaFaltando : 0f;

        // Limpa referência do slot (item permanece onde está)
        itemNoSlot = null;
        equipamentoNoSlot = null;
        AtualizarValorNaTela(0f);

        if (equip != null && faltando <= 0.01f)
        {
            if (mensagemJaEstaInteiro != null)
                mensagemJaEstaInteiro.SetActive(true);
            return;
        }

        if (equip == null)
            return;

        if (statusPlayer == null)
            statusPlayer = FindFirstObjectByType<StatusPlayer>();

        if (statusPlayer == null)
            return;

        var carteira = statusPlayer.ObterCarteiraRein();
        if (carteira == null)
            return;

        long custoEmUnidades = (long)Mathf.Round(preco * 100000f);

        if (carteira.ReinUnidades < custoEmUnidades)
        {
            if (mensagemSemDinheiro != null)
                mensagemSemDinheiro.SetActive(true);
            return;
        }

        carteira.AdicionarReinUnidades(-custoEmUnidades);
        equip.RepararCompleto();

        if (mensagemSucesso != null)
            mensagemSucesso.SetActive(true);
    }
}