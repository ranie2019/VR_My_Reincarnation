using UnityEngine;


public class ScaleObject : MonoBehaviour
{
    public Vector3 targetScale = Vector3.one;
    private Vector3 originalScale = Vector3.one;

    public void ApplyTargetScale(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable)
    {
        originalScale = interactable.transform.localScale;
        interactable.transform.localScale = targetScale;
    }

    public void ResetScale(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable)
    {
        interactable.transform.localScale = originalScale;
        originalScale = Vector3.one;
    }
}
