using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ArmaAttachPorMao : XRGrabInteractable
{
    [Header("Pontos de Attach por Mão")]
    [SerializeField] private Transform pontoMaoDireita;
    [SerializeField] private Transform pontoMaoEsquerda;

    private bool pegadaDireitaAtiva;
    private bool pegadaEsquerdaAtiva;

    protected override void Awake()
    {
        base.Awake();
        ConfigurarEncaixeImediato();
        CorrigirParentInteractableAutoReferencia();
    }

    protected override void Reset()
    {
        base.Reset();
        ConfigurarEncaixeImediato();
    }

    private void OnValidate()
    {
        ConfigurarEncaixeImediato();
    }

    protected override void OnEnable()
    {
        CorrigirParentInteractableAutoReferencia();
        base.OnEnable();
    }

    protected override void OnSelectEntering(SelectEnterEventArgs args)
    {
        base.OnSelectEntering(args);

        if (TentarDetectarLadoMao(args.interactorObject, out _))
            EncaixarImediatamenteNaMao(args.interactorObject);
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);

        if (!TentarDetectarLadoMao(args.interactorObject, out bool direita))
            return;

        if (direita && !pegadaDireitaAtiva)
        {
            pegadaDireitaAtiva = true;
            AnimacaoPegadaMaoVR.NotificarPegada(true, true);
        }
        else if (!direita && !pegadaEsquerdaAtiva)
        {
            pegadaEsquerdaAtiva = true;
            AnimacaoPegadaMaoVR.NotificarPegada(false, true);
        }
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        bool ladoDetectado = TentarDetectarLadoMao(args.interactorObject, out bool direita);
        base.OnSelectExited(args);

        if (!ladoDetectado)
            return;

        if (direita && pegadaDireitaAtiva)
        {
            pegadaDireitaAtiva = false;
            AnimacaoPegadaMaoVR.NotificarPegada(true, false);
        }
        else if (!direita && pegadaEsquerdaAtiva)
        {
            pegadaEsquerdaAtiva = false;
            AnimacaoPegadaMaoVR.NotificarPegada(false, false);
        }
    }

    protected override void OnDisable()
    {
        LiberarAnimacoesAtivas();
        CorrigirParentInteractableAutoReferencia();
        base.OnDisable();
    }

    public override Transform GetAttachTransform(IXRInteractor interactor)
    {
        // Pega o transform do interactor como MonoBehaviour
        Transform t = (interactor as MonoBehaviour)?.transform;

        // Sobe a hierarquia procurando "left" ou "right" em qualquer pai
        while (t != null)
        {
            string nome = t.name.ToLower();

            if (nome.Contains("left") || nome.Contains("esquerda"))
                return pontoMaoEsquerda != null ? pontoMaoEsquerda : base.GetAttachTransform(interactor);

            if (nome.Contains("right") || nome.Contains("direita"))
                return pontoMaoDireita != null ? pontoMaoDireita : base.GetAttachTransform(interactor);

            t = t.parent;
        }

        return base.GetAttachTransform(interactor);
    }

    private static bool TentarDetectarLadoMao(IXRInteractor interactor, out bool direita)
    {
        direita = false;
        if (interactor == null || interactor is XRSocketInteractor)
            return false;

        Transform atual = (interactor as MonoBehaviour)?.transform;
        while (atual != null)
        {
            string nome = atual.name.Trim().ToLowerInvariant();
            if (nome.Contains("right") || nome.Contains("direita"))
            {
                direita = true;
                return true;
            }

            if (nome.Contains("left") || nome.Contains("esquerda"))
                return true;

            atual = atual.parent;
        }

        return false;
    }

    private void ConfigurarEncaixeImediato()
    {
        // O ponto de pegada configurado no objeto deve ir direto ao attach da mão.
        // Qualquer valor maior que zero faz o XR Grab Interactable interpolar a
        // posição e a rotação desde o local distante até o controle.
        useDynamicAttach = false;
        attachEaseInTime = 0f;

        // O Ray Interactor do jogador usa Force Grab desativado. Sem esta
        // sobrescrita, ele move o próprio attach para o ponto atingido pelo raio
        // e mantém a arma flutuando longe da mão.
        farAttachMode = InteractableFarAttachMode.Near;
    }

    private void EncaixarImediatamenteNaMao(IXRSelectInteractor interactor)
    {
        if (interactor == null)
            return;

        Transform pontoObjeto = GetAttachTransform(interactor);
        Transform pontoInteractor = interactor.GetAttachTransform(this);
        if (pontoObjeto == null || pontoInteractor == null)
            return;

        // Primeiro iguala a rotação dos pontos de attach. Depois da rotação,
        // recalcula a posição do ponto do objeto e elimina o deslocamento restante.
        Quaternion deltaRotacao = pontoInteractor.rotation * Quaternion.Inverse(pontoObjeto.rotation);
        Quaternion rotacaoObjeto = deltaRotacao * transform.rotation;
        transform.rotation = rotacaoObjeto;

        Vector3 posicaoObjeto = transform.position + (pontoInteractor.position - pontoObjeto.position);
        transform.SetPositionAndRotation(posicaoObjeto, rotacaoObjeto);
    }

    private void LiberarAnimacoesAtivas()
    {
        if (pegadaDireitaAtiva)
        {
            pegadaDireitaAtiva = false;
            AnimacaoPegadaMaoVR.NotificarPegada(true, false);
        }

        if (pegadaEsquerdaAtiva)
        {
            pegadaEsquerdaAtiva = false;
            AnimacaoPegadaMaoVR.NotificarPegada(false, false);
        }
    }

    private void CorrigirParentInteractableAutoReferencia()
    {
        autoFindParentInteractableInHierarchy = false;

        if (ReferenceEquals(parentInteractable, (IXRInteractable)this))
            parentInteractable = null;

        XRBaseInteractable[] interactables = GetComponentsInChildren<XRBaseInteractable>(true);
        for (int i = 0; i < interactables.Length; i++)
        {
            XRBaseInteractable interactable = interactables[i];
            if (interactable != null && ReferenceEquals(interactable.parentInteractable, (IXRInteractable)interactable))
                interactable.parentInteractable = null;
        }
    }
}
