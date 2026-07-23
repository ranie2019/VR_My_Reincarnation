using UnityEngine;
using UnityEngine.Serialization;

public class AvatarVRIKController : MonoBehaviour
{
    [Header("Fontes XR")]
    [FormerlySerializedAs("cameraVR")]
    public Transform cameraCabeca;

    [Tooltip("Objeto que deve receber o ajuste da visao. Normalmente e o Camera Offset do XR Origin.")]
    public Transform cameraOffsetRoot;

    [FormerlySerializedAs("maoEsquerdaVR")]
    public Transform controleEsquerdo;

    [FormerlySerializedAs("maoDireitaVR")]
    public Transform controleDireito;

    [Header("Alvos do Avatar")]
    [FormerlySerializedAs("ossoCabeca")]
    public Transform cabecaAvatar;

    [FormerlySerializedAs("maoEsquerdaOsso")]
    public Transform maoEsquerdaAvatar;

    [FormerlySerializedAs("maoDireitaOsso")]
    public Transform maoDireitaAvatar;

    [FormerlySerializedAs("raizAvatar")]
    public Transform corpoAvatar;

    [Header("Alvos dos Bracos")]
    public Transform ombroEsquerdoAvatar;
    public Transform bracoEsquerdoAvatar;
    public Transform antebracoEsquerdoAvatar;
    public Transform ombroDireitoAvatar;
    public Transform bracoDireitoAvatar;
    public Transform antebracoDireitoAvatar;

    [Header("Ajustes da Visao")]
    public bool ajustarPosicaoCamera = true;
    public bool cameraSegueCabecaAvatar = true;
    public Vector3 offsetLocalCamera = Vector3.zero;

    [Header("Ajustes da Cabeca")]
    public bool suavizarCabeca = true;
    public Vector3 offsetRotacaoCabeca = Vector3.zero;

    [Header("Ajustes do Corpo")]
    public Vector3 offsetCorpo = new Vector3(0f, -0.9f, 0f);
    public bool moverCorpo = true;
    public bool corpoGiraNoY = true;

    [Header("Ajustes das Maos")]
    public bool moverMaos = true;
    public bool suavizarMaos;
    public Vector3 offsetPosicaoMaoEsquerda = Vector3.zero;
    public Vector3 offsetRotacaoMaoEsquerda = Vector3.zero;
    public Vector3 offsetPosicaoMaoDireita = Vector3.zero;
    public Vector3 offsetRotacaoMaoDireita = Vector3.zero;

    [Header("Ajustes dos Bracos")]
    public bool moverBracos = true;
    public bool suavizarBracos = true;
    [Range(0f, 1f)] public float pesoOmbro = 0.35f;
    [Range(0f, 1f)] public float pesoBraco = 0.75f;
    [Range(0f, 1f)] public float pesoAntebraco = 1f;
    public bool acompanharOmbros = true;
    public bool travarPosicaoOmbros = true;
    [Range(0f, 45f)] public float limiteRotacaoOmbroGraus = 20f;

    [Header("Suavizacao")]
    [Tooltip("0 = instantaneo.")]
    public float suavizacao = 16f;

    private Vector3 posicaoLocalInicialCameraOffset;
    private bool posicaoLocalInicialCameraOffsetCapturada;
    private Vector3 deslocamentoCameraMundoAtual;
    private Vector3 eixoLocalOmbroEsquerdo = Vector3.forward;
    private Vector3 eixoLocalBracoEsquerdo = Vector3.forward;
    private Vector3 eixoLocalAntebracoEsquerdo = Vector3.forward;
    private Vector3 eixoLocalOmbroDireito = Vector3.forward;
    private Vector3 eixoLocalBracoDireito = Vector3.forward;
    private Vector3 eixoLocalAntebracoDireito = Vector3.forward;
    private bool eixoOmbroEsquerdoValido;
    private bool eixoBracoEsquerdoValido;
    private bool eixoAntebracoEsquerdoValido;
    private bool eixoOmbroDireitoValido;
    private bool eixoBracoDireitoValido;
    private bool eixoAntebracoDireitoValido;
    private bool eixosNaturaisBracosCapturados;
    private Vector3 localOmbroEsquerdoNatural;
    private Vector3 localBracoEsquerdoNatural;
    private Vector3 localAntebracoEsquerdoNatural;
    private Vector3 localMaoEsquerdaNatural;
    private Vector3 localOmbroDireitoNatural;
    private Vector3 localBracoDireitoNatural;
    private Vector3 localAntebracoDireitoNatural;
    private Vector3 localMaoDireitaNatural;
    private Quaternion rotacaoLocalOmbroEsquerdoNatural = Quaternion.identity;
    private Quaternion rotacaoLocalOmbroDireitoNatural = Quaternion.identity;
    private float comprimentoBracoEsquerdoSuperior;
    private float comprimentoBracoEsquerdoInferior;
    private float comprimentoBracoDireitoSuperior;
    private float comprimentoBracoDireitoInferior;
    private bool poseNaturalBracoEsquerdoCapturada;
    private bool poseNaturalBracoDireitoCapturada;

