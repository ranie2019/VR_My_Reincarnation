using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[DisallowMultipleComponent]
public class XRHandAttachSwitcher : MonoBehaviour
{
    [Header("Attach Points")]
    public Transform attachRight;
    public Transform attachLeft;

    [Header("Detecção (fallback por nome)")]
    [Tooltip("Se o nome do interactor tiver esse texto, considera Mão Esquerda.")]
    public string keywordLeft = "left";
    [Tooltip("Se o nome do interactor tiver esse texto, considera Mão Direita.")]
    public string keywordRight = "right";

    [Header("Opções")]
    [Tooltip("Desliga o Dynamic Attach para não bagunçar offsets.")]
    public bool forceDisableDynamicAttach = true;

    XRGrabInteractable grab;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (!grab)
        {
            Debug.LogError("[XRHandAttachSwitcher] Falta XRGrabInteractable neste objeto.", this);
            enabled = false;
            return;
        }

        if (forceDisableDynamicAttach)
            grab.useDynamicAttach = false;

        grab.selectEntered.AddListener(OnSelectEntered);
    }

    void OnDestroy()
    {
        if (grab != null)
            grab.selectEntered.RemoveListener(OnSelectEntered);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        bool isLeft = IsLeftHand(args.interactorObject);

        if (isLeft)
        {
            if (attachLeft != null) grab.attachTransform = attachLeft;
            else Debug.LogWarning("[XRHandAttachSwitcher] attachLeft está vazio.", this);
        }
        else
        {
            if (attachRight != null) grab.attachTransform = attachRight;
            else Debug.LogWarning("[XRHandAttachSwitcher] attachRight está vazio.", this);
        }
    }

    bool IsLeftHand(IXRSelectInteractor interactor)
    {
        if (interactor == null) return false;

        // 1) Tenta pelo nome do interactor
        string n = interactor.transform.name.ToLower();
        if (!string.IsNullOrEmpty(keywordLeft) && n.Contains(keywordLeft.ToLower())) return true;
        if (!string.IsNullOrEmpty(keywordRight) && n.Contains(keywordRight.ToLower())) return false;

        // 2) Tenta pelo nome do pai (muito comum: "LeftHand Controller", etc)
        if (interactor.transform.parent != null)
        {
            string p = interactor.transform.parent.name.ToLower();
            if (!string.IsNullOrEmpty(keywordLeft) && p.Contains(keywordLeft.ToLower())) return true;
            if (!string.IsNullOrEmpty(keywordRight) && p.Contains(keywordRight.ToLower())) return false;
        }

        // 3) Fallback: assume direita (pra não travar)
        return false;
    }
}
