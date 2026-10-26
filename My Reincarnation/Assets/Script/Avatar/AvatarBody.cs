using UnityEngine;

using UnityEngine.Serialization;

public class AvatarBody : MonoBehaviour
{
    [Header("Fontes (XR)")]
    public Transform cameraCabeca;
    public Transform controleEsquerdo;
    public Transform controleDireito;

    [Header("Alvos (Avatar)")]
    public Transform cabecaAvatar;        // Head bone
    public Transform maoEsquerdaAvatar;   // HandL
    public Transform maoDireitaAvatar;    // HandR
    public Transform corpoAvatar;         // Body / Root do corpo

    [Header("Ajustes - Cabeça")]
    [Tooltip("Desloca a cabeça do modelo em relação ao ponto rastreado pelo headset.")]
    [FormerlySerializedAs("offsetOlhos")]
    public Vector3 offsetCabeca = new Vector3(0f, 0.10f, 0f);

    [Header("Ajuste - Câmera Olhos")]
    [Tooltip("Desloca somente a Main Camera. Z positivo avança no eixo local do Camera Offset; não desloca os controles nem o avatar.")]
    public Vector3 offsetCameraOlhos = Vector3.zero;

    [Header("Ajustes - Corpo (opcional)")]
    [Tooltip("Offset do corpo em relação à câmera (normalmente fica negativo no Y).")]
    public Vector3 offsetCorpo = new Vector3(0f, -0.9f, 0f);

    [Tooltip("Suavização (0 = instantâneo).")]
    public float suavizacao = 16f;

    public bool corpoGiraNoY = true;
    public bool suavizarMaos = false;
    public bool moverCorpo = true;

    private Transform ajusteCameraOlhosRoot;
    private Vector3 deslocamentoCameraOlhosMundo;

    void Awake()
    {
        CriarRaizDeAjusteDaCamera();
    }

    void LateUpdate()
    {
        if (!cameraCabeca) return;

        AplicarAjusteCameraOlhos();
        AtualizarCabecaComOffsetCabeca();
        AtualizarMaos();

        if (moverCorpo)
            AtualizarCorpo();
    }

    // =========================
    // ✅ FUNÇÃO PEDIDA (nome: offset visao)
    // Mas aplicada do jeito CERTO: no avatar, não na câmera
    // =========================
    public void OffsetVisao(Vector3 novoOffsetOlhos)
    {
        offsetCabeca = novoOffsetOlhos;
    }

    void CriarRaizDeAjusteDaCamera()
    {
        if (!cameraCabeca || ajusteCameraOlhosRoot)
            return;

        Transform paiOriginal = cameraCabeca.parent;
        if (!paiOriginal)
            return;

        GameObject raiz = new GameObject("__AjusteCameraOlhos");
        ajusteCameraOlhosRoot = raiz.transform;
        ajusteCameraOlhosRoot.SetParent(paiOriginal, false);
        ajusteCameraOlhosRoot.localPosition = Vector3.zero;
        ajusteCameraOlhosRoot.localRotation = Quaternion.identity;
        ajusteCameraOlhosRoot.localScale = Vector3.one;

        // Isola a Main Camera dos controles XR, que continuam no Camera Offset.
        cameraCabeca.SetParent(ajusteCameraOlhosRoot, true);
    }

    void AplicarAjusteCameraOlhos()
    {
        if (!ajusteCameraOlhosRoot)
            CriarRaizDeAjusteDaCamera();

        if (!ajusteCameraOlhosRoot)
        {
            deslocamentoCameraOlhosMundo = Vector3.zero;
            return;
        }

        ajusteCameraOlhosRoot.localPosition = offsetCameraOlhos;
        Transform pai = ajusteCameraOlhosRoot.parent;
        deslocamentoCameraOlhosMundo = pai
            ? pai.TransformVector(offsetCameraOlhos)
            : offsetCameraOlhos;
    }

    Vector3 ObterPosicaoCameraSemAjuste()
    {
        return cameraCabeca.position - deslocamentoCameraOlhosMundo;
    }

    void AtualizarCabecaComOffsetCabeca()
    {
        if (!cabecaAvatar) return;

        // A cabeça do avatar vai para a posição do HMD + offset dos olhos do modelo
        Vector3 alvoPos = ObterPosicaoCameraSemAjuste() + (cameraCabeca.rotation * offsetCabeca);
        Quaternion alvoRot = cameraCabeca.rotation;

        if (suavizacao <= 0f)
        {
            cabecaAvatar.position = alvoPos;
            cabecaAvatar.rotation = alvoRot;
        }
        else
        {
            cabecaAvatar.position = Vector3.Lerp(cabecaAvatar.position, alvoPos, Time.deltaTime * suavizacao);
            cabecaAvatar.rotation = Quaternion.Slerp(cabecaAvatar.rotation, alvoRot, Time.deltaTime * suavizacao);
        }
    }

    void AtualizarMaos()
    {
        if (maoEsquerdaAvatar && controleEsquerdo)
            AplicarPose(maoEsquerdaAvatar, controleEsquerdo, suavizarMaos);

        if (maoDireitaAvatar && controleDireito)
            AplicarPose(maoDireitaAvatar, controleDireito, suavizarMaos);
    }

    void AtualizarCorpo()
    {
        if (!corpoAvatar) return;

        // Corpo segue a câmera com offset (só pra posicionar torso / root)
        Vector3 alvoPos = ObterPosicaoCameraSemAjuste() + offsetCorpo;

        if (suavizacao <= 0f)
            corpoAvatar.position = alvoPos;
        else
            corpoAvatar.position = Vector3.Lerp(corpoAvatar.position, alvoPos, Time.deltaTime * suavizacao);

        if (corpoGiraNoY)
        {
            Vector3 forward = cameraCabeca.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
            {
                Quaternion alvoRot = Quaternion.LookRotation(forward.normalized, Vector3.up);

                if (suavizacao <= 0f)
                    corpoAvatar.rotation = alvoRot;
                else
                    corpoAvatar.rotation = Quaternion.Slerp(corpoAvatar.rotation, alvoRot, Time.deltaTime * suavizacao);
            }
        }
    }

    void AplicarPose(Transform alvo, Transform fonte, bool suavizar)
    {
        if (!suavizar || suavizacao <= 0f)
        {
            alvo.position = fonte.position;
            alvo.rotation = fonte.rotation;
            return;
        }

        alvo.position = Vector3.Lerp(alvo.position, fonte.position, Time.deltaTime * suavizacao);
        alvo.rotation = Quaternion.Slerp(alvo.rotation, fonte.rotation, Time.deltaTime * suavizacao);
    }
}
