using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
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
        useDynamicAttach = false;
        CorrigirParentInteractableAutoReferencia();
    }

    protected override void OnEnable()
    {
        CorrigirParentInteractableAutoReferencia();
        base.OnEnable();
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