    private void Awake()
    {
        CapturarPosicaoInicialCameraOffset();
        ConfigurarBracosAutomaticamente();
        CapturarPoseNaturalBracos();
        CapturarEixosNaturaisBracos();
    }

    private void LateUpdate()
    {
        if (cameraCabeca == null)
            return;

        AtualizarDeslocamentoCameraAtual();

        if (moverCorpo)
            AtualizarCorpo();

        AtualizarCabecaComOffsetOlhos();
        AtualizarOffsetCamera();

        if (moverBracos)
        {
            AtualizarBracos();

            if (moverMaos)
                AtualizarRotacaoMaos();
        }
        else if (moverMaos)
        {
            AtualizarMaos();
        }
    }

    public void OffsetVisao(Vector3 novoOffsetCamera)
    {
        offsetLocalCamera = novoOffsetCamera;
    }

    [ContextMenu("Capturar Posicao Atual da Camera Como Base")]
    public void CapturarPosicaoInicialCameraOffset()
    {
        Transform alvoOffset = ObterAlvoOffsetCamera();
        if (alvoOffset == null)
            return;

        posicaoLocalInicialCameraOffset = alvoOffset.localPosition;
        posicaoLocalInicialCameraOffsetCapturada = true;
    }

    private void AtualizarOffsetCamera()
    {
        if (!ajustarPosicaoCamera)
            return;

        Transform alvoOffset = ObterAlvoOffsetCamera();
        if (alvoOffset == null)
            return;

        if (!posicaoLocalInicialCameraOffsetCapturada)
            CapturarPosicaoInicialCameraOffset();

        if (cameraSegueCabecaAvatar && cabecaAvatar != null)
        {
            Vector3 posicaoCameraDesejada = cabecaAvatar.TransformPoint(offsetLocalCamera);
            alvoOffset.position += posicaoCameraDesejada - cameraCabeca.position;
        }
        else
        {
            alvoOffset.localPosition = posicaoLocalInicialCameraOffset + offsetLocalCamera;
        }

        AtualizarDeslocamentoCameraAtual();
    }

    private void AtualizarDeslocamentoCameraAtual()
    {
        deslocamentoCameraMundoAtual = Vector3.zero;

        Transform alvoOffset = ObterAlvoOffsetCamera();
        if (alvoOffset == null)
            return;

        if (!posicaoLocalInicialCameraOffsetCapturada)
            CapturarPosicaoInicialCameraOffset();

        Vector3 offsetLocalAplicado = alvoOffset.localPosition - posicaoLocalInicialCameraOffset;

        deslocamentoCameraMundoAtual = alvoOffset.parent != null
            ? alvoOffset.parent.TransformVector(offsetLocalAplicado)
            : offsetLocalAplicado;
    }

    private Transform ObterAlvoOffsetCamera()
    {
        if (cameraOffsetRoot != null)
            return cameraOffsetRoot;

        return cameraCabeca != null ? cameraCabeca.parent : null;
    }

    private void AtualizarCabecaComOffsetOlhos()
    {
        if (cabecaAvatar == null)
            return;

        Quaternion alvoRot = cameraCabeca.rotation * Quaternion.Euler(offsetRotacaoCabeca);
        AplicarRotacao(cabecaAvatar, alvoRot, suavizarCabeca);
    }

