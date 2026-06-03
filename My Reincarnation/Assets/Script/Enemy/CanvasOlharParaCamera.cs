using UnityEngine;

[DisallowMultipleComponent]
public class CanvasOlharParaCamera : MonoBehaviour
{
    [Header("Referencia")]
    [SerializeField] private Camera cameraAlvo;

    [Header("Busca Automatica")]
    [SerializeField] private bool buscarCameraAutomaticamente = true;
    [SerializeField] private bool buscarCameraLocalMultiplayer = true;
    [SerializeField] private string tagCameraLocal = "MainCamera";

    [Header("Configuracao")]
    [SerializeField] private bool olharParaCamera = true;
    [SerializeField] private bool inverterDirecao = false;
    [SerializeField] private bool manterVertical = true;

    private void Awake()
    {
        if (cameraAlvo == null && buscarCameraAutomaticamente)
            BuscarCameraLocal();
    }

    private void OnEnable()
    {
        if (cameraAlvo == null && buscarCameraAutomaticamente)
            BuscarCameraLocal();
    }

    private void LateUpdate()
    {
        if (!olharParaCamera)
            return;

        if (cameraAlvo == null && buscarCameraAutomaticamente)
            BuscarCameraLocal();

        if (cameraAlvo == null)
            return;

        RotacionarParaCamera();
    }

    private void BuscarCameraLocal()
    {
        if (buscarCameraLocalMultiplayer)
        {
            // Futuro multiplayer: atribua aqui, ou pelo Inspector, a camera local do cliente.
            // Este script deve rodar localmente em cada cliente; a rotacao visual do Canvas
            // nao deve ser sincronizada pela rede. Sincronize apenas vida/nivel do inimigo.
        }

        cameraAlvo = Camera.main;
        if (cameraAlvo != null)
            return;

        if (!string.IsNullOrWhiteSpace(tagCameraLocal))
        {
            GameObject cameraPorTag = ProcurarCameraPorTag();
            if (cameraPorTag != null && cameraPorTag.TryGetComponent(out Camera cameraEncontrada))
            {
                cameraAlvo = cameraEncontrada;
                return;
            }
        }

        cameraAlvo = UnityEngine.Object.FindFirstObjectByType<Camera>();
    }

    private GameObject ProcurarCameraPorTag()
    {
        try
        {
            return GameObject.FindGameObjectWithTag(tagCameraLocal);
        }
        catch (UnityException)
        {
            return null;
        }
    }

    private void RotacionarParaCamera()
    {
        Transform cameraTransform = cameraAlvo.transform;

        if (manterVertical)
        {
            Vector3 direcao = transform.position - cameraTransform.position;
            direcao.y = 0f;

            if (inverterDirecao)
                direcao = -direcao;

            if (direcao.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direcao);

            return;
        }

        Vector3 forward = cameraTransform.rotation * Vector3.forward;
        if (inverterDirecao)
            forward = -forward;

        transform.LookAt(
            transform.position + forward,
            cameraTransform.rotation * Vector3.up);
    }
}
