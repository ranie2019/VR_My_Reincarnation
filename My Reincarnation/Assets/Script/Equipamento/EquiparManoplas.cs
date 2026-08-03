using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Fica no objeto raiz do item de armadura (ex: "Luva Medieval D/E").
///
/// Ao equipar:
/// - Move o luvaVisual para fora da hierarquia do item ANTES de desativar o item.
/// - Desativa apenas BoxColliders e Rigidbody — SkinnedMeshRenderer permanece ativo.
/// - Desativa o objeto raiz do item (Luva Medieval D/E). Como a luvaVisual
///   ja foi removida antes, ela continua ativa e visivel na mao.
/// </summary>
public class EquiparManoplas : MonoBehaviour
{
    [Header("Popup")]
    [Tooltip("Canvas com as informacoes e o botao EQUIP. Comeca desativado.")]
    [SerializeField] private GameObject popupInfo;

    [Tooltip("Tempo em segundos antes do popup fechar apos o laser sair do item.")]
    [SerializeField] private float tempoParaFecharPopup = 10f;

    [Header("Visual da Luva")]
    [Tooltip(
        "Filho INTERNO da luva que contem o mesh (ex: o objeto 'Luva' dentro de 'Luva Medieval E').\n" +
        "NAO arraste o objeto raiz 'Luva Medieval E' aqui — se fizer isso o codigo avisa no Console.")]
    [SerializeField] private GameObject luvaVisual;

    [Header("Mao do Avatar")]
    [Tooltip("Transform da mao do avatar (ex: 'Mao Esquerda' ou 'Mao Direita'). A luva vira filha deste Transform.")]
    [SerializeField] private Transform transformMaoAvatar;

    [Tooltip("OPCIONAL — SkinnedMeshRenderer da mao nua. Se preenchido e 'Esconder Mao' marcado, so o renderer some.")]
    [SerializeField] private SkinnedMeshRenderer rendererMaoAvatar;

    [Tooltip("Se marcado, esconde o renderer da mao nua ao equipar. Deixe desmarcado para ver mao + luva juntas.")]
    [SerializeField] private bool esconderMaoAoEquipar = false;

    // [Header("Atributos (futuro)")]
    // [SerializeField] private AtributosArmadura atributos;

    private XRBaseInteractable interactable;
    private bool jaEquipado;
    private Coroutine corotinaFecharPopup;

    public bool JaEquipado => jaEquipado;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();

        if (interactable == null)
            Debug.LogWarning("[EquiparArmadura] Nenhum XRBaseInteractable encontrado em: " + gameObject.name, this);