    private void AtualizarMaos()
    {
        if (maoEsquerdaAvatar != null && controleEsquerdo != null)
        {
            Vector3 alvoPos = ObterPosicaoComOffset(controleEsquerdo, offsetPosicaoMaoEsquerda);
            Quaternion alvoRot = controleEsquerdo.rotation * Quaternion.Euler(offsetRotacaoMaoEsquerda);
            AplicarPose(maoEsquerdaAvatar, alvoPos, alvoRot, suavizarMaos);
        }

        if (maoDireitaAvatar != null && controleDireito != null)
        {
            Vector3 alvoPos = ObterPosicaoComOffset(controleDireito, offsetPosicaoMaoDireita);
            Quaternion alvoRot = controleDireito.rotation * Quaternion.Euler(offsetRotacaoMaoDireita);
            AplicarPose(maoDireitaAvatar, alvoPos, alvoRot, suavizarMaos);
        }
    }

    private void AtualizarRotacaoMaos()
    {
        if (maoEsquerdaAvatar != null && controleEsquerdo != null)
        {
            Quaternion alvoRot = controleEsquerdo.rotation * Quaternion.Euler(offsetRotacaoMaoEsquerda);
            AplicarRotacao(maoEsquerdaAvatar, alvoRot, suavizarMaos);
        }

        if (maoDireitaAvatar != null && controleDireito != null)
        {
            Quaternion alvoRot = controleDireito.rotation * Quaternion.Euler(offsetRotacaoMaoDireita);
            AplicarRotacao(maoDireitaAvatar, alvoRot, suavizarMaos);
        }
    }

    private void AtualizarBracos()
    {
        if (!eixosNaturaisBracosCapturados)
            CapturarEixosNaturaisBracos();

        if (!poseNaturalBracoEsquerdoCapturada || !poseNaturalBracoDireitoCapturada)
            CapturarPoseNaturalBracos();

        bool esquerdoAtualizado = AtualizarBracoIKLado(
            ombroEsquerdoAvatar,
            bracoEsquerdoAvatar,
            antebracoEsquerdoAvatar,
            maoEsquerdaAvatar,
            controleEsquerdo,
            offsetPosicaoMaoEsquerda,
            localOmbroEsquerdoNatural,
            localBracoEsquerdoNatural,
            localAntebracoEsquerdoNatural,
            localMaoEsquerdaNatural,
            rotacaoLocalOmbroEsquerdoNatural,
            comprimentoBracoEsquerdoSuperior,
            comprimentoBracoEsquerdoInferior,
            poseNaturalBracoEsquerdoCapturada,
            eixoLocalOmbroEsquerdo,
            eixoLocalBracoEsquerdo,
            eixoLocalAntebracoEsquerdo,
            eixoOmbroEsquerdoValido,
            eixoBracoEsquerdoValido,
            eixoAntebracoEsquerdoValido
        );

        bool direitoAtualizado = AtualizarBracoIKLado(
            ombroDireitoAvatar,
            bracoDireitoAvatar,
            antebracoDireitoAvatar,
            maoDireitaAvatar,
            controleDireito,
            offsetPosicaoMaoDireita,
            localOmbroDireitoNatural,
            localBracoDireitoNatural,
            localAntebracoDireitoNatural,
            localMaoDireitaNatural,
            rotacaoLocalOmbroDireitoNatural,
            comprimentoBracoDireitoSuperior,
            comprimentoBracoDireitoInferior,
            poseNaturalBracoDireitoCapturada,
            eixoLocalOmbroDireito,
            eixoLocalBracoDireito,
            eixoLocalAntebracoDireito,
            eixoOmbroDireitoValido,
            eixoBracoDireitoValido,
            eixoAntebracoDireitoValido
        );

        if (moverMaos)
        {
            if (!esquerdoAtualizado && maoEsquerdaAvatar != null && controleEsquerdo != null)
            {
                Vector3 alvoPos = ObterPosicaoComOffset(controleEsquerdo, offsetPosicaoMaoEsquerda);
                Quaternion alvoRot = controleEsquerdo.rotation * Quaternion.Euler(offsetRotacaoMaoEsquerda);
                AplicarPose(maoEsquerdaAvatar, alvoPos, alvoRot, suavizarMaos);
            }

            if (!direitoAtualizado && maoDireitaAvatar != null && controleDireito != null)
            {
                Vector3 alvoPos = ObterPosicaoComOffset(controleDireito, offsetPosicaoMaoDireita);
                Quaternion alvoRot = controleDireito.rotation * Quaternion.Euler(offsetRotacaoMaoDireita);
                AplicarPose(maoDireitaAvatar, alvoPos, alvoRot, suavizarMaos);
            }
        }
    }

