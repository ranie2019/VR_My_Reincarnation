using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class EquiparManoplas : MonoBehaviour
{
    [Header("Popup")]
    [SerializeField] private GameObject popupInfo;
    [SerializeField] private float tempoParaFecharPopup = 10f;

    [Header("Prefab Completo")]
    [Tooltip(
        "Referência opcional ao prefab completo da manopla. " +
        "O objeto raiz deste componente será movido por inteiro."
    )]
    [SerializeField] private GameObject luvaVisual;

    [Header("Mão do Avatar")]
    [SerializeField] private Transform transformMaoAvatar;

    [Tooltip("Renderer opcional da mão nua do avatar.")]
    [SerializeField] private SkinnedMeshRenderer rendererMaoAvatar;

    [SerializeField] private bool esconderMaoAoEquipar = false;

    [Header("Escala")]
    [Tooltip(
        "Escala usada somente depois que o botão Equipar for chamado. " +
        "Esta escala não é aplicada no Awake nem ao iniciar o jogo."
    )]
    public Vector3 escalaEquipado = Vector3.one;

    private XRBaseInteractable interactable;
    private Coroutine corotinaFecharPopup;

    private bool jaEquipado;

    private Transform paiOriginal;
    private Vector3 escalaOriginalLocal;
    private Vector3 posicaoOriginalLocal;
    private Quaternion rotacaoOriginalLocal;

    public bool JaEquipado => jaEquipado;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();

        if (interactable == null)
        {
            Debug.LogWarning(
                "[EquiparManoplas] Nenhum XRBaseInteractable encontrado em: " +
                gameObject.name,
                this
            );
        }

        if (popupInfo != null)
            popupInfo.SetActive(false);

        // Apenas salva os valores originais.
        // Não altera a escala neste momento.
        SalvarTransformOriginal();
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
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        CancelarFechamentoPendente();
    }

    private void SalvarTransformOriginal()
    {
        paiOriginal = transform.parent;
        escalaOriginalLocal = transform.localScale;
        posicaoOriginalLocal = transform.localPosition;
        rotacaoOriginalLocal = transform.localRotation;
    }

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

    public void Equipar()
    {
        if (jaEquipado)
            return;

        if (!ValidarReferencias())
            return;

        // A escala original já foi preservada no Awake.
        // A escala equipada só é aplicada neste momento.
        jaEquipado = true;

        EncaixarPrefabCompletoNaMao();

        DesativarInteracaoDoItem();

        DesativarFisicaDoItem();

        EsconderMaoSeConfigurado();

        AplicarAtributos();

        FinalizarEquipamento();
    }

    private bool ValidarReferencias()
    {
        if (transformMaoAvatar == null)
        {
            Debug.LogWarning(
                "[EquiparManoplas] O campo 'Transform Mao Avatar' não foi atribuído em: " +
                gameObject.name,
                this
            );

            return false;
        }

        return true;
    }

    private void EncaixarPrefabCompletoNaMao()
    {
        Transform raizPrefab = transform;

        if (raizPrefab == null || transformMaoAvatar == null)
            return;

        raizPrefab.gameObject.SetActive(true);

        // O prefab completo passa a acompanhar a mão.
        raizPrefab.SetParent(transformMaoAvatar, false);

        // Mantém o alinhamento atual usado pela lógica de equipamento.
        raizPrefab.localPosition = Vector3.zero;
        raizPrefab.localRotation = Quaternion.identity;

        // A escala equipada é aplicada somente quando Equipar() é chamado.
        raizPrefab.localScale = escalaEquipado;

        AtivarHierarquiaCompleta(raizPrefab);
    }

    private void AtivarHierarquiaCompleta(Transform raiz)
    {
        if (raiz == null)
            return;

        raiz.gameObject.SetActive(true);

        for (int i = 0; i < raiz.childCount; i++)
        {
            AtivarHierarquiaCompleta(raiz.GetChild(i));
        }
    }

    private void DesativarInteracaoDoItem()
    {
        XRBaseInteractable[] interactables =
            GetComponentsInChildren<XRBaseInteractable>(true);

        for (int i = 0; i < interactables.Length; i++)
        {
            XRBaseInteractable itemInteractable = interactables[i];

            if (itemInteractable != null)
                itemInteractable.enabled = false;
        }

        if (popupInfo != null)
            popupInfo.SetActive(false);
    }

    private void DesativarFisicaDoItem()
    {
        Rigidbody[] rigidbodies =
            GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];

            if (rb == null)
                continue;

            rb.isKinematic = true;
            rb.detectCollisions = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Collider[] colliders =
            GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider != null)
                collider.enabled = false;
        }
    }

    private void EsconderMaoSeConfigurado()
    {
        if (!esconderMaoAoEquipar)
            return;

        if (rendererMaoAvatar != null)
            rendererMaoAvatar.enabled = false;
    }

    private void AplicarAtributos()
    {
        // Reservado para futuros bônus e efeitos da manopla.
        // Os scripts do prefab permanecem ativos.
    }

    private void FinalizarEquipamento()
    {
        if (popupInfo != null)
            popupInfo.SetActive(false);

        // O prefab completo permanece ativo na mão.
        gameObject.SetActive(true);
    }

    public void Desequipar()
    {
        if (!jaEquipado)
            return;

        RestaurarInteracaoDoItem();

        RestaurarFisicaDoItem();

        if (rendererMaoAvatar != null)
            rendererMaoAvatar.enabled = true;

        if (paiOriginal != null)
        {
            transform.SetParent(paiOriginal, false);
            transform.localPosition = posicaoOriginalLocal;
            transform.localRotation = rotacaoOriginalLocal;

            // Ao desequipar, retorna exatamente para a escala original.
            transform.localScale = escalaOriginalLocal;
        }

        jaEquipado = false;
    }

    private void RestaurarInteracaoDoItem()
    {
        XRBaseInteractable[] interactables =
            GetComponentsInChildren<XRBaseInteractable>(true);

        for (int i = 0; i < interactables.Length; i++)
        {
            XRBaseInteractable itemInteractable = interactables[i];

            if (itemInteractable != null)
                itemInteractable.enabled = true;
        }
    }

    private void RestaurarFisicaDoItem()
    {
        Rigidbody[] rigidbodies =
            GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];

            if (rb == null)
                continue;

            rb.isKinematic = false;
            rb.detectCollisions = true;
        }

        Collider[] colliders =
            GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider != null)
                collider.enabled = true;
        }
    }
}
