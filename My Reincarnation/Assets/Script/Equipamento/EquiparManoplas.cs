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
        "Mantido por compatibilidade com prefabs antigos. " +
        "A escala original do prefab é preservada."
    )]
    public Vector3 escalaEquipado = Vector3.one;

    private XRBaseInteractable interactable;
    private Coroutine corotinaFecharPopup;

    private bool jaEquipado;

    private Transform paiOriginal;
    private Vector3 escalaOriginalLocal;
    private Vector3 posicaoOriginalLocal;
    private Quaternion rotacaoOriginalLocal;

    private Vector3 escalaOriginalMundo;
    private bool escalaOriginalMundoCapturada;

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

        SalvarTransformOriginal();
    }

    private void Start()
    {
        /*
         * Captura a escala real depois dos Awake dos outros componentes.
         * Não altera o tamanho visual original da manopla.
         */
        escalaOriginalMundo = transform.lossyScale;
        escalaOriginalMundoCapturada = true;

        AplicarEscalaMundoComoLocal(escalaOriginalMundo);
    }

    private void LateUpdate()
    {
        /*
         * Mantém a escala original enquanto a manopla não estiver equipada.
         */
        if (!jaEquipado &&
            escalaOriginalMundoCapturada &&
            isActiveAndEnabled &&
            gameObject.activeInHierarchy)
        {
            AplicarEscalaMundoComoLocal(escalaOriginalMundo);
        }
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
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        CancelarFechamentoPendente();

        if (popupInfo != null)
            popupInfo.SetActive(true);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        CancelarFechamentoPendente();

        /*
         * Ao guardar a manopla no inventário, o GameObject pode ser
         * desativado antes de o XRI terminar de disparar o hoverExited.
         * Nesse caso, não se pode iniciar uma coroutine.
         */
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        corotinaFecharPopup = StartCoroutine(FecharPopupAposEspera());
    }

    private IEnumerator FecharPopupAposEspera()
    {
        yield return new WaitForSeconds(tempoParaFecharPopup);

        /*
         * O item pode ter sido guardado no inventário durante a espera.
         */
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            corotinaFecharPopup = null;
            yield break;
        }

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
        if (transformMaoAvatar == null)
            return;

        gameObject.SetActive(true);

        /*
         * Mantém a escala de mundo durante a troca de pai.
         */
        transform.SetParent(transformMaoAvatar, true);

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        /*
         * Não utiliza escalaEquipado nem Vector3.one.
         * A escala original do prefab é preservada.
         */
        if (escalaOriginalMundoCapturada)
        {
            AplicarEscalaMundoComoLocal(escalaOriginalMundo);
        }
        else
        {
            transform.localScale = escalaOriginalLocal;
        }

        AtivarHierarquiaCompleta(transform);
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
            if (interactables[i] != null)
                interactables[i].enabled = false;
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
            if (colliders[i] != null)
                colliders[i].enabled = false;
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
    }

    private void FinalizarEquipamento()
    {
        if (popupInfo != null)
            popupInfo.SetActive(false);

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

            if (escalaOriginalMundoCapturada)
                AplicarEscalaMundoComoLocal(escalaOriginalMundo);
            else
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
            if (interactables[i] != null)
                interactables[i].enabled = true;
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
            if (colliders[i] != null)
                colliders[i].enabled = true;
        }
    }

    private void AplicarEscalaMundoComoLocal(Vector3 escalaMundo)
    {
        Vector3 escalaDoPai = transform.parent != null
            ? transform.parent.lossyScale
            : Vector3.one;

        Vector3 novaEscalaLocal = new Vector3(
            DividirSeguro(escalaMundo.x, escalaDoPai.x),
            DividirSeguro(escalaMundo.y, escalaDoPai.y),
            DividirSeguro(escalaMundo.z, escalaDoPai.z)
        );

        if (novaEscalaLocal != transform.localScale)
            transform.localScale = novaEscalaLocal;
    }

    private static float DividirSeguro(float valor, float divisor)
    {
        if (
            Mathf.Approximately(divisor, 0f) ||
            float.IsNaN(divisor) ||
            float.IsInfinity(divisor)
        )
        {
            return valor;
        }

        float resultado = valor / divisor;

        if (float.IsNaN(resultado) || float.IsInfinity(resultado))
            return valor;

        return resultado;
    }
}