    private bool AtualizarBracoIKLado(
        Transform ombro,
        Transform braco,
        Transform antebraco,
        Transform mao,
        Transform controle,
        Vector3 offsetPosicaoMao,
        Vector3 localOmbroNatural,
        Vector3 localBracoNatural,
        Vector3 localAntebracoNatural,
        Vector3 localMaoNatural,
        Quaternion rotacaoLocalOmbroNatural,
        float comprimentoSuperior,
        float comprimentoInferior,
        bool poseNaturalCapturada,
        Vector3 eixoOmbro,
        Vector3 eixoBraco,
        Vector3 eixoAntebraco,
        bool eixoOmbroValido,
        bool eixoBracoValido,
        bool eixoAntebracoValido
    )
    {
        if (braco == null || antebraco == null || mao == null || controle == null || !poseNaturalCapturada)
            return false;

        RestaurarPoseNaturalBraco(braco, antebraco, mao, localBracoNatural, localAntebracoNatural, localMaoNatural);

        Vector3 posicaoAlvoMao = ObterPosicaoComOffset(controle, offsetPosicaoMao);

        AplicarAcompanhamentoOmbroLimitado(ombro, posicaoAlvoMao, eixoOmbro, eixoOmbroValido, localOmbroNatural, rotacaoLocalOmbroNatural);

        Vector3 raizBraco = braco.position;
        Vector3 direcaoAlvo = posicaoAlvoMao - raizBraco;
        float distanciaAlvo = direcaoAlvo.magnitude;
        float alcanceTotal = comprimentoSuperior + comprimentoInferior;

        if (distanciaAlvo < 0.0001f || alcanceTotal < 0.0001f)
            return false;

        Vector3 direcaoMao = direcaoAlvo / distanciaAlvo;
        float distanciaResolvida = Mathf.Clamp(distanciaAlvo, 0.0001f, alcanceTotal * 0.999f);
        Vector3 direcaoDobra = CalcularDirecaoDobraCotovelo(raizBraco, posicaoAlvoMao, antebraco.position);

        float x = (comprimentoSuperior * comprimentoSuperior - comprimentoInferior * comprimentoInferior + distanciaResolvida * distanciaResolvida) / (2f * distanciaResolvida);
        float yQuadrado = Mathf.Max(0f, comprimentoSuperior * comprimentoSuperior - x * x);
        float y = Mathf.Sqrt(yQuadrado);
        Vector3 posicaoCotoveloAlvo = raizBraco + direcaoMao * x + direcaoDobra * y;

        AplicarDirecionamentoOsso(braco, posicaoCotoveloAlvo, eixoBraco, eixoBracoValido, pesoBraco);
        AplicarDirecionamentoOsso(antebraco, posicaoAlvoMao, eixoAntebraco, eixoAntebracoValido, pesoAntebraco);

        return true;
    }

