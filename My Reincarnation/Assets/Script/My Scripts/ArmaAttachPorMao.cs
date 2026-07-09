using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ArmaAttachPorMao : XRGrabInteractable
{
    [Header("Pontos de Attach por Mão")]
    [SerializeField] private Transform pontoMaoDireita;
    [SerializeField] private Transform pontoMaoEsquerda;

    protected override void Awake()
    {
        base.Awake();
        useDynamicAttach = false;
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
}