using UnityEngine;


public class ScaleObject : MonoBehaviour
{
    public Vector3 targetScale = Vector3.one;
    private Vector3 originalScale = Vector3.one;

    public void ApplyTargetScale(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable)
    {
        if (interactable == null || !EscalaValida(targetScale))
            return;

        originalScale = interactable.transform.localScale;
        interactable.transform.localScale = targetScale;
    }

    public void ResetScale(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable)
    {
        if (interactable == null || !EscalaValida(originalScale))
            return;

        interactable.transform.localScale = originalScale;
        originalScale = Vector3.one;
    }

    private static bool EscalaValida(Vector3 escala)
    {
        const float minimo = 0.0001f;
        return ValorFinito(escala.x) && ValorFinito(escala.y) && ValorFinito(escala.z) &&
               Mathf.Abs(escala.x) > minimo &&
               Mathf.Abs(escala.y) > minimo &&
               Mathf.Abs(escala.z) > minimo;
    }

    private static bool ValorFinito(float valor)
    {
        return !float.IsNaN(valor) && !float.IsInfinity(valor);
    }
}