    private void AplicarAcompanhamentoOmbroLimitado(
        Transform ombro,
        Vector3 posicaoAlvoMao,
        Vector3 eixoLocalNatural,
        bool eixoValido,
        Vector3 localOmbroNatural,
        Quaternion rotacaoLocalOmbroNatural
    )
    {
        if (ombro == null)
            return;

        if (travarPosicaoOmbros)
            ombro.localPosition = localOmbroNatural;

        if (!acompanharOmbros || !eixoValido || pesoOmbro <= 0f || limiteRotacaoOmbroGraus <= 0f)
        {
            AplicarRotacaoLocal(ombro, rotacaoLocalOmbroNatural, suavizarBracos);
            return;
        }

        Vector3 direcaoAlvo = posicaoAlvoMao - ombro.position;
        if (direcaoAlvo.sqrMagnitude < 0.000001f)
            return;

        Quaternion rotacaoMundoNatural = ombro.parent != null
            ? ombro.parent.rotation * rotacaoLocalOmbroNatural
            : rotacaoLocalOmbroNatural;

        Vector3 eixoNaturalMundo = rotacaoMundoNatural * eixoLocalNatural;
        if (eixoNaturalMundo.sqrMagnitude < 0.000001f)
            return;

        Quaternion delta = Quaternion.FromToRotation(eixoNaturalMundo.normalized, direcaoAlvo.normalized);
        Quaternion rotacaoMundoDesejada = delta * rotacaoMundoNatural;
        Quaternion rotacaoMundoLimitada = Quaternion.RotateTowards(rotacaoMundoNatural, rotacaoMundoDesejada, limiteRotacaoOmbroGraus);
        Quaternion rotacaoMundoComPeso = Quaternion.Slerp(rotacaoMundoNatural, rotacaoMundoLimitada, Mathf.Clamp01(pesoOmbro));
        Quaternion rotacaoLocalAlvo = ombro.parent != null
            ? Quaternion.Inverse(ombro.parent.rotation) * rotacaoMundoComPeso
            : rotacaoMundoComPeso;

        AplicarRotacaoLocal(ombro, rotacaoLocalAlvo, suavizarBracos);
    }

    private void AplicarDirecionamentoOsso(Transform osso, Vector3 posicaoAlvo, Vector3 eixoLocalNatural, bool eixoValido, float peso)
    {
        if (osso == null || !eixoValido || peso <= 0f)
            return;

        Vector3 direcaoAlvo = posicaoAlvo - osso.position;
        if (direcaoAlvo.sqrMagnitude < 0.000001f)
            return;

        Vector3 eixoAtual = osso.TransformDirection(eixoLocalNatural);
        if (eixoAtual.sqrMagnitude < 0.000001f)
            return;

        Quaternion delta = Quaternion.FromToRotation(eixoAtual.normalized, direcaoAlvo.normalized);
        Quaternion rotacaoAlvo = delta * osso.rotation;
        Quaternion rotacaoComPeso = Quaternion.Slerp(osso.rotation, rotacaoAlvo, Mathf.Clamp01(peso));

        AplicarRotacao(osso, rotacaoComPeso, suavizarBracos);
    }

