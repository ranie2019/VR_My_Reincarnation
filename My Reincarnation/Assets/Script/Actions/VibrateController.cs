using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Vibrate the XR Controller
/// </summary>
public class VibrateController : MonoBehaviour
{
    public float strongVibrate = 0.75f;
    public float weakVibrate = 0.25f;

    [SerializeField] private XRNode maoControle = XRNode.RightHand;
    [SerializeField] private bool detectarMaoAutomaticamente = true;

    private InputDevice dispositivoControle;

    private void Awake()
    {
        if (detectarMaoAutomaticamente)
            DetectarMaoPeloNome();

        AtualizarDispositivoControle();
    }

    public void Vibrate(float amplitude, float duration)
    {
        if (!AtualizarDispositivoControle())
            return;

        dispositivoControle.SendHapticImpulse(0u, amplitude, duration);
    }

    public void VibrateWeak(float duration)
    {
        Vibrate(weakVibrate, duration);
    }

    public void VibrateStrong(float duration)
    {
        Vibrate(strongVibrate, duration);
    }

    private void DetectarMaoPeloNome()
    {
        string nome = gameObject.name.ToLowerInvariant();

        if (nome.Contains("left") || nome.Contains("esquer"))
            maoControle = XRNode.LeftHand;
        else if (nome.Contains("right") || nome.Contains("direit"))
            maoControle = XRNode.RightHand;
    }

    private bool AtualizarDispositivoControle()
    {
        if (dispositivoControle.isValid)
            return true;

        dispositivoControle = InputDevices.GetDeviceAtXRNode(maoControle);
        return dispositivoControle.isValid;
    }
}
