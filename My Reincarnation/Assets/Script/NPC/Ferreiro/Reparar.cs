using System;
using System.Reflection;
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
    [SerializeField] private GameObject mensagemSucesso;        // "Item reparado com sucesso"
    [SerializeField] private GameObject mensagemSemDinheiro;    // "Você não tem dinheiro suficiente"
    [SerializeField] private GameObject mensagemJaEstaInteiro;  // "O item já está com 100% de vida"

    [Header("Spawn")]
    [SerializeField] private Transform pontoSpawn;

    [Header("Preço")]
    [Tooltip("Custo em REIN por 1 ponto de vida faltando")]
    [SerializeField] private float precoPorPontoDeVida = 0.1f;

    // Internos
    private GameObject itemNoSlot;
    private Component equipamentoNoSlot;
    private float valorAtualDoReparo;
    private StatusPlayer statusPlayer;
    private XRSocketInteractor socketSlot;

    private static readonly string[] TiposReparaveis =
    {
        "Equipamento",
        "Espada",
        "Machado",
        "Picareta",
        "Arco",
        "Escudo"
    };

    private void Start()
    {
        EsconderTodasMensagens();

        statusPlayer = FindFirstObjectByType<StatusPlayer>();

        if (botaoReparar != null)
            botaoReparar.onClick.AddListener(TentarReparar);

        // Se o Slot tiver um XR Socket Interactor, escutamos os eventos dele para
        // atualizar o preço IMEDIATAMENTE ao encaixar o item — sem esperar o próximo
        // frame e sem depender do item virar filho na hierarquia do slot (o que o
        // XR Socket Interactor não faz por padrão).
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
        if (socketSlot != null)
        {
            socketSlot.selectEntered.RemoveListener(OnItemEncaixado);
            socketSlot.selectExited.RemoveListener(OnItemRemovido);
        }
    }

    private void OnItemEncaixado(SelectEnterEventArgs args)
    {
        GameObject item = args.interactableObject != null ? args.interactableObject.transform.gameObject : null;
        ProcessarItemDetectado(item);
    }

    private void OnItemRemovido(SelectExitEventArgs args)
    {
        GameObject item = args.interactableObject != null ? args.interactableObject.transform.gameObject : null;

        // Só limpa se o item removido era realmente o que estava sendo exibido.
        if (item == itemNoSlot)
            ProcessarItemDetectado(null);
    }

    private void Update()
    {
        // Fallback: só faz polling por Transform se o slot NÃO for um XR Socket
        // Interactor (ex.: um slot simples de UI onde o item vira filho do slot).
        if (socketSlot == null)
            VerificarItemNoSlot();
    }

    private void VerificarItemNoSlot()
    {
        if (slot == null) return;

        GameObject itemAtual = slot.childCount > 0 ? slot.GetChild(0).gameObject : null;
        ProcessarItemDetectado(itemAtual);
    }

    /// <summary>
    /// Ponto único de processamento: chamado tanto pelo polling (Update)
    /// quanto pelos eventos do XR Socket Interactor.
    /// </summary>
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

        equipamentoNoSlot = ObterComponenteReparavel(itemAtual);

        if (equipamentoNoSlot != null && TentarCalcularVidaFaltando(equipamentoNoSlot, out float faltando))
        {
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
        {
            return;
        }

        GameObject item = itemNoSlot;
        Component equip = equipamentoNoSlot;
        float preco = valorAtualDoReparo;
        float faltando = equip != null && TentarCalcularVidaFaltando(equip, out float valorFaltando) ? valorFaltando : 0f;

        MoverItemParaSpawn(item);

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
        {
            return;
        }

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

        if (!TentarRepararComponente(equip))
            return;

        carteira.AdicionarReinUnidades(-custoEmUnidades);

        if (mensagemSucesso != null)
            mensagemSucesso.SetActive(true);

    }

    private void MoverItemParaSpawn(GameObject item)
    {
        if (item == null) return;

        item.transform.SetParent(null);

        if (pontoSpawn != null)
        {
            item.transform.position = pontoSpawn.position;
            item.transform.rotation = pontoSpawn.rotation;
        }

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = false;
    }

    private static Component ObterComponenteReparavel(GameObject item)
    {
        if (item == null)
            return null;

        Component[] componentes = item.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < componentes.Length; i++)
        {
            Component componente = componentes[i];
            if (componente == null || !TipoEhReparavel(componente.GetType()))
                continue;

            if (TentarCalcularVidaFaltando(componente, out _))
                return componente;
        }

        return null;
    }

    private static bool TipoEhReparavel(Type tipo)
    {
        if (tipo == null)
            return false;

        string nomeTipo = tipo.Name;
        for (int i = 0; i < TiposReparaveis.Length; i++)
        {
            if (string.Equals(nomeTipo, TiposReparaveis[i], StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool TentarCalcularVidaFaltando(Component componente, out float faltando)
    {
        faltando = 0f;

        if (componente == null)
            return false;

        if (TentarLerNumero(componente, "VidaFaltando", out faltando))
        {
            faltando = Mathf.Max(0f, faltando);
            return true;
        }

        if (TentarLerMaximoEAtual(componente, out float maximo, out float atual))
        {
            faltando = Mathf.Max(0f, maximo - atual);
            return true;
        }

        return false;
    }

    private static bool TentarRepararComponente(Component componente)
    {
        if (componente == null || !TentarLerMaximoEAtual(componente, out float maximo, out _))
            return false;

        Type tipo = componente.GetType();
        MethodInfo repararCompleto = tipo.GetMethod(
            "RepararCompleto",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (repararCompleto != null && repararCompleto.GetParameters().Length == 0)
        {
            repararCompleto.Invoke(componente, null);
            AtualizarPersistenciaDurabilidade(componente);
            return true;
        }

        bool escreveu =
            TentarEscreverNumero(componente, "vidaAtual", maximo) ||
            TentarEscreverNumero(componente, "VidaAtual", maximo) ||
            TentarEscreverNumero(componente, "durabilidadeAtual", maximo) ||
            TentarEscreverNumero(componente, "DurabilidadeAtual", maximo) ||
            TentarEscreverNumero(componente, "durabilidade", maximo);

        if (!escreveu)
            return false;

        TentarEscreverBooleano(componente, "quebrado", false);
        TentarEscreverBooleano(componente, "quebrada", false);
        TentarEscreverBooleano(componente, "arcoQuebrado", false);
        InvocarAtualizacaoVisual(componente);
        AtualizarPersistenciaDurabilidade(componente);
        return true;
    }

    private static bool TentarLerMaximoEAtual(Component componente, out float maximo, out float atual)
    {
        maximo = 0f;
        atual = 0f;

        return (TentarLerNumero(componente, "VidaMaxima", out maximo) ||
                TentarLerNumero(componente, "vidaMaxima", out maximo) ||
                TentarLerNumero(componente, "DurabilidadeMaxima", out maximo) ||
                TentarLerNumero(componente, "durabilidadeMaxima", out maximo)) &&
               (TentarLerNumero(componente, "VidaAtual", out atual) ||
                TentarLerNumero(componente, "vidaAtual", out atual) ||
                TentarLerNumero(componente, "DurabilidadeAtual", out atual) ||
                TentarLerNumero(componente, "durabilidadeAtual", out atual) ||
                TentarLerNumero(componente, "durabilidade", out atual));
    }

    private static bool TentarLerNumero(Component componente, string nome, out float valor)
    {
        valor = 0f;

        if (componente == null || string.IsNullOrWhiteSpace(nome))
            return false;

        Type tipo = componente.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        PropertyInfo propriedade = tipo.GetProperty(nome, flags);
        if (propriedade != null && LerNumero(propriedade.GetValue(componente), out valor))
            return true;

        FieldInfo campo = tipo.GetField(nome, flags);
        return campo != null && LerNumero(campo.GetValue(componente), out valor);
    }

    private static bool TentarEscreverNumero(Component componente, string nome, float valor)
    {
        if (componente == null || string.IsNullOrWhiteSpace(nome))
            return false;

        Type tipo = componente.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        FieldInfo campo = tipo.GetField(nome, flags);
        if (campo != null)
            return EscreverNumero(campo, componente, valor);

        PropertyInfo propriedade = tipo.GetProperty(nome, flags);
        return propriedade != null && propriedade.CanWrite && EscreverNumero(propriedade, componente, valor);
    }

    private static bool TentarEscreverBooleano(Component componente, string nome, bool valor)
    {
        if (componente == null || string.IsNullOrWhiteSpace(nome))
            return false;

        Type tipo = componente.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        FieldInfo campo = tipo.GetField(nome, flags);
        if (campo != null && campo.FieldType == typeof(bool))
        {
            campo.SetValue(componente, valor);
            return true;
        }

        PropertyInfo propriedade = tipo.GetProperty(nome, flags);
        if (propriedade != null && propriedade.CanWrite && propriedade.PropertyType == typeof(bool))
        {
            propriedade.SetValue(componente, valor);
            return true;
        }

        return false;
    }

    private static bool LerNumero(object origem, out float valor)
    {
        valor = 0f;

        if (origem is int inteiro)
        {
            valor = inteiro;
            return true;
        }

        if (origem is float flutuante)
        {
            valor = flutuante;
            return true;
        }

        return false;
    }

    private static bool EscreverNumero(FieldInfo campo, Component componente, float valor)
    {
        if (campo.FieldType == typeof(int))
        {
            campo.SetValue(componente, Mathf.RoundToInt(valor));
            return true;
        }

        if (campo.FieldType == typeof(float))
        {
            campo.SetValue(componente, valor);
            return true;
        }

        return false;
    }

    private static bool EscreverNumero(PropertyInfo propriedade, Component componente, float valor)
    {
        if (propriedade.PropertyType == typeof(int))
        {
            propriedade.SetValue(componente, Mathf.RoundToInt(valor));
            return true;
        }

        if (propriedade.PropertyType == typeof(float))
        {
            propriedade.SetValue(componente, valor);
            return true;
        }

        return false;
    }

    private static void InvocarAtualizacaoVisual(Component componente)
    {
        MethodInfo atualizarVisual = componente.GetType().GetMethod(
            "AtualizarDurabilidadeVisual",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        atualizarVisual?.Invoke(componente, null);
    }

    private static void AtualizarPersistenciaDurabilidade(Component componente)
    {
        ItemPersistente persistente = componente.GetComponentInParent<ItemPersistente>();
        if (persistente != null)
            persistente.LerDurabilidade();
    }
}