        if (popupInfo != null)
            popupInfo.SetActive(false);
    }

    private void OnEnable()
    {
        if (interactable == null)
            return;

        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
    }

    private void OnDisable()
    {
        if (interactable == null)
            return;

        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
    }

    // -------------------------------------------------------------------------
    // Hover — popup
    // -------------------------------------------------------------------------

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        CancelarFechamentoPendente();

        if (popupInfo != null)
            popupInfo.SetActive(true);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        CancelarFechamentoPendente();
        corotinaFecharPopup = StartCoroutine(FecharPopupAposEspera());
    }

    private IEnumerator FecharPopupAposEspera()
    {
        yield return new WaitForSeconds(tempoParaFecharPopup);

        if (popupInfo != null)
            popupInfo.SetActive(false);

        corotinaFecharPopup = null;
    }

    private void CancelarFechamentoPendente()
    {
        if (corotinaFecharPopup == null)
            return;

        StopCoroutine(corotinaFecharPopup);
        corotinaFecharPopup = null;
    }

    // -------------------------------------------------------------------------
    // Equipar
    // -------------------------------------------------------------------------

    public void Equipar()
    {
        if (jaEquipado)
        {
            Debug.Log("[EquiparArmadura] Ja equipado, ignorando chamada.", this);
            return;
        }

        if (!ValidarReferencias())
            return;

        jaEquipado = true;

        // ORDEM CRITICA:
        // 1. Tira a luva da hierarquia do item ANTES de desativar o item.
        EncaixarLuvaNaMao();

        // 2. Desativa so BoxCollider e Rigidbody do item raiz.
        DesativarFisicaDoItem();

        // 3. Esconde renderer da mao nua (opcional).
        EsconderMaoSeConfigurado();

        // 4. Atributos futuros.
        AplicarAtributos();

        // 5. Desativa o item do chao. A luvaVisual ja foi removida, nao e afetada.
        DesativarItemDoChao();

        Debug.Log("[EquiparArmadura] Equipado com sucesso. Luva agora e filha de: " + transformMaoAvatar.name, this);
    }

    private bool ValidarReferencias()
    {
        if (luvaVisual == null)
        {
            Debug.LogWarning("[EquiparArmadura] 'Luva Visual' nao atribuida em: " + gameObject.name +
                             "\nArraste o filho 'Luva' (nao o objeto raiz 'Luva Medieval') para este campo.", this);
            return false;
        }

        // Protecao: avisa se o luvaVisual for o proprio objeto raiz.
        // Se for, o SetActive(false) do item desativaria a luva junto.
        if (luvaVisual == gameObject)
        {
            Debug.LogWarning("[EquiparArmadura] 'Luva Visual' esta apontando para o proprio objeto raiz '" + gameObject.name + "'!" +
                           "\nIsto faz a luva sumir ao equipar. Arraste um filho interno (ex: 'Luva') para este campo.", this);
            return false;
        }

        if (transformMaoAvatar == null)
        {
            Debug.LogWarning("[EquiparArmadura] 'Transform Mao Avatar' nao atribuido em: " + gameObject.name +
                             "\nArraste 'Mao Direita' ou 'Mao Esquerda' do Avatar para este campo.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Move a luvaVisual para fora da hierarquia do item e encaixa na mao do avatar.
    /// Depois disso, desativar o item raiz NAO afeta a luva.
    /// </summary>
    private void EncaixarLuvaNaMao()
    {
        // Salva rotacao e escala locais originais antes de mudar o pai.
        Quaternion rotacaoOriginal = luvaVisual.transform.localRotation;
        Vector3 escalaOriginal = luvaVisual.transform.localScale;

        // Muda o pai. worldPositionStays: false para podermos definir
        // os valores locais manualmente logo abaixo.
        luvaVisual.transform.SetParent(transformMaoAvatar, false);

        // Posicao zerada: a luva fica exatamente sobre a mao.
        luvaVisual.transform.localPosition = Vector3.zero;

        // Rotacao e escala preservadas dos valores originais da luva.
        luvaVisual.transform.localRotation = rotacaoOriginal;
        luvaVisual.transform.localScale = escalaOriginal;

        // Garante ativo — pode ter vindo desativado do prefab.
        luvaVisual.SetActive(true);

        Debug.Log("[EquiparArmadura] Luva encaixada em: " + transformMaoAvatar.name, this);
    }

    /// <summary>
    /// Desativa apenas BoxCollider e Rigidbody do item raiz.
    /// SkinnedMeshRenderer e os outros componentes da luva nao sao tocados.
    /// </summary>
    private void DesativarFisicaDoItem()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        BoxCollider[] boxColliders = GetComponentsInChildren<BoxCollider>(true);
        for (int i = 0; i < boxColliders.Length; i++)
        {
            if (boxColliders[i] != null)
                boxColliders[i].enabled = false;
        }
    }

    private void EsconderMaoSeConfigurado()
    {
        if (!esconderMaoAoEquipar || rendererMaoAvatar == null)
            return;

        rendererMaoAvatar.enabled = false;
        Debug.Log("[EquiparArmadura] Renderer da mao desativado: " + rendererMaoAvatar.name, this);
    }

    private void AplicarAtributos()
    {
        // Futuro:
        // if (atributos == null) return;
        // StatusPlayer status = FindAnyObjectByType<StatusPlayer>();
        // if (status != null) status.AplicarBonus(atributos);
    }

    /// <summary>
    /// Desativa o objeto raiz do item (Luva Medieval D/E).
    /// A luvaVisual ja foi removida da hierarquia dele em EncaixarLuvaNaMao(),
    /// entao ela NAO e afetada por este SetActive(false).
    /// </summary>
    private void DesativarItemDoChao()
    {
        if (popupInfo != null)
            popupInfo.SetActive(false);

        gameObject.SetActive(false);
    }
}