    private void AtualizarCorpo()
    {
        if (corpoAvatar == null)
            return;

        Vector3 posicaoCameraSemOffsetVisual = cameraCabeca.position - deslocamentoCameraMundoAtual;
        Vector3 alvoPos = posicaoCameraSemOffsetVisual + offsetCorpo;
        Quaternion alvoRot = corpoAvatar.rotation;

        if (corpoGiraNoY)
        {
            Vector3 forward = cameraCabeca.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
                alvoRot = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        AplicarPose(corpoAvatar, alvoPos, alvoRot, true);
    }

    private Vector3 ObterPosicaoComOffset(Transform fonte, Vector3 offsetLocal)
    {
        return offsetLocal.sqrMagnitude > 0.0000001f
            ? fonte.TransformPoint(offsetLocal)
            : fonte.position;
    }

    [ContextMenu("Recapturar Pose Natural dos Bracos")]
    public void CapturarPoseNaturalBracos()
    {
        poseNaturalBracoEsquerdoCapturada = CapturarPoseNaturalBracoLado(
            ombroEsquerdoAvatar,
            bracoEsquerdoAvatar,
            antebracoEsquerdoAvatar,
            maoEsquerdaAvatar,
            out localOmbroEsquerdoNatural,
            out localBracoEsquerdoNatural,
            out localAntebracoEsquerdoNatural,
            out localMaoEsquerdaNatural,
            out rotacaoLocalOmbroEsquerdoNatural,
            out comprimentoBracoEsquerdoSuperior,
            out comprimentoBracoEsquerdoInferior
        );

        poseNaturalBracoDireitoCapturada = CapturarPoseNaturalBracoLado(
            ombroDireitoAvatar,
            bracoDireitoAvatar,
            antebracoDireitoAvatar,
            maoDireitaAvatar,
            out localOmbroDireitoNatural,
            out localBracoDireitoNatural,
            out localAntebracoDireitoNatural,
            out localMaoDireitaNatural,
            out rotacaoLocalOmbroDireitoNatural,
            out comprimentoBracoDireitoSuperior,
            out comprimentoBracoDireitoInferior
        );
    }

    private bool CapturarPoseNaturalBracoLado(
        Transform ombro,
        Transform braco,
        Transform antebraco,
        Transform mao,
        out Vector3 localOmbroNatural,
        out Vector3 localBracoNatural,
        out Vector3 localAntebracoNatural,
        out Vector3 localMaoNatural,
        out Quaternion rotacaoLocalOmbroNatural,
        out float comprimentoSuperior,
        out float comprimentoInferior
    )
    {
        localOmbroNatural = ombro != null ? ombro.localPosition : Vector3.zero;
        localBracoNatural = braco != null ? braco.localPosition : Vector3.zero;
        localAntebracoNatural = antebraco != null ? antebraco.localPosition : Vector3.zero;
        localMaoNatural = mao != null ? mao.localPosition : Vector3.zero;
        rotacaoLocalOmbroNatural = ombro != null ? ombro.localRotation : Quaternion.identity;
        comprimentoSuperior = 0f;
        comprimentoInferior = 0f;

        if (braco == null || antebraco == null || mao == null)
            return false;

        comprimentoSuperior = Vector3.Distance(braco.position, antebraco.position);
        comprimentoInferior = Vector3.Distance(antebraco.position, mao.position);

        return comprimentoSuperior > 0.0001f && comprimentoInferior > 0.0001f;
    }

    private void RestaurarPoseNaturalBraco(
        Transform braco,
        Transform antebraco,
        Transform mao,
        Vector3 localBracoNatural,
        Vector3 localAntebracoNatural,
        Vector3 localMaoNatural
    )
    {
        braco.localPosition = localBracoNatural;
        antebraco.localPosition = localAntebracoNatural;
        mao.localPosition = localMaoNatural;
    }

    private Vector3 CalcularDirecaoDobraCotovelo(Vector3 raizBraco, Vector3 posicaoAlvoMao, Vector3 posicaoAntebraco)
    {
        Vector3 direcaoMao = posicaoAlvoMao - raizBraco;
        if (direcaoMao.sqrMagnitude < 0.000001f)
            return Vector3.down;

        Vector3 direcaoNormalizada = direcaoMao.normalized;
        Vector3 direcaoDobra = Vector3.ProjectOnPlane(posicaoAntebraco - raizBraco, direcaoNormalizada);

        if (direcaoDobra.sqrMagnitude < 0.000001f)
        {
            Vector3 referenciaBaixo = corpoAvatar != null ? -corpoAvatar.up : Vector3.down;
            direcaoDobra = Vector3.ProjectOnPlane(referenciaBaixo, direcaoNormalizada);
        }

        if (direcaoDobra.sqrMagnitude < 0.000001f)
            direcaoDobra = Vector3.ProjectOnPlane(Vector3.forward, direcaoNormalizada);

        return direcaoDobra.sqrMagnitude > 0.000001f ? direcaoDobra.normalized : Vector3.down;
    }

    [ContextMenu("Recapturar Eixos Naturais dos Bracos")]
    public void CapturarEixosNaturaisBracos()
    {
        eixoOmbroEsquerdoValido = CapturarEixoLocalOsso(ombroEsquerdoAvatar, bracoEsquerdoAvatar, out eixoLocalOmbroEsquerdo);
        eixoBracoEsquerdoValido = CapturarEixoLocalOsso(bracoEsquerdoAvatar, antebracoEsquerdoAvatar, out eixoLocalBracoEsquerdo);
        eixoAntebracoEsquerdoValido = CapturarEixoLocalOsso(antebracoEsquerdoAvatar, maoEsquerdaAvatar, out eixoLocalAntebracoEsquerdo);

        eixoOmbroDireitoValido = CapturarEixoLocalOsso(ombroDireitoAvatar, bracoDireitoAvatar, out eixoLocalOmbroDireito);
        eixoBracoDireitoValido = CapturarEixoLocalOsso(bracoDireitoAvatar, antebracoDireitoAvatar, out eixoLocalBracoDireito);
        eixoAntebracoDireitoValido = CapturarEixoLocalOsso(antebracoDireitoAvatar, maoDireitaAvatar, out eixoLocalAntebracoDireito);

        eixosNaturaisBracosCapturados = true;
    }

    private bool CapturarEixoLocalOsso(Transform osso, Transform referencia, out Vector3 eixoLocal)
    {
        eixoLocal = Vector3.forward;

        if (osso == null || referencia == null)
            return false;

        Vector3 direcaoMundo = referencia.position - osso.position;
        if (direcaoMundo.sqrMagnitude < 0.000001f)
            return false;

        eixoLocal = osso.InverseTransformDirection(direcaoMundo.normalized);
        return eixoLocal.sqrMagnitude > 0.000001f;
    }

    private void ConfigurarBracosAutomaticamente()
    {
        Transform raiz = corpoAvatar != null ? corpoAvatar : transform;

        if (ombroEsquerdoAvatar == null)
            ombroEsquerdoAvatar = ProcurarFilhoPorNome(raiz, "Ombro Esquerdo", "ombro esquerdo", "ombro_l", "ombrol", "leftshoulder", "leftclavicle");

        if (bracoEsquerdoAvatar == null)
            bracoEsquerdoAvatar = ProcurarFilhoPorNome(raiz, "Braco Esquerdo", "braco esquerdo", "LeftArm", "UpperArm_L");

        if (antebracoEsquerdoAvatar == null)
            antebracoEsquerdoAvatar = ProcurarFilhoPorNome(raiz, "Antebraco Esquerdo", "Ante braco Esquerdo", "antebraco esquerdo", "LeftForeArm", "ForeArm_L");

        if (ombroDireitoAvatar == null)
            ombroDireitoAvatar = ProcurarFilhoPorNome(raiz, "Ombro Direito", "ombro direito", "ombro_r", "ombror", "rightshoulder", "rightclavicle");

        if (bracoDireitoAvatar == null)
            bracoDireitoAvatar = ProcurarFilhoPorNome(raiz, "Braco Direito", "braco direito", "RightArm", "UpperArm_R");

        if (antebracoDireitoAvatar == null)
            antebracoDireitoAvatar = ProcurarFilhoPorNome(raiz, "Antebraco Direito", "Ante braco Direito", "antebraco direito", "RightForeArm", "ForeArm_R");
    }

    private Transform ProcurarFilhoPorNome(Transform raiz, params string[] nomes)
    {
        if (raiz == null || nomes == null)
            return null;

        foreach (Transform filho in raiz.GetComponentsInChildren<Transform>(true))
        {
            foreach (string nome in nomes)
            {
                if (string.Equals(filho.name, nome, System.StringComparison.OrdinalIgnoreCase))
                    return filho;
            }
        }

        return null;
    }

    private void AplicarPose(Transform alvo, Vector3 posicao, Quaternion rotacao, bool suavizar)
    {
        if (!suavizar || suavizacao <= 0f)
        {
            alvo.SetPositionAndRotation(posicao, rotacao);
            return;
        }

        float fator = Time.deltaTime * suavizacao;
        alvo.position = Vector3.Lerp(alvo.position, posicao, fator);
        alvo.rotation = Quaternion.Slerp(alvo.rotation, rotacao, fator);
    }

    private void AplicarRotacao(Transform alvo, Quaternion rotacao, bool suavizar)
    {
        if (!suavizar || suavizacao <= 0f)
        {
            alvo.rotation = rotacao;
            return;
        }

        alvo.rotation = Quaternion.Slerp(alvo.rotation, rotacao, Time.deltaTime * suavizacao);
    }

    private void AplicarRotacaoLocal(Transform alvo, Quaternion rotacaoLocal, bool suavizar)
    {
        if (!suavizar || suavizacao <= 0f)
        {
            alvo.localRotation = rotacaoLocal;
            return;
        }

        alvo.localRotation = Quaternion.Slerp(alvo.localRotation, rotacaoLocal, Time.deltaTime * suavizacao);
    }
}
