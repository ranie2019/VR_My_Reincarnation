using System;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class AvatarVRIKController : MonoBehaviour
{
    [Header("Referências VR")]
    [Tooltip("Transform da câmera/HMD do jogador. Normalmente é o objeto Main Camera.")]
    [SerializeField] private Transform cameraVR;

    [Tooltip("Transform do controle esquerdo VR.")]
    [SerializeField] private Transform maoEsquerdaVR;

    [Tooltip("Transform do controle direito VR.")]
    [SerializeField] private Transform maoDireitaVR;

    [Header("Altura Virtual VR")]
    [SerializeField] private Transform cameraOffsetRoot;
    [SerializeField] private bool usarAlturaVirtual = true;
    [SerializeField] private float alturaVirtualOlhos = 1.65f;
    [FormerlySerializedAs("offsetAlturaManual")]
    [SerializeField] private float ajusteAlturaExtra = 0f;
    [SerializeField] private Vector3 offsetPosicaoCameraVirtual = Vector3.zero;
    [SerializeField] private bool modoSentado = true;
    [SerializeField] private float alturaRealSentado = 1.15f;

    [Header("Avatar")]
    [Tooltip("Animator do avatar visível. A auto-configuração prioriza o filho chamado 'avatar masculino'.")]
    [SerializeField] private Animator animatorAvatar;

    [Tooltip("Raiz visual do avatar. Normalmente é o Transform do Animator.")]
    [FormerlySerializedAs("raizDoAvatar")]
    [SerializeField] private Transform raizAvatar;

    [Header("Posi\u00e7\u00e3o do Avatar")]
    [SerializeField] private bool manterAvatarNaOrigemDoPlayer = true;

    [FormerlySerializedAs("offsetLocalAvatar")]
    [FormerlySerializedAs("mainCameraAvatar")]
    [SerializeField, HideInInspector] private Vector3 mainCameraAvatarLegado = Vector3.zero;

    [Header("Seguir Movimento F\u00edsico VR")]
    [SerializeField] private bool avatarSegueMovimentoFisicoDaCabeca = true;
    [SerializeField] private float suavidadeSeguirCabecaXZ = 20f;
    [SerializeField] private Vector3 offsetAvatarEmRelacaoCabeca = new Vector3(0f, 0f, 0f);

    [Header("Ossos do Avatar")]
    [FormerlySerializedAs("headBone")]
    [Tooltip("Osso da cabeça do avatar.")]
    [SerializeField] private Transform ossoCabeca;

    [FormerlySerializedAs("neckBone")]
    [Tooltip("Osso do pescoço, se existir.")]
    [SerializeField] private Transform ossoPescoco;

    [FormerlySerializedAs("chestBone")]
    [Tooltip("Osso do peito/chest/upper chest, se existir.")]
    [SerializeField] private Transform ossoPeito;

    [FormerlySerializedAs("spineBone")]
    [Tooltip("Osso da coluna/spine, se existir.")]
    [SerializeField] private Transform ossoColuna;

    [FormerlySerializedAs("leftUpperArm")]
    [Tooltip("Osso superior do braço esquerdo.")]
    [SerializeField] private Transform bracoEsquerdoSuperior;

    [FormerlySerializedAs("leftLowerArm")]
    [Tooltip("Osso inferior/antebraço esquerdo.")]
    [SerializeField] private Transform bracoEsquerdoInferior;

    [FormerlySerializedAs("leftHand")]
    [Tooltip("Osso da mão esquerda.")]
    [SerializeField] private Transform maoEsquerdaOsso;

    [FormerlySerializedAs("rightUpperArm")]
    [Tooltip("Osso superior do braço direito.")]
    [SerializeField] private Transform bracoDireitoSuperior;

    [FormerlySerializedAs("rightLowerArm")]
    [Tooltip("Osso inferior/antebraço direito.")]
    [SerializeField] private Transform bracoDireitoInferior;

    [FormerlySerializedAs("rightHand")]
    [Tooltip("Osso da mão direita.")]
    [SerializeField] private Transform maoDireitaOsso;

    [FormerlySerializedAs("leftClavicle")]
    [Tooltip("Clavícula/shoulder esquerda, se existir.")]
    [SerializeField] private Transform claviculaEsquerda;

    [FormerlySerializedAs("rightClavicle")]
    [Tooltip("Clavícula/shoulder direita, se existir.")]
    [SerializeField] private Transform claviculaDireita;

    [Header("Alvos IK")]
    [FormerlySerializedAs("leftHandTarget")]
    [Tooltip("Alvo IK da mão esquerda. Deve ser filho do Player, não do osso.")]
    [SerializeField] private Transform alvoMaoEsquerda;

    [FormerlySerializedAs("rightHandTarget")]
    [Tooltip("Alvo IK da mão direita. Deve ser filho do Player, não do osso.")]
    [SerializeField] private Transform alvoMaoDireita;

    [FormerlySerializedAs("leftElbowHint")]
    [Tooltip("Hint/alvo do cotovelo esquerdo. Deve ficar para fora e levemente atrás do braço.")]
    [SerializeField] private Transform alvoCotoveloEsquerdo;

    [FormerlySerializedAs("rightElbowHint")]
    [Tooltip("Hint/alvo do cotovelo direito. Deve ficar para fora e levemente atrás do braço.")]
    [SerializeField] private Transform alvoCotoveloDireito;

    [Header("Suavização")]
    [Tooltip("Suavização da posição dos alvos das mãos.")]
    [SerializeField] private float suavidadeMao = 18f;

    [Tooltip("Suavização da rotação dos alvos das mãos.")]
    [SerializeField] private float suavidadeRotacaoMao = 18f;

    [Tooltip("Suavização opcional da cabeça, caso controlarCabeca esteja ativo.")]
    [SerializeField] private float suavidadeCabeca = 18f;

    [Tooltip("Suavização da rotação do peito, coluna e pescoço.")]
    [SerializeField] private float suavidadeCorpo = 6f;

    [Header("Offsets da Mão Esquerda")]
    [Tooltip("Offset local de posição aplicado a partir da rotação do controle esquerdo.")]
    [SerializeField] private Vector3 offsetPosicaoMaoEsquerda = Vector3.zero;

    [Tooltip("Offset local de rotação aplicado ao controle esquerdo.")]
    [SerializeField] private Vector3 offsetRotacaoMaoEsquerdaEuler = Vector3.zero;

    [Header("Offsets da Mão Direita")]
    [Tooltip("Offset local de posição aplicado a partir da rotação do controle direito.")]
    [SerializeField] private Vector3 offsetPosicaoMaoDireita = Vector3.zero;

    [Tooltip("Offset local de rotação aplicado ao controle direito.")]
    [SerializeField] private Vector3 offsetRotacaoMaoDireitaEuler = Vector3.zero;

    [Header("Cotovelos")]
    [FormerlySerializedAs("distanciaHintCotovelo")]
    [Tooltip("Distância do hint do cotovelo para trás do corpo.")]
    [SerializeField] private float distanciaAlvoCotovelo = 0.35f;

    [FormerlySerializedAs("alturaHintCotovelo")]
    [Tooltip("Ajuste vertical dos hints dos cotovelos.")]
    [SerializeField] private float alturaAlvoCotovelo = -0.1f;

    [Tooltip("Afastamento lateral dos cotovelos para fora do corpo.")]
    [SerializeField] private float afastamentoLateralCotovelo = 0.25f;

    [Header("Corpo")]
    [Tooltip("Permite que peito, coluna e pescoço acompanhem suavemente o yaw da câmera.")]
    [SerializeField] private bool rotacionarCorpoComCabeca = true;

    [Header("Rota\u00e7\u00e3o do Avatar Inteiro")]
    [SerializeField] private bool rotacionarAvatarInteiroComCabeca = true;
    [SerializeField] private bool inverterRotacaoYaw = false;
    [SerializeField] private float suavidadeRotacaoRaiz = 10f;
    [SerializeField] private float offsetRotacaoYawAvatar = 0f;

    [FormerlySerializedAs("pesoRotacaoChest")]
    [Tooltip("Quanto o peito acompanha a rotação horizontal da câmera.")]
    [SerializeField, Range(0f, 1f)] private float pesoRotacaoPeito = 0.25f;

    [FormerlySerializedAs("pesoRotacaoSpine")]
    [Tooltip("Quanto a coluna acompanha a rotação horizontal da câmera.")]
    [SerializeField, Range(0f, 1f)] private float pesoRotacaoColuna = 0.12f;

    [FormerlySerializedAs("pesoRotacaoNeck")]
    [Tooltip("Quanto o pescoço acompanha a rotação horizontal da câmera.")]
    [SerializeField, Range(0f, 1f)] private float pesoRotacaoPescoco = 0.35f;

    [Header("Controle")]
    [Tooltip("Liga/desliga o controle IK/targets deste componente.")]
    [SerializeField] private bool usarIK = true;

    [FormerlySerializedAs("controlarCabecaComEsteScript")]
    [Tooltip("Por padrão fica desligado porque a cabeça já acompanha a câmera por outro sistema.")]
    [SerializeField] private bool controlarCabeca = false;

    [Tooltip("Permite rotacionar peito, coluna e pescoço suavemente com o yaw da câmera.")]
    [SerializeField] private bool controlarCorpo = true;

    [Header("Cabeça Opcional")]
    [FormerlySerializedAs("offsetPosicaoCabeca")]
    [Tooltip("Offset local de posição da cabeça, usado somente se controlarCabeca estiver ativo.")]
    [SerializeField] private Vector3 offsetPosicaoCabeca = Vector3.zero;

    [FormerlySerializedAs("offsetRotacaoCabecaEuler")]
    [Tooltip("Offset local de rotação da cabeça, usado somente se controlarCabeca estiver ativo.")]
    [SerializeField] private Vector3 offsetRotacaoCabecaEuler = Vector3.zero;

    [Header("Fallback sem Animation Rigging")]
    [Tooltip("Usa Animator IK se o avatar for Humanoid e a layer do Animator estiver com IK Pass ativo.")]
    [SerializeField] private bool usarAnimatorIKHumanoidComoFallback = true;

    [Header("Status")]
    [Tooltip("Resumo da configuração atual. Este campo é apenas informativo.")]
    [SerializeField, TextArea(10, 25)] private string statusConfiguracao;

    private const string NomeAvatarPreferido = "avatar masculino";
    private const string NomeLeftHandTargetAntigo = "LeftHandIKTarget";
    private const string NomeRightHandTargetAntigo = "RightHandIKTarget";
    private const string NomeAlvoMaoEsquerda = "Alvo_Mao_Esquerda_IK";
    private const string NomeAlvoMaoDireita = "Alvo_Mao_Direita_IK";
    private const string NomeAlvoCotoveloEsquerdo = "Alvo_Cotovelo_Esquerdo_IK";
    private const string NomeAlvoCotoveloDireito = "Alvo_Cotovelo_Direito_IK";
    private const string NomeRigArms = "AvatarVR_ArmsRig";

    private bool animationRiggingDisponivel;
    private bool animatorHumanoidDisponivel;
    private Quaternion baseYawAvatar = Quaternion.identity;
    private Quaternion baseRotacaoPescoco = Quaternion.identity;
    private Quaternion baseRotacaoPeito = Quaternion.identity;
    private Quaternion baseRotacaoColuna = Quaternion.identity;
    private bool rotacoesBaseCapturadas;

    private void Reset()
    {
        ConfigurarAvatarAutomaticamenteInterno(true);
        AtualizarAlturaVirtualCamera();
        CapturarRotacoesBase();
        AtualizarStatusConfiguracao();
    }

    private void Awake()
    {
        DesativarAvatarBodyNoMesmoGameObject();
        ConfigurarAvatarAutomaticamenteInterno(false);
        AtualizarAlturaVirtualCamera();
        CapturarRotacoesBase();
        AtualizarStatusConfiguracao();
    }

    private void OnValidate()
    {
        NormalizarValores();
        AtualizarStatusConfiguracao();
    }

    [ContextMenu("Configurar Avatar VR Automaticamente")]
    public void ConfigurarAvatarAutomaticamente()
    {
        ConfigurarAvatarAutomaticamenteInterno(true);
        AtualizarAlturaVirtualCamera();
        CapturarRotacoesBase();
        AtualizarStatusConfiguracao();
    }

    [ContextMenu("Verificar Configura\u00e7\u00e3o do Avatar")]
    public void VerificarConfiguracaoDoAvatar()
    {
        ConfigurarAvatarAutomaticamenteInterno(false);
        CapturarRotacoesBase();
        AtualizarStatusConfiguracao();
    }

    private void ConfigurarAvatarAutomaticamenteInterno(bool sobrescreverCamposPreenchidos)
    {
        NormalizarValores();
        ConfigurarReferenciasVR(sobrescreverCamposPreenchidos);
        ConfigurarAnimatorAvatar(sobrescreverCamposPreenchidos);
        ConfigurarOssosAvatar(sobrescreverCamposPreenchidos);
        ConfigurarAlvosIK(sobrescreverCamposPreenchidos);
    }

    private void LateUpdate()
    {
        AtualizarAlturaVirtualCamera();
        AtualizarPosicaoAvatarPelaCabeca();

        if (!avatarSegueMovimentoFisicoDaCabeca)
            ManterAvatarAlinhadoAoPlayer();

        AtualizarRotacaoDoAvatarInteiro();

        if (!usarIK)
            return;

        AtualizarAlvosDasMaos();
        AtualizarAlvosDosCotovelos();

        if (controlarCabeca)
            AtualizarCabecaOpcional();

        if (controlarCorpo && !rotacionarAvatarInteiroComCabeca)
            AtualizarRotacaoSuaveDoCorpo();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!usarIK || !usarAnimatorIKHumanoidComoFallback || !AnimatorHumanoidValido())
            return;

        AtualizarAlvosDasMaos();
        AtualizarAlvosDosCotovelos();

        AplicarAnimatorIK(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, alvoMaoEsquerda, alvoCotoveloEsquerdo);
        AplicarAnimatorIK(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, alvoMaoDireita, alvoCotoveloDireito);
    }

    private void AtualizarAlturaVirtualCamera()
    {
        if (cameraOffsetRoot == null)
            cameraOffsetRoot = ProcurarFilhoPorNome(transform, "Camera Offset");

        if (!usarAlturaVirtual || cameraOffsetRoot == null)
            return;

        Vector3 pos = offsetPosicaoCameraVirtual;
        float alturaCalculada;

        if (modoSentado)
            alturaCalculada = Mathf.Max(0f, alturaVirtualOlhos - alturaRealSentado + ajusteAlturaExtra);
        else
            alturaCalculada = ajusteAlturaExtra;

        pos.y += alturaCalculada;

        cameraOffsetRoot.localPosition = pos;
    }

    private void AtualizarPosicaoAvatarPelaCabeca()
    {
        if (!avatarSegueMovimentoFisicoDaCabeca || cameraVR == null || raizAvatar == null)
            return;

        Vector3 posicaoFisicaCabeca = cameraVR.position - ObterOffsetHorizontalCameraVirtualEmMundo();
        Vector3 alvo = raizAvatar.position;
        alvo.x = posicaoFisicaCabeca.x + offsetAvatarEmRelacaoCabeca.x;
        alvo.z = posicaoFisicaCabeca.z + offsetAvatarEmRelacaoCabeca.z;
        alvo.y = transform.position.y + offsetAvatarEmRelacaoCabeca.y;

        raizAvatar.position = Vector3.Lerp(
            raizAvatar.position,
            alvo,
            Mathf.Clamp01(Time.deltaTime * suavidadeSeguirCabecaXZ)
        );
    }

    private Vector3 ObterOffsetHorizontalCameraVirtualEmMundo()
    {
        Vector3 offsetHorizontalLocal = new Vector3(offsetPosicaoCameraVirtual.x, 0f, offsetPosicaoCameraVirtual.z);

        if (!usarAlturaVirtual || cameraOffsetRoot == null)
            return Vector3.zero;

        Transform referencia = cameraOffsetRoot.parent != null ? cameraOffsetRoot.parent : transform;
        Vector3 offsetMundo = referencia.TransformVector(offsetHorizontalLocal);
        offsetMundo.y = 0f;
        return offsetMundo;
    }

    private void ManterAvatarAlinhadoAoPlayer()
    {
        if (!manterAvatarNaOrigemDoPlayer || raizAvatar == null || raizAvatar == transform)
            return;

        if (raizAvatar.parent != transform)
            return;

        raizAvatar.localPosition = Vector3.zero;
    }

    private void AtualizarRotacaoDoAvatarInteiro()
    {
        if (!rotacionarAvatarInteiroComCabeca || cameraVR == null || raizAvatar == null)
            return;

        float yaw = cameraVR.eulerAngles.y + offsetRotacaoYawAvatar;

        if (inverterRotacaoYaw)
            yaw = -yaw;

        Quaternion rotacaoAlvo = Quaternion.Euler(0f, yaw, 0f);
        raizAvatar.rotation = Quaternion.Slerp(raizAvatar.rotation, rotacaoAlvo, FatorSuavizacao(suavidadeRotacaoRaiz));
    }

    private void ConfigurarReferenciasVR(bool sobrescrever)
    {
        if (sobrescrever || cameraVR == null)
            cameraVR = ProcurarFilhoPorNome(transform, "Main Camera") ?? cameraVR;

        if (sobrescrever || maoEsquerdaVR == null)
            maoEsquerdaVR = ProcurarFilhoPorNome(transform, "Left Controller") ?? maoEsquerdaVR;

        if (sobrescrever || maoDireitaVR == null)
            maoDireitaVR = ProcurarFilhoPorNome(transform, "Right Controller") ?? maoDireitaVR;

        if (sobrescrever || cameraOffsetRoot == null)
            cameraOffsetRoot = ProcurarFilhoPorNome(transform, "Camera Offset") ?? cameraOffsetRoot;
    }

    private void ConfigurarAnimatorAvatar(bool sobrescrever)
    {
        Transform avatarPreferido = ProcurarFilhoPorNome(transform, NomeAvatarPreferido);

        if (sobrescrever || animatorAvatar == null)
        {
            Animator animatorPreferido = avatarPreferido != null
                ? avatarPreferido.GetComponentInChildren<Animator>(true)
                : null;

            animatorAvatar = animatorPreferido != null
                ? animatorPreferido
                : GetComponentInChildren<Animator>(true);
        }

        if (sobrescrever || raizAvatar == null)
        {
            if (avatarPreferido != null)
                raizAvatar = avatarPreferido;
            else if (animatorAvatar != null)
                raizAvatar = animatorAvatar.transform;
        }
    }

    private void ConfigurarOssosAvatar(bool sobrescrever)
    {
        animatorHumanoidDisponivel = AnimatorHumanoidValido();

        if (animatorHumanoidDisponivel)
        {
            AtribuirOssoHumanoid(ref ossoCabeca, HumanBodyBones.Head, sobrescrever);
            AtribuirOssoHumanoid(ref ossoPescoco, HumanBodyBones.Neck, sobrescrever);
            AtribuirOssoHumanoid(ref ossoPeito, HumanBodyBones.UpperChest, sobrescrever);
            AtribuirOssoHumanoid(ref ossoColuna, HumanBodyBones.Spine, sobrescrever);
            AtribuirOssoHumanoid(ref bracoEsquerdoSuperior, HumanBodyBones.LeftUpperArm, sobrescrever);
            AtribuirOssoHumanoid(ref bracoEsquerdoInferior, HumanBodyBones.LeftLowerArm, sobrescrever);
            AtribuirOssoHumanoid(ref maoEsquerdaOsso, HumanBodyBones.LeftHand, sobrescrever);
            AtribuirOssoHumanoid(ref bracoDireitoSuperior, HumanBodyBones.RightUpperArm, sobrescrever);
            AtribuirOssoHumanoid(ref bracoDireitoInferior, HumanBodyBones.RightLowerArm, sobrescrever);
            AtribuirOssoHumanoid(ref maoDireitaOsso, HumanBodyBones.RightHand, sobrescrever);
            AtribuirOssoHumanoid(ref claviculaEsquerda, HumanBodyBones.LeftShoulder, sobrescrever);
            AtribuirOssoHumanoid(ref claviculaDireita, HumanBodyBones.RightShoulder, sobrescrever);

            if (ossoPeito == null)
                AtribuirOssoHumanoid(ref ossoPeito, HumanBodyBones.Chest, sobrescrever);
        }

        BuscarOssosPorNome(animatorHumanoidDisponivel ? false : sobrescrever);
    }

    private void ConfigurarAlvosIK(bool sobrescrever)
    {
        if (sobrescrever || alvoMaoEsquerda == null)
            alvoMaoEsquerda = ObterOuCriarAlvo(alvoMaoEsquerda, NomeLeftHandTargetAntigo, NomeAlvoMaoEsquerda, sobrescrever);

        if (sobrescrever || alvoMaoDireita == null)
            alvoMaoDireita = ObterOuCriarAlvo(alvoMaoDireita, NomeRightHandTargetAntigo, NomeAlvoMaoDireita, sobrescrever);

        if (sobrescrever || alvoCotoveloEsquerdo == null)
            alvoCotoveloEsquerdo = ObterOuCriarAlvo(alvoCotoveloEsquerdo, NomeAlvoCotoveloEsquerdo, NomeAlvoCotoveloEsquerdo, sobrescrever);

        if (sobrescrever || alvoCotoveloDireito == null)
            alvoCotoveloDireito = ObterOuCriarAlvo(alvoCotoveloDireito, NomeAlvoCotoveloDireito, NomeAlvoCotoveloDireito, sobrescrever);

        InicializarAlvoComOsso(alvoMaoEsquerda, maoEsquerdaOsso);
        InicializarAlvoComOsso(alvoMaoDireita, maoDireitaOsso);
        InicializarAlvoCotovelo(alvoCotoveloEsquerdo, true);
        InicializarAlvoCotovelo(alvoCotoveloDireito, false);
    }

    private Transform ObterOuCriarAlvo(Transform atual, string nomePreferido, string nomeCriacao, bool sobrescrever)
    {
        if (!sobrescrever && atual != null)
            return atual;

        Transform existente = ProcurarFilhoPorNome(transform, nomePreferido);
        if (existente == null && !string.Equals(nomePreferido, nomeCriacao, StringComparison.Ordinal))
            existente = ProcurarFilhoPorNome(transform, nomeCriacao);

        if (existente != null)
        {
            if (existente.parent != transform)
                existente.SetParent(transform, true);

            return existente;
        }

        GameObject criado = new GameObject(nomeCriacao);
        criado.transform.SetParent(transform, false);
        return criado.transform;
    }

    private void AtualizarAlvosDasMaos()
    {
        AtualizarAlvoMao(maoEsquerdaVR, alvoMaoEsquerda, offsetPosicaoMaoEsquerda, offsetRotacaoMaoEsquerdaEuler);
        AtualizarAlvoMao(maoDireitaVR, alvoMaoDireita, offsetPosicaoMaoDireita, offsetRotacaoMaoDireitaEuler);
    }

    private void AtualizarAlvoMao(Transform controle, Transform alvo, Vector3 offsetPosicao, Vector3 offsetRotacaoEuler)
    {
        if (controle == null || alvo == null)
            return;

        Vector3 posicaoAlvo = controle.position + controle.rotation * offsetPosicao;
        Quaternion rotacaoAlvo = controle.rotation * Quaternion.Euler(offsetRotacaoEuler);

        alvo.position = Vector3.Lerp(alvo.position, posicaoAlvo, FatorSuavizacao(suavidadeMao));
        alvo.rotation = Quaternion.Slerp(alvo.rotation, rotacaoAlvo, FatorSuavizacao(suavidadeRotacaoMao));
    }

    private void AtualizarAlvosDosCotovelos()
    {
        AtualizarAlvoCotovelo(true);
        AtualizarAlvoCotovelo(false);
    }

    private void AtualizarAlvoCotovelo(bool esquerdo)
    {
        Transform bracoSuperior = esquerdo ? bracoEsquerdoSuperior : bracoDireitoSuperior;
        Transform clavicula = esquerdo ? claviculaEsquerda : claviculaDireita;
        Transform alvoMao = esquerdo ? alvoMaoEsquerda : alvoMaoDireita;
        Transform alvoCotovelo = esquerdo ? alvoCotoveloEsquerdo : alvoCotoveloDireito;

        if (alvoCotovelo == null || alvoMao == null)
            return;

        Transform ombro = clavicula != null ? clavicula : bracoSuperior;
        if (ombro == null)
            return;

        Vector3 posicaoOmbro = ombro.position;
        Vector3 posicaoMao = alvoMao.position;
        Vector3 meioBraco = Vector3.Lerp(posicaoOmbro, posicaoMao, 0.5f);

        Vector3 direitaCorpo = ObterDireitaCorpo();
        Vector3 frenteCorpo = ObterFrenteCorpo();
        Vector3 lateral = esquerdo ? -direitaCorpo : direitaCorpo;
        Vector3 tras = -frenteCorpo;

        Vector3 posicaoAlvo = meioBraco +
                              lateral.normalized * afastamentoLateralCotovelo +
                              tras.normalized * distanciaAlvoCotovelo +
                              Vector3.up * alturaAlvoCotovelo;

        float distanciaMinima = Mathf.Max(0.08f, distanciaAlvoCotovelo * 0.35f);
        float distanciaOmbro = Vector3.Distance(posicaoAlvo, posicaoOmbro);
        float distanciaMao = Vector3.Distance(posicaoAlvo, posicaoMao);

        if (distanciaOmbro < distanciaMinima)
            posicaoAlvo += lateral.normalized * (distanciaMinima - distanciaOmbro);

        if (distanciaMao < distanciaMinima)
            posicaoAlvo += tras.normalized * (distanciaMinima - distanciaMao);

        alvoCotovelo.position = Vector3.Lerp(alvoCotovelo.position, posicaoAlvo, FatorSuavizacao(suavidadeMao));
    }

    private void AtualizarCabecaOpcional()
    {
        if (cameraVR == null || ossoCabeca == null)
            return;

        Vector3 posicaoAlvo = cameraVR.position + cameraVR.rotation * offsetPosicaoCabeca;
        Quaternion rotacaoAlvo = cameraVR.rotation * Quaternion.Euler(offsetRotacaoCabecaEuler);

        ossoCabeca.position = Vector3.Lerp(ossoCabeca.position, posicaoAlvo, FatorSuavizacao(suavidadeCabeca));
        ossoCabeca.rotation = Quaternion.Slerp(ossoCabeca.rotation, rotacaoAlvo, FatorSuavizacao(suavidadeCabeca));
    }

    private void AtualizarRotacaoSuaveDoCorpo()
    {
        if (rotacionarAvatarInteiroComCabeca)
            return;

        if (!rotacionarCorpoComCabeca || cameraVR == null)
            return;

        if (!rotacoesBaseCapturadas)
            CapturarRotacoesBase();

        Quaternion yawCamera = ObterYaw(cameraVR.forward);
        Quaternion deltaYaw = yawCamera * Quaternion.Inverse(baseYawAvatar);

        AplicarRotacaoParcial(ossoColuna, baseRotacaoColuna, deltaYaw, pesoRotacaoColuna);
        AplicarRotacaoParcial(ossoPeito, baseRotacaoPeito, deltaYaw, pesoRotacaoPeito);
        AplicarRotacaoParcial(ossoPescoco, baseRotacaoPescoco, deltaYaw, pesoRotacaoPescoco);
    }

    private void AplicarRotacaoParcial(Transform osso, Quaternion rotacaoBase, Quaternion deltaYaw, float peso)
    {
        if (osso == null || peso <= 0f)
            return;

        Quaternion rotacaoComYaw = deltaYaw * rotacaoBase;
        Quaternion alvo = Quaternion.Slerp(rotacaoBase, rotacaoComYaw, Mathf.Clamp01(peso));
        osso.rotation = Quaternion.Slerp(osso.rotation, alvo, FatorSuavizacao(suavidadeCorpo));
    }

    private void AplicarAnimatorIK(AvatarIKGoal goal, AvatarIKHint hintTipo, Transform alvoMao, Transform alvoCotovelo)
    {
        if (animatorAvatar == null || alvoMao == null)
            return;

        animatorAvatar.SetIKPositionWeight(goal, 1f);
        animatorAvatar.SetIKRotationWeight(goal, 1f);
        animatorAvatar.SetIKPosition(goal, alvoMao.position);
        animatorAvatar.SetIKRotation(goal, alvoMao.rotation);

        if (alvoCotovelo != null)
        {
            animatorAvatar.SetIKHintPositionWeight(hintTipo, 1f);
            animatorAvatar.SetIKHintPosition(hintTipo, alvoCotovelo.position);
        }
        else
        {
            animatorAvatar.SetIKHintPositionWeight(hintTipo, 0f);
        }
    }

    private void AtribuirOssoHumanoid(ref Transform campo, HumanBodyBones osso, bool sobrescrever)
    {
        if (!sobrescrever && campo != null)
            return;

        if (animatorAvatar == null)
            return;

        Transform transformOsso = animatorAvatar.GetBoneTransform(osso);
        if (transformOsso != null)
            campo = transformOsso;
    }

    private void BuscarOssosPorNome(bool sobrescrever)
    {
        Transform raizBusca = raizAvatar != null
            ? raizAvatar
            : animatorAvatar != null
                ? animatorAvatar.transform
                : transform;

        Transform[] ossos = raizBusca.GetComponentsInChildren<Transform>(true);

        AtribuirPorNome(ref ossoCabeca, sobrescrever, ossos, "cabeça", "cabeca", "head");
        AtribuirPorNome(ref ossoPescoco, sobrescrever, ossos, "pescoço", "pescoco", "neck");
        AtribuirPorNome(ref ossoPeito, sobrescrever, ossos, "peito", "chest", "upperchest");
        AtribuirPorNome(ref ossoColuna, sobrescrever, ossos, "coluna", "spine");
        AtribuirPorNome(ref bracoEsquerdoSuperior, sobrescrever, ossos, "braço esquerdo", "braco esquerdo", "leftupperarm", "upperarm_l", "arm_l");
        AtribuirPorNome(ref bracoEsquerdoInferior, sobrescrever, ossos, "antebraço esquerdo", "antebraco esquerdo", "leftlowerarm", "lowerarm_l", "forearm_l");
        AtribuirPorNome(ref maoEsquerdaOsso, sobrescrever, ossos, "mão esquerda", "mao esquerda", "lefthand", "hand_l");
        AtribuirPorNome(ref bracoDireitoSuperior, sobrescrever, ossos, "braço direito", "braco direito", "rightupperarm", "upperarm_r", "arm_r");
        AtribuirPorNome(ref bracoDireitoInferior, sobrescrever, ossos, "antebraço direito", "antebraco direito", "rightlowerarm", "lowerarm_r", "forearm_r");
        AtribuirPorNome(ref maoDireitaOsso, sobrescrever, ossos, "mão direita", "mao direita", "righthand", "hand_r");
        AtribuirPorNome(ref claviculaEsquerda, sobrescrever, ossos, "clavicula esquerda", "clavícula esquerda", "leftshoulder", "leftclavicle");
        AtribuirPorNome(ref claviculaDireita, sobrescrever, ossos, "clavicula direita", "clavícula direita", "rightshoulder", "rightclavicle");
    }

    private void AtribuirPorNome(ref Transform campo, bool sobrescrever, Transform[] ossos, params string[] nomesPossiveis)
    {
        if (!sobrescrever && campo != null)
            return;

        if (ossos == null)
            return;

        for (int i = 0; i < ossos.Length; i++)
        {
            Transform osso = ossos[i];
            if (osso == null)
                continue;

            string nomeNormalizado = NormalizarNome(osso.name);

            for (int n = 0; n < nomesPossiveis.Length; n++)
            {
                string candidato = NormalizarNome(nomesPossiveis[n]);
                if (nomeNormalizado == candidato ||
                    nomeNormalizado.EndsWith(candidato, StringComparison.Ordinal))
                {
                    campo = osso;
                    return;
                }
            }
        }
    }

    private Transform ProcurarFilhoPorNome(Transform raiz, string nome)
    {
        if (raiz == null || string.IsNullOrWhiteSpace(nome))
            return null;

        string nomeNormalizado = NormalizarNome(nome);
        Transform[] filhos = raiz.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < filhos.Length; i++)
        {
            Transform filho = filhos[i];
            if (filho != null && NormalizarNome(filho.name) == nomeNormalizado)
                return filho;
        }

        return null;
    }

    private void InicializarAlvoComOsso(Transform alvo, Transform osso)
    {
        if (alvo == null || osso == null)
            return;

        alvo.SetPositionAndRotation(osso.position, osso.rotation);
    }

    private void InicializarAlvoCotovelo(Transform alvoCotovelo, bool esquerdo)
    {
        if (alvoCotovelo == null)
            return;

        Transform bracoSuperior = esquerdo ? bracoEsquerdoSuperior : bracoDireitoSuperior;
        Transform maoOsso = esquerdo ? maoEsquerdaOsso : maoDireitaOsso;

        if (bracoSuperior == null || maoOsso == null)
            return;

        Vector3 meio = Vector3.Lerp(bracoSuperior.position, maoOsso.position, 0.5f);
        Vector3 lateral = esquerdo ? -ObterDireitaCorpo() : ObterDireitaCorpo();
        alvoCotovelo.position = meio + lateral.normalized * afastamentoLateralCotovelo - ObterFrenteCorpo() * distanciaAlvoCotovelo;
    }

    private void CapturarRotacoesBase()
    {
        Transform referenciaYaw = raizAvatar != null ? raizAvatar : transform;
        baseYawAvatar = ObterYaw(referenciaYaw.forward);
        baseRotacaoPescoco = ossoPescoco != null ? ossoPescoco.rotation : Quaternion.identity;
        baseRotacaoPeito = ossoPeito != null ? ossoPeito.rotation : Quaternion.identity;
        baseRotacaoColuna = ossoColuna != null ? ossoColuna.rotation : Quaternion.identity;
        rotacoesBaseCapturadas = true;
    }

    private Vector3 ObterDireitaCorpo()
    {
        Transform referencia = ossoPeito != null ? ossoPeito : transform;
        Vector3 direita = referencia.right;
        direita.y = 0f;

        if (direita.sqrMagnitude < 0.0001f)
            direita = transform.right;

        direita.y = 0f;
        return direita.sqrMagnitude > 0.0001f ? direita.normalized : Vector3.right;
    }

    private Vector3 ObterFrenteCorpo()
    {
        Transform referencia = ossoPeito != null ? ossoPeito : transform;
        Vector3 frente = referencia.forward;
        frente.y = 0f;

        if (frente.sqrMagnitude < 0.0001f && cameraVR != null)
        {
            frente = cameraVR.forward;
            frente.y = 0f;
        }

        if (frente.sqrMagnitude < 0.0001f)
            frente = transform.forward;

        frente.y = 0f;
        return frente.sqrMagnitude > 0.0001f ? frente.normalized : Vector3.forward;
    }

    private Quaternion ObterYaw(Vector3 forward)
    {
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private float FatorSuavizacao(float suavidade)
    {
        if (suavidade <= 0f)
            return 1f;

        return 1f - Mathf.Exp(-suavidade * Time.deltaTime);
    }

    private bool AnimatorHumanoidValido()
    {
        return animatorAvatar != null &&
               animatorAvatar.avatar != null &&
               animatorAvatar.avatar.isValid &&
               animatorAvatar.avatar.isHuman;
    }

    private bool RuntimeAnimatorControllerValido()
    {
        return animatorAvatar != null && animatorAvatar.runtimeAnimatorController != null;
    }

    private void DesativarAvatarBodyNoMesmoGameObject()
    {
        // AvatarVRIKController substitui AvatarBody no controle do avatar.
        // Não destruímos o componente; apenas desativamos se ainda estiver ativo no mesmo GameObject.
        Component avatarBody = GetComponent("AvatarBody");
        if (avatarBody is Behaviour comportamento && comportamento.enabled)
            comportamento.enabled = false;
    }

    private void NormalizarValores()
    {
        suavidadeMao = Mathf.Max(0f, suavidadeMao);
        suavidadeRotacaoMao = Mathf.Max(0f, suavidadeRotacaoMao);
        suavidadeCabeca = Mathf.Max(0f, suavidadeCabeca);
        suavidadeCorpo = Mathf.Max(0f, suavidadeCorpo);
        suavidadeRotacaoRaiz = Mathf.Max(0f, suavidadeRotacaoRaiz);
        suavidadeSeguirCabecaXZ = Mathf.Max(0f, suavidadeSeguirCabecaXZ);
        alturaVirtualOlhos = Mathf.Max(0f, alturaVirtualOlhos);
        alturaRealSentado = Mathf.Max(0f, alturaRealSentado);
        distanciaAlvoCotovelo = Mathf.Max(0.01f, distanciaAlvoCotovelo);
        afastamentoLateralCotovelo = Mathf.Max(0.01f, afastamentoLateralCotovelo);
        pesoRotacaoPeito = Mathf.Clamp01(pesoRotacaoPeito);
        pesoRotacaoColuna = Mathf.Clamp01(pesoRotacaoColuna);
        pesoRotacaoPescoco = Mathf.Clamp01(pesoRotacaoPescoco);
    }

    private void AtualizarStatusConfiguracao()
    {
        animationRiggingDisponivel = AnimationRiggingEstaDisponivel();
        animatorHumanoidDisponivel = AnimatorHumanoidValido();

        bool animatorEncontrado = animatorAvatar != null;
        bool avatarEncontrado = animatorEncontrado && animatorAvatar.avatar != null;
        bool avatarValido = avatarEncontrado && animatorAvatar.avatar.isValid;
        bool avatarHumanoid = avatarValido && animatorAvatar.avatar.isHuman;
        bool controllerEncontrado = RuntimeAnimatorControllerValido();
        bool cameraVREncontrada = cameraVR != null;
        bool cameraOffsetEncontrado = cameraOffsetRoot != null;
        bool alturaVirtualConfigurada = !usarAlturaVirtual || cameraOffsetEncontrado;
        bool raizAvatarEncontrada = raizAvatar != null;
        bool seguirMovimentoFisicoConfigurado = avatarSegueMovimentoFisicoDaCabeca && cameraVREncontrada && raizAvatarEncontrada;
        bool cabecaEncontrada = ossoCabeca != null;
        bool bracoEsquerdoEncontrado = bracoEsquerdoSuperior != null;
        bool antebracoEsquerdoEncontrado = bracoEsquerdoInferior != null;
        bool maoEsquerdaEncontrada = maoEsquerdaOsso != null;
        bool bracoDireitoEncontrado = bracoDireitoSuperior != null;
        bool antebracoDireitoEncontrado = bracoDireitoInferior != null;
        bool maoDireitaEncontrada = maoDireitaOsso != null;
        bool targetsEncontrados = alvoMaoEsquerda != null && alvoMaoDireita != null;
        bool hintsEncontrados = alvoCotoveloEsquerdo != null && alvoCotoveloDireito != null;

        StringBuilder status = new StringBuilder(900);
        status.AppendLine(LinhaStatus("Animator encontrado", animatorEncontrado));
        status.AppendLine(LinhaStatus("Avatar encontrado", avatarEncontrado));
        status.AppendLine(LinhaStatus("Avatar v\u00e1lido", avatarValido));
        status.AppendLine(LinhaStatus("Avatar Humanoid", avatarHumanoid));
        status.AppendLine(LinhaStatus("Runtime Controller encontrado", controllerEncontrado));
        status.AppendLine(LinhaStatus("Camera VR encontrada", cameraVREncontrada));
        status.AppendLine(LinhaStatus("Camera Offset Root encontrado", cameraOffsetEncontrado));
        status.AppendLine(LinhaStatus("Altura Virtual VR configurada", alturaVirtualConfigurada));
        status.AppendLine(LinhaStatus("Raiz do avatar encontrada", raizAvatarEncontrada));
        status.AppendLine(LinhaStatus("Avatar segue movimento fisico da cabeca", seguirMovimentoFisicoConfigurado));
        status.AppendLine("\u2714 IK Pass: verifica\u00e7\u00e3o manual necess\u00e1ria na Base Layer do Animator.");
        status.AppendLine(LinhaStatus("Cabe\u00e7a encontrada", cabecaEncontrada));
        status.AppendLine(LinhaStatus("Bra\u00e7o esquerdo encontrado", bracoEsquerdoEncontrado));
        status.AppendLine(LinhaStatus("Antebra\u00e7o esquerdo encontrado", antebracoEsquerdoEncontrado));
        status.AppendLine(LinhaStatus("M\u00e3o esquerda encontrada", maoEsquerdaEncontrada));
        status.AppendLine(LinhaStatus("Bra\u00e7o direito encontrado", bracoDireitoEncontrado));
        status.AppendLine(LinhaStatus("Antebra\u00e7o direito encontrado", antebracoDireitoEncontrado));
        status.AppendLine(LinhaStatus("M\u00e3o direita encontrada", maoDireitaEncontrada));
        status.AppendLine(LinhaStatus("Targets encontrados", targetsEncontrados));
        status.AppendLine(LinhaStatus("Hints encontrados", hintsEncontrados));
        status.AppendLine();

        if (!animatorEncontrado)
        {
            status.AppendLine("Animator ausente: o controle simples de raiz, movimento fisico da cabeca e targets ainda pode funcionar se Camera VR, raiz do avatar e controles VR estiverem configurados.");
            status.AppendLine("Pendente para Animator IK/Humanoid: adicione ou arraste o Animator do avatar vis\u00edvel para este campo.");
        }
        else if (!avatarEncontrado)
        {
            status.AppendLine("Avatar n\u00e3o configurado. Configure o modelo como Humanoid.");
            AdicionarPassoManualHumanoid(status);
        }
        else if (!avatarValido)
        {
            status.AppendLine("Avatar inv\u00e1lido. Reconfigure o Avatar no Import Settings do modelo.");
            AdicionarPassoManualHumanoid(status);
        }
        else if (!avatarHumanoid)
        {
            status.AppendLine("O modelo n\u00e3o est\u00e1 configurado como Humanoid.");
            AdicionarPassoManualHumanoid(status);
        }
        else if (!controllerEncontrado)
        {
            status.AppendLine("O Animator n\u00e3o possui RuntimeAnimatorController.");
            status.AppendLine("Passo manual: atribua um Controller no campo Controller do Animator. N\u00e3o foi criado controller vazio por c\u00f3digo.");
        }
        else
        {
            status.AppendLine("Configura\u00e7\u00e3o obrigat\u00f3ria do avatar est\u00e1 correta.");
            status.AppendLine("Verifique manualmente se IK Pass est\u00e1 ativado na Base Layer do Animator.");
        }

        if (!cameraVREncontrada)
            status.AppendLine("Use 'Configurar Avatar VR Automaticamente' para preencher Camera VR automaticamente.");

        if (usarAlturaVirtual && !cameraOffsetEncontrado)
            status.AppendLine("Use 'Configurar Avatar VR Automaticamente' para preencher Camera Offset Root com o objeto 'Camera Offset'.");

        if (!raizAvatarEncontrada)
            status.AppendLine("Use 'Configurar Avatar VR Automaticamente' para preencher a raiz do avatar com o filho 'avatar masculino'.");

        if (!targetsEncontrados || !hintsEncontrados)
            status.AppendLine("Use 'Configurar Avatar VR Automaticamente' para criar/preencher Targets e Hints. Isso n\u00e3o depende de Humanoid perfeito.");

        if (!animationRiggingDisponivel && usarAnimatorIKHumanoidComoFallback)
            status.AppendLine("Animation Rigging n\u00e3o detectado: usando fallback por Animator IK Humanoid, dependente do IK Pass.");
        else if (animationRiggingDisponivel)
            status.AppendLine("Animation Rigging detectado: os targets/hints deste script podem alimentar o rig configurado no avatar.");

        statusConfiguracao = status.ToString().TrimEnd();
    }

    private static string LinhaStatus(string item, bool ok)
    {
        return (ok ? "\u2714 " : "\u2718 ") + item;
    }

    private static void AdicionarPassoManualHumanoid(StringBuilder status)
    {
        status.AppendLine("Passo manual:");
        status.AppendLine("1. Abra o modelo FBX do avatar.");
        status.AppendLine("2. Rig > Animation Type = Humanoid.");
        status.AppendLine("3. Avatar Definition = Create From This Model.");
        status.AppendLine("4. Apply.");
    }

    private bool AnimationRiggingEstaDisponivel()
    {
        return Type.GetType("UnityEngine.Animations.Rigging.RigBuilder, Unity.Animation.Rigging") != null &&
               Type.GetType("UnityEngine.Animations.Rigging.TwoBoneIKConstraint, Unity.Animation.Rigging") != null;
    }

    private static string NormalizarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return string.Empty;

        return nome
            .ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(".", string.Empty)
            .Replace(":", string.Empty);
    }

    public bool AnimationRiggingDisponivel => animationRiggingDisponivel;
    public bool AnimatorHumanoidDisponivel => animatorHumanoidDisponivel;
    public Transform RaizAvatar => raizAvatar;
    public Transform AlvoMaoEsquerda => alvoMaoEsquerda;
    public Transform AlvoMaoDireita => alvoMaoDireita;
    public Transform AlvoCotoveloEsquerdo => alvoCotoveloEsquerdo;
    public Transform AlvoCotoveloDireito => alvoCotoveloDireito;
    public const string NomeRigRecomendado = NomeRigArms;
}
