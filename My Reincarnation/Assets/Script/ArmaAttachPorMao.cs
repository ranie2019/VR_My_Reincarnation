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

        // Garante que o XRI não crie um attach dinâmico automático
        useDynamicAttach = false;
    }

    // Método obrigatório do XRGrabInteractable
    public override Transform GetAttachTransform(IXRInteractor interator)
    {
        XRBaseInteractor interatorBase = interator as XRBaseInteractor;

        if (interatorBase != null)
        {
            string nomeInterator = interatorBase.name.ToLower();

            if (nomeInterator.Contains("left") || nomeInterator.Contains("esquerda"))
            {
                if (pontoMaoEsquerda != null)
                    return pontoMaoEsquerda;
            }

            if (nomeInterator.Contains("right") || nomeInterator.Contains("direita"))
            {
                if (pontoMaoDireita != null)
                    return pontoMaoDireita;
            }
        }

        // Caso não identifique, usa comportamento padrão
        return base.GetAttachTransform(interator);
    }
}