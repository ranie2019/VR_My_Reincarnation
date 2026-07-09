using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class AvatarVRIKController : MonoBehaviour
{
    private enum EixoLocalAvancoOmbro
    {
        X,
        Y,
        Z
    }

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
    [FormerlySerializedAs("offsetRotacaoMaoEsquerdaEuler")]
    [SerializeField] private Vector3 offsetRotacaoMaoEsquerda = Vector3.zero;

    [Header("Offsets da Mão Direita")]
    [Tooltip("Offset local de posição aplicado a partir da rotação do controle direito.")]
    [SerializeField] private Vector3 offsetPosicaoMaoDireita = Vector3.zero;

    [Tooltip("Offset local de rotação aplicado ao controle direito.")]
    [FormerlySerializedAs("offsetRotacaoMaoDireitaEuler")]
    [SerializeField] private Vector3 offsetRotacaoMaoDireita = Vector3.zero;

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

    [Header("Animation Rigging - Bracos VR")]
    [Tooltip("Quando ativo, usa Two Bone IK para fazer a mao real chegar ao controle VR.")]
    [SerializeField] private bool usarAnimationRiggingBracos = true;

    [Tooltip("Cria/configura RigBuilder, Rig e Two Bone IK automaticamente quando as referencias estao preenchidas.")]
    [SerializeField] private bool configurarAnimationRiggingAutomaticamente = true;

    [Tooltip("Se existir um ponto de interacao dentro do controle, usa esse ponto como rastreador da mao.")]
    [SerializeField] private bool usarPontoInteracaoDoControle = true;

    [Tooltip("Referencia final da mao esquerda no controle VR. Se vazio, sera preenchido automaticamente.")]
    [SerializeField] private Transform referenciaMaoEsquerdaVR;

    [Tooltip("Referencia final da mao direita no controle VR. Se vazio, sera preenchido automaticamente.")]
    [SerializeField] private Transform referenciaMaoDireitaVR;

    [SerializeField] private RigBuilder rigBuilderAvatar;
    [SerializeField] private Rig rigBracosVR;
    [SerializeField] private TwoBoneIKConstraint ikBracoEsquerdo;
    [SerializeField] private TwoBoneIKConstraint ikBracoDireito;
    [SerializeField, Range(0f, 1f)] private float pesoAnimationRiggingBracos = 1f;

    [Header("Movimentacao Direta dos Bracos")]
    [Tooltip("Modo temporario: targets seguem os controles VR. Se Animation Rigging estiver configurado, o Two Bone IK move os bracos.")]
    [SerializeField] private bool MovimentacaoDiretaDosBracos = true;

    [SerializeField, Range(0f, 1f)] private float pesoBraco = 1f;
    [SerializeField, Range(0f, 1f)] private float pesoAnteBraco = 1f;
    [SerializeField, Range(0f, 1f)] private float pesoMao = 1f;
    [SerializeField] private bool permitirStretch = true;
    [SerializeField, Min(1f)] private float limiteStretch = 1.08f;

    [Header("Acompanhamento do Antebraço com a Mão")]
    [Tooltip("Quando ativo, o antebraço acompanha a torção da mão no eixo real antebraço -> mão. Mantém a mão apontando para o controle/laser e distribui parte da rotação para o antebraço.")]
    [SerializeField] private bool usarAcompanhamentoAntebracoComMao = true;

    [Tooltip("Quanto da torção da mão direita é repassada para o antebraço direito. 0 = não acompanha. 1 = acompanha totalmente dentro do limite.")]
    [SerializeField, Range(0f, 1f)] private float pesoAcompanhamentoAntebracoDireito = 0.75f;

    [Tooltip("Quanto da torção da mão esquerda é repassada para o antebraço esquerdo. 0 = não acompanha. 1 = acompanha totalmente dentro do limite.")]
    [SerializeField, Range(0f, 1f)] private float pesoAcompanhamentoAntebracoEsquerdo = 0.75f;

    [Tooltip("Limite máximo de torção aplicada no antebraço direito por frame, em graus.")]
    [SerializeField, Range(0f, 120f)] private float limiteAcompanhamentoAntebracoDireito = 65f;

    [Tooltip("Limite máximo de torção aplicada no antebraço esquerdo por frame, em graus.")]
    [SerializeField, Range(0f, 120f)] private float limiteAcompanhamentoAntebracoEsquerdo = 65f;

    [Tooltip("Inverte somente o sinal do acompanhamento do antebraço direito, caso ele gire para o lado contrário da mão.")]
    [SerializeField] private bool inverterAcompanhamentoAntebracoDireito = false;

    [Tooltip("Inverte somente o sinal do acompanhamento do antebraço esquerdo, caso ele gire para o lado contrário da mão.")]
    [SerializeField] private bool inverterAcompanhamentoAntebracoEsquerdo = false;

    [Tooltip("Usa a diferença inicial entre antebraço e mão como pose neutra. Desligue apenas se o antebraço ficar torcido em repouso.")]
    [SerializeField] private bool usarPoseBaseNaturalAntebraco = true;

    [Header("Diagnóstico Antebraço/Mão")]
    [SerializeField] private float twistMaoDireitaGraus;
    [SerializeField] private float twistMaoEsquerdaGraus;
    [SerializeField] private float twistAplicadoAntebracoDireitoGraus;
    [SerializeField] private float twistAplicadoAntebracoEsquerdoGraus;
    [SerializeField] private bool eixoAcompanhamentoDireitoValido;
    [SerializeField] private bool eixoAcompanhamentoEsquerdoValido;

    [Header("Avanço do Ombro (opcional)")]
    [Tooltip("Quando ativo, permite que o ombro (clavícula) gire um pouco no eixo local selecionado, imitando o movimento humano de levar o ombro à frente para aumentar o alcance da mão. Se desligado, o ombro fica sempre na pose natural.")]
    [SerializeField] private bool usarAvancoDoOmbro = false;

    [Tooltip("Eixo local usado para aplicar o avanco visual da clavicula/ombro. Use X, Y ou Z para descobrir qual eixo do rig move o ombro para frente sem torcer o braco.")]
    [SerializeField] private EixoLocalAvancoOmbro eixoLocalAvancoOmbro = EixoLocalAvancoOmbro.Z;

    [Tooltip("Ângulo máximo de avanço do ombro, em graus. Recomendado 3 a 8 graus para ficar sutil e natural; nunca passa do Limite Máximo Ombro abaixo, mesmo se você aumentar este valor.")]
    [SerializeField, Range(0f, 30f)] private float anguloMaximoAvancoOmbro = 6f;

    [Tooltip("Distância à frente do ombro (em metros) a partir da qual o avanço começa a aparecer. Abaixo disso o ombro fica parado.")]
    [SerializeField, Min(0f)] private float limiarAvancoOmbro = 0.15f;

    [Tooltip("Distância à frente do ombro (em metros) na qual o avanço já atinge o ângulo máximo.")]
    [SerializeField, Min(0.01f)] private float distanciaAvancoOmbroMaximo = 0.45f;

    [Tooltip("Suavidade da transição do avanço do ombro (maior = reage mais rápido).")]
    [SerializeField] private float suavidadeAvancoOmbro = 10f;

    [Tooltip("Limite público de segurança (em graus) para a rotação do ombro no eixo local selecionado. Trava o ângulo final e impede qualquer rotação exagerada (ex.: 360°), mesmo que outros valores estejam configurados errado. Ex.: -30 e 30.")]
    [SerializeField] private float limiteMinimoOmbro = -30f;

    [SerializeField] private float limiteMaximoOmbro = 30f;

    [Tooltip("Inverte o sinal do avanço do ombro direito, caso o ombro gire para o lado errado no seu esqueleto.")]
    [SerializeField] private bool inverterAvancoOmbroDireito = false;

    [Tooltip("Inverte o sinal do avanço do ombro esquerdo, caso o ombro gire para o lado errado no seu esqueleto.")]
    [SerializeField] private bool inverterAvancoOmbroEsquerdo = false;

    [Header("Diagnóstico Avanço do Ombro")]
    [SerializeField] private float avancoOmbroDireitoGraus;
    [SerializeField] private float avancoOmbroEsquerdoGraus;

    [Header("Diagnóstico Visual das Mãos")]
    [SerializeField] private bool mostrarGizmosMaos = true;
    [SerializeField] private float tamanhoGizmoMao = 0.06f;

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
    private const string NomeRigArms = "Rig_Bracos_VR";
    private const string NomeIKBracoEsquerdoVR = "IK_Braco_Esquerdo_VR";
    private const string NomeIKBracoDireitoVR = "IK_Braco_Direito_VR";

    private bool animationRiggingDisponivel;
    private bool animatorHumanoidDisponivel;
    private bool animationRiggingBracosAtivo;
    private Quaternion baseYawAvatar = Quaternion.identity;
    private Quaternion baseRotacaoPescoco = Quaternion.identity;
    private Quaternion baseRotacaoPeito = Quaternion.identity;
    private Quaternion baseRotacaoColuna = Quaternion.identity;
    private bool rotacoesBaseCapturadas;
    private bool dadosNaturaisBracosCapturados;
    private Vector3 escalaNaturalBracoEsquerdoSuperior = Vector3.one;
    private Vector3 escalaNaturalBracoEsquerdoInferior = Vector3.one;
    private Vector3 escalaNaturalBracoDireitoSuperior = Vector3.one;
    private Vector3 escalaNaturalBracoDireitoInferior = Vector3.one;
    private float comprimentoNaturalBracoEsquerdoSuperior;
    private float comprimentoNaturalBracoEsquerdoInferior;
    private float comprimentoNaturalBracoDireitoSuperior;
    private float comprimentoNaturalBracoDireitoInferior;
    private bool baseTwistAntebracoMaoCapturada;
    private float baseTwistAntebracoMaoEsquerda;
    private float baseTwistAntebracoMaoDireita;
    private bool baseRotacaoOmbroCapturada;
    private Quaternion baseLocalRotacaoClaviculaEsquerda = Quaternion.identity;
    private Quaternion baseLocalRotacaoClaviculaDireita = Quaternion.identity;
    private float anguloAtualOmbroEsquerdo;
    private float anguloAtualOmbroDireito;

    private void Reset()
    {
        ConfigurarAvatarAutomaticamenteInterno(true);
        AtualizarAlturaVirtualCamera();
        CapturarRotacoesBase();
        CapturarDadosNaturaisBracos();
        CapturarBaseTwistAntebracoMao();
        CapturarBaseRotacaoOmbros();
        AtualizarStatusConfiguracao();
    }

    private void Awake()
    {
        DesativarAvatarBodyNoMesmoGameObject();
        ConfigurarAvatarAutomaticamenteInterno(false);
        AtualizarAlturaVirtualCamera();
        CapturarRotacoesBase();
        CapturarDadosNaturaisBracos();
        CapturarBaseTwistAntebracoMao();
        CapturarBaseRotacaoOmbros();
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
        CapturarDadosNaturaisBracos();
        CapturarBaseTwistAntebracoMao();
        CapturarBaseRotacaoOmbros();
        AtualizarStatusConfiguracao();
    }

    [ContextMenu("Verificar Configura\u00e7\u00e3o do Avatar")]
    public void VerificarConfiguracaoDoAvatar()
    {
        ConfigurarAvatarAutomaticamenteInterno(false);
        CapturarRotacoesBase();
        CapturarDadosNaturaisBracos();
        CapturarBaseTwistAntebracoMao();
        CapturarBaseRotacaoOmbros();
        AtualizarStatusConfiguracao();
    }

    private void ConfigurarAvatarAutomaticamenteInterno(bool sobrescreverCamposPreenchidos)
    {
        NormalizarValores();
        ConfigurarReferenciasVR(sobrescreverCamposPreenchidos);
        ConfigurarAnimatorAvatar(sobrescreverCamposPreenchidos);
        ConfigurarOssosAvatar(sobrescreverCamposPreenchidos);
        ConfigurarAlvosIK(sobrescreverCamposPreenchidos);
        ConfigurarReferenciasMaoVR(sobrescreverCamposPreenchidos);
        ConfigurarAnimationRiggingBracos(sobrescreverCamposPreenchidos);
    }

    private void Update()
    {
        if (AnimationRiggingBracosPodeControlar())
        {
            AtualizarAlvosAnimationRiggingBracos(false);
        }
        else
        {
            RestaurarOmbroNatural(claviculaEsquerda, baseLocalRotacaoClaviculaEsquerda, ref anguloAtualOmbroEsquerdo);
            RestaurarOmbroNatural(claviculaDireita, baseLocalRotacaoClaviculaDireita, ref anguloAtualOmbroDireito);
        }
    }

    private void LateUpdate()
    {
        AtualizarAlturaVirtualCamera();
        AtualizarPosicaoAvatarPelaCabeca();

        if (!avatarSegueMovimentoFisicoDaCabeca)
            ManterAvatarAlinhadoAoPlayer();

        AtualizarRotacaoDoAvatarInteiro();

        bool usarTwoBoneIKBracos = AnimationRiggingBracosPodeControlar();

        if (!usarIK && !MovimentacaoDiretaDosBracos && !usarTwoBoneIKBracos)
            return;

        if (usarTwoBoneIKBracos)
        {
            AtualizarAlvosAnimationRiggingBracos(true);
        }
        else if (MovimentacaoDiretaDosBracos)
        {
            AtualizarMovimentacaoDiretaDosBracos();
        }
        else
        {
            AtualizarAlvosDasMaos();
            AtualizarAlvosDosCotovelos();
        }

        if (controlarCabeca)
            AtualizarCabecaOpcional();

        if (controlarCorpo && !rotacionarAvatarInteiroComCabeca)
            AtualizarRotacaoSuaveDoCorpo();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (MovimentacaoDiretaDosBracos || AnimationRiggingBracosPodeControlar())
            return;

        if (!usarIK || !usarAnimatorIKHumanoidComoFallback || !AnimatorHumanoidValido())
            return;

        AtualizarAlvosDasMaos();
        AtualizarAlvosDosCotovelos();

        AplicarAnimatorIK(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, alvoMaoEsquerda, alvoCotoveloEsquerdo);
        AplicarAnimatorIK(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, alvoMaoDireita, alvoCotoveloDireito);
    }

    private void OnDrawGizmos()
    {
        if (!mostrarGizmosMaos)
            return;

        DesenharGizmoMao(maoEsquerdaVR, Color.blue, "Controle Esquerdo");
        DesenharGizmoMao(alvoMaoEsquerda, Color.cyan, "Alvo Mão Esquerda");

        DesenharGizmoMao(maoDireitaVR, Color.red, "Controle Direito");
        DesenharGizmoMao(alvoMaoDireita, Color.magenta, "Alvo Mão Direita");
    }

    private void DesenharGizmoMao(Transform alvo, Color cor, string nome)
    {
        if (alvo == null)
            return;

        Gizmos.color = cor;
        Gizmos.DrawSphere(alvo.position, tamanhoGizmoMao);

        Gizmos.DrawLine(
            alvo.position,
            alvo.position + alvo.forward * tamanhoGizmoMao * 3f
        );
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

    private void ConfigurarReferenciasMaoVR(bool sobrescrever)
    {
        if (sobrescrever || referenciaMaoEsquerdaVR == null)
            referenciaMaoEsquerdaVR = ObterReferenciaPreferidaControle(maoEsquerdaVR) ?? referenciaMaoEsquerdaVR;

        if (sobrescrever || referenciaMaoDireitaVR == null)
            referenciaMaoDireitaVR = ObterReferenciaPreferidaControle(maoDireitaVR) ?? referenciaMaoDireitaVR;
    }

    private Transform ObterReferenciaPreferidaControle(Transform controle)
    {
        if (controle == null)
            return null;

        if (!usarPontoInteracaoDoControle)
            return controle;

        Transform pontoInteracao =
            ProcurarFilhoPorNome(controle, "Poke Point") ??
            ProcurarFilhoPorNome(controle, "Interaction Attach") ??
            ProcurarFilhoPorNome(controle, "InteractionAttach") ??
            ProcurarFilhoPorNome(controle, "Ray Origin") ??
            ProcurarFilhoPorNome(controle, "Laser Origin") ??
            ProcurarFilhoPorNome(controle, "Line Visual") ??
            ProcurarFilhoPorNome(controle, "LineVisual");

        return pontoInteracao != null ? pontoInteracao : controle;
    }

    private Transform ObterReferenciaMaoAtual(bool esquerdo)
    {
        Transform referencia = esquerdo ? referenciaMaoEsquerdaVR : referenciaMaoDireitaVR;
        if (referencia != null)
            return referencia;

        Transform controle = esquerdo ? maoEsquerdaVR : maoDireitaVR;
        referencia = ObterReferenciaPreferidaControle(controle);

        if (esquerdo)
            referenciaMaoEsquerdaVR = referencia;
        else
            referenciaMaoDireitaVR = referencia;

        return referencia;
    }

    private void ConfigurarAnimationRiggingBracos(bool sobrescrever)
    {
        animationRiggingBracosAtivo = false;

        if (!usarAnimationRiggingBracos || animatorAvatar == null)
            return;

        bool podeCriar = configurarAnimationRiggingAutomaticamente;

        if (sobrescrever || rigBuilderAvatar == null)
            rigBuilderAvatar = animatorAvatar.GetComponent<RigBuilder>();

        if (rigBuilderAvatar == null && podeCriar)
            rigBuilderAvatar = animatorAvatar.gameObject.AddComponent<RigBuilder>();

        if (rigBuilderAvatar == null)
            return;

        rigBuilderAvatar.enabled = true;

        if (sobrescrever || rigBracosVR == null)
            rigBracosVR = ObterRigBracosExistente();

        if (rigBracosVR == null && podeCriar)
        {
            Transform rigTransform = ObterOuCriarFilhoDireto(animatorAvatar.transform, NomeRigArms);
            rigBracosVR = rigTransform.GetComponent<Rig>();

            if (rigBracosVR == null)
                rigBracosVR = rigTransform.gameObject.AddComponent<Rig>();
        }

        if (rigBracosVR == null)
            return;

        rigBracosVR.enabled = true;
        rigBracosVR.weight = pesoAnimationRiggingBracos;
        RegistrarRigNoBuilder();

        bool esquerdoConfigurado = ConfigurarTwoBoneIK(
            ref ikBracoEsquerdo,
            NomeIKBracoEsquerdoVR,
            bracoEsquerdoSuperior,
            bracoEsquerdoInferior,
            maoEsquerdaOsso,
            alvoMaoEsquerda,
            alvoCotoveloEsquerdo,
            sobrescrever,
            podeCriar
        );

        bool direitoConfigurado = ConfigurarTwoBoneIK(
            ref ikBracoDireito,
            NomeIKBracoDireitoVR,
            bracoDireitoSuperior,
            bracoDireitoInferior,
            maoDireitaOsso,
            alvoMaoDireita,
            alvoCotoveloDireito,
            sobrescrever,
            podeCriar
        );

        animationRiggingBracosAtivo = esquerdoConfigurado && direitoConfigurado;

        if (animationRiggingBracosAtivo && Application.isPlaying)
        {
            rigBuilderAvatar.Clear();
            rigBuilderAvatar.Build();
        }
    }

    private Rig ObterRigBracosExistente()
    {
        Transform raizBusca = animatorAvatar != null ? animatorAvatar.transform : raizAvatar;
        if (raizBusca == null)
            return null;

        Transform rigTransform = ProcurarFilhoPorNome(raizBusca, NomeRigArms);
        return rigTransform != null ? rigTransform.GetComponent<Rig>() : null;
    }

    private Transform ObterOuCriarFilhoDireto(Transform pai, string nome)
    {
        if (pai == null)
            return null;

        for (int i = 0; i < pai.childCount; i++)
        {
            Transform filho = pai.GetChild(i);
            if (filho != null && string.Equals(filho.name, nome, StringComparison.Ordinal))
                return filho;
        }

        GameObject criado = new GameObject(nome);
        criado.transform.SetParent(pai, false);
        return criado.transform;
    }

    private void RegistrarRigNoBuilder()
    {
        if (rigBuilderAvatar == null || rigBracosVR == null)
            return;

        List<RigLayer> layers = rigBuilderAvatar.layers;

        for (int i = 0; i < layers.Count; i++)
        {
            RigLayer layer = layers[i];
            if (layer != null && layer.rig == rigBracosVR)
            {
                layer.active = true;
                return;
            }
        }

        layers.Add(new RigLayer(rigBracosVR, true));
    }

    private bool ConfigurarTwoBoneIK(
        ref TwoBoneIKConstraint constraint,
        string nomeObjeto,
        Transform root,
        Transform mid,
        Transform tip,
        Transform target,
        Transform hint,
        bool sobrescrever,
        bool podeCriar
    )
    {
        if (root == null || mid == null || tip == null || target == null)
            return false;

        if (sobrescrever || constraint == null)
            constraint = EncontrarTwoBoneIK(nomeObjeto, tip, target);

        if (constraint == null && podeCriar && rigBracosVR != null)
        {
            Transform constraintTransform = ObterOuCriarFilhoDireto(rigBracosVR.transform, nomeObjeto);
            constraint = constraintTransform.GetComponent<TwoBoneIKConstraint>();

            if (constraint == null)
                constraint = constraintTransform.gameObject.AddComponent<TwoBoneIKConstraint>();
        }

        if (constraint == null)
            return false;

        constraint.enabled = true;
        constraint.weight = 1f;
        ref TwoBoneIKConstraintData data = ref constraint.data;
        data.root = root;
        data.mid = mid;
        data.tip = tip;
        data.target = target;
        data.hint = hint;
        data.targetPositionWeight = 1f;
        data.targetRotationWeight = 1f;
        data.hintWeight = hint != null ? 1f : 0f;
        data.maintainTargetPositionOffset = false;
        data.maintainTargetRotationOffset = false;

        return ConstraintTwoBoneIKConfigurado(constraint, root, mid, tip, target);
    }

    private TwoBoneIKConstraint EncontrarTwoBoneIK(string nomeObjeto, Transform tip, Transform target)
    {
        Transform raizBusca = raizAvatar != null ? raizAvatar : transform;
        TwoBoneIKConstraint[] constraints = raizBusca.GetComponentsInChildren<TwoBoneIKConstraint>(true);

        for (int i = 0; i < constraints.Length; i++)
        {
            TwoBoneIKConstraint constraint = constraints[i];
            if (constraint != null && string.Equals(constraint.gameObject.name, nomeObjeto, StringComparison.Ordinal))
                return constraint;
        }

        for (int i = 0; i < constraints.Length; i++)
        {
            TwoBoneIKConstraint constraint = constraints[i];
            if (constraint == null)
                continue;

            if ((tip != null && constraint.data.tip == tip) ||
                (target != null && constraint.data.target == target))
            {
                return constraint;
            }
        }

        return null;
    }

    private bool ConstraintTwoBoneIKConfigurado(TwoBoneIKConstraint constraint, Transform root, Transform mid, Transform tip, Transform target)
    {
        if (constraint == null)
            return false;

        if (root == null || mid == null || tip == null || target == null)
            return false;

        return constraint.data.root == root &&
               constraint.data.mid == mid &&
               constraint.data.tip == tip &&
               constraint.data.target == target &&
               constraint.data.targetPositionWeight > 0.999f &&
               constraint.data.targetRotationWeight > 0.999f;
    }

    private bool AnimationRiggingBracosPodeControlar()
    {
        if (!usarIK || !MovimentacaoDiretaDosBracos || !usarAnimationRiggingBracos)
            return false;

        if (rigBuilderAvatar == null || rigBracosVR == null)
            return false;

        if (!rigBuilderAvatar.enabled || !rigBuilderAvatar.gameObject.activeInHierarchy)
            return false;

        if (!rigBracosVR.enabled || !rigBracosVR.gameObject.activeInHierarchy || rigBracosVR.weight <= 0f)
            return false;

        bool esquerdoAtivo = ConstraintTwoBoneIKAtivo(ikBracoEsquerdo, alvoMaoEsquerda);
        bool direitoAtivo = ConstraintTwoBoneIKAtivo(ikBracoDireito, alvoMaoDireita);

        animationRiggingBracosAtivo = esquerdoAtivo && direitoAtivo;
        return animationRiggingBracosAtivo;
    }

    private bool ConstraintTwoBoneIKAtivo(TwoBoneIKConstraint constraint, Transform targetEsperado)
    {
        if (constraint == null || !constraint.enabled || !constraint.gameObject.activeInHierarchy)
            return false;

        if (constraint.weight <= 0f || targetEsperado == null)
            return false;

        if (constraint.data.target != targetEsperado)
            return false;

        return constraint.IsValid();
    }

    private void AtualizarAlvosDasMaos()
    {
        AtualizarAlvoMao(maoEsquerdaVR, alvoMaoEsquerda, offsetPosicaoMaoEsquerda, offsetRotacaoMaoEsquerda);
        AtualizarAlvoMao(maoDireitaVR, alvoMaoDireita, offsetPosicaoMaoDireita, offsetRotacaoMaoDireita);
    }

    private void AtualizarAlvosAnimationRiggingBracos(bool aplicarAcompanhamentoAntebraco)
    {
        AtualizarAlvoMaoInstantaneo(
            ObterReferenciaMaoAtual(true),
            alvoMaoEsquerda,
            offsetPosicaoMaoEsquerda,
            offsetRotacaoMaoEsquerda
        );

        AtualizarAlvoMaoInstantaneo(
            ObterReferenciaMaoAtual(false),
            alvoMaoDireita,
            offsetPosicaoMaoDireita,
            offsetRotacaoMaoDireita
        );

        if (!aplicarAcompanhamentoAntebraco)
        {
            // Fase de Update(): o Animation Rigging resolve o IK entre Update() e LateUpdate().
            // O ombro precisa ser atualizado agora, com o alvo da mão já atualizado acima,
            // para que o Two Bone IK deste frame já resolva a mão considerando a nova base do braço.
            if (!baseRotacaoOmbroCapturada)
                CapturarBaseRotacaoOmbros();

            if (usarAvancoDoOmbro)
            {
                AplicarAvancoOmbro(claviculaEsquerda, alvoMaoEsquerda, baseLocalRotacaoClaviculaEsquerda, ref anguloAtualOmbroEsquerdo, inverterAvancoOmbroEsquerdo, true);
                AplicarAvancoOmbro(claviculaDireita, alvoMaoDireita, baseLocalRotacaoClaviculaDireita, ref anguloAtualOmbroDireito, inverterAvancoOmbroDireito, false);
            }
            else
            {
                RestaurarOmbroNatural(claviculaEsquerda, baseLocalRotacaoClaviculaEsquerda, ref anguloAtualOmbroEsquerdo);
                RestaurarOmbroNatural(claviculaDireita, baseLocalRotacaoClaviculaDireita, ref anguloAtualOmbroDireito);
            }
        }

        AtualizarStretchParaAnimationRigging(true);
        AtualizarStretchParaAnimationRigging(false);
        AtualizarAlvoCotoveloParaRigging(true);
        AtualizarAlvoCotoveloParaRigging(false);

        if (aplicarAcompanhamentoAntebraco)
        {
            AplicarAcompanhamentoAntebracoComMao(
                bracoEsquerdoInferior,
                maoEsquerdaOsso,
                alvoMaoEsquerda != null ? alvoMaoEsquerda.rotation : maoEsquerdaOsso != null ? maoEsquerdaOsso.rotation : Quaternion.identity,
                true
            );

            AplicarAcompanhamentoAntebracoComMao(
                bracoDireitoInferior,
                maoDireitaOsso,
                alvoMaoDireita != null ? alvoMaoDireita.rotation : maoDireitaOsso != null ? maoDireitaOsso.rotation : Quaternion.identity,
                false
            );
        }

        if (rigBuilderAvatar != null)
            rigBuilderAvatar.SyncLayers();
    }

    private void AtualizarAlvoMaoInstantaneo(Transform controle, Transform alvo, Vector3 offsetPosicao, Vector3 offsetRotacaoEuler)
    {
        if (alvo == null || !TentarObterPoseMao(controle, offsetPosicao, offsetRotacaoEuler, out Vector3 posicaoAlvo, out Quaternion rotacaoAlvo))
            return;

        alvo.SetPositionAndRotation(posicaoAlvo, rotacaoAlvo);
    }

    private void AtualizarStretchParaAnimationRigging(bool esquerdo)
    {
        Transform bracoSuperior = esquerdo ? bracoEsquerdoSuperior : bracoDireitoSuperior;
        Transform anteBraco = esquerdo ? bracoEsquerdoInferior : bracoDireitoInferior;
        Transform maoOsso = esquerdo ? maoEsquerdaOsso : maoDireitaOsso;
        Transform alvoMao = esquerdo ? alvoMaoEsquerda : alvoMaoDireita;

        if (bracoSuperior == null || anteBraco == null || alvoMao == null)
            return;

        GarantirDadosNaturaisBracos();

        float comprimentoBraco = ObterComprimentoNaturalBracoSuperior(bracoSuperior, anteBraco);
        float comprimentoAnteBraco = ObterComprimentoNaturalAnteBraco(anteBraco, maoOsso, alvoMao.position);
        float fatorStretch = CalcularFatorStretch(bracoSuperior.position, alvoMao.position, comprimentoBraco, comprimentoAnteBraco);

        AplicarStretchVisual(bracoSuperior, anteBraco, fatorStretch);
    }

    private void AtualizarAlvoMao(Transform controle, Transform alvo, Vector3 offsetPosicao, Vector3 offsetRotacaoEuler)
    {
        if (alvo == null || !TentarObterPoseMao(controle, offsetPosicao, offsetRotacaoEuler, out Vector3 posicaoAlvo, out Quaternion rotacaoAlvo))
            return;

        alvo.position = Vector3.Lerp(alvo.position, posicaoAlvo, FatorSuavizacao(suavidadeMao));
        alvo.rotation = Quaternion.Slerp(alvo.rotation, rotacaoAlvo, FatorSuavizacao(suavidadeRotacaoMao));
    }

    private bool TentarObterPoseMao(Transform controle, Vector3 offsetPosicao, Vector3 offsetRotacao, out Vector3 posicaoAlvo, out Quaternion rotacaoAlvo)
    {
        posicaoAlvo = Vector3.zero;
        rotacaoAlvo = Quaternion.identity;

        if (controle == null)
            return false;

        posicaoAlvo = offsetPosicao.sqrMagnitude > 0.0000001f
            ? controle.TransformPoint(offsetPosicao)
            : controle.position;
        rotacaoAlvo = controle.rotation * Quaternion.Euler(offsetRotacao);
        return true;
    }

    private void AtualizarMovimentacaoDiretaDosBracos()
    {
        AtualizarBracoDireto(
            bracoEsquerdoSuperior,
            bracoEsquerdoInferior,
            maoEsquerdaOsso,
            maoEsquerdaVR,
            ref alvoMaoEsquerda,
            ref alvoCotoveloEsquerdo,
            offsetPosicaoMaoEsquerda,
            offsetRotacaoMaoEsquerda,
            pesoBraco,
            pesoAnteBraco,
            pesoMao,
            true
        );

        AtualizarBracoDireto(
            bracoDireitoSuperior,
            bracoDireitoInferior,
            maoDireitaOsso,
            maoDireitaVR,
            ref alvoMaoDireita,
            ref alvoCotoveloDireito,
            offsetPosicaoMaoDireita,
            offsetRotacaoMaoDireita,
            pesoBraco,
            pesoAnteBraco,
            pesoMao,
            false
        );
    }

    private void AtualizarBracoDireto(
        Transform bracoSuperior,
        Transform anteBraco,
        Transform maoOsso,
        Transform controleVR,
        ref Transform alvoMao,
        ref Transform alvoCotovelo,
        Vector3 offsetPosicaoMao,
        Vector3 offsetRotacaoMao,
        float pesoBraco,
        float pesoAnteBraco,
        float pesoMao,
        bool esquerdo
    )
    {
        bool twoBoneIKAtivo = TentarSincronizarComTwoBoneIKAtivo(bracoSuperior, anteBraco, maoOsso, ref alvoMao, ref alvoCotovelo);

        if (!TentarObterPoseMao(controleVR, offsetPosicaoMao, offsetRotacaoMao, out Vector3 posicaoMaoAlvo, out Quaternion rotacaoMaoAlvo))
            return;

        if (alvoMao != null)
        {
            alvoMao.position = posicaoMaoAlvo;
            alvoMao.rotation = rotacaoMaoAlvo;
        }

        Vector3 destinoMao = alvoMao != null ? alvoMao.position : posicaoMaoAlvo;

        if (twoBoneIKAtivo)
        {
            RestaurarStretchNatural(bracoSuperior, anteBraco);
            return;
        }

        if (bracoSuperior != null && anteBraco != null)
        {
            GarantirDadosNaturaisBracos();
            float comprimentoBraco = ObterComprimentoNaturalBracoSuperior(bracoSuperior, anteBraco);
            float comprimentoAnteBraco = ObterComprimentoNaturalAnteBraco(anteBraco, maoOsso, destinoMao);
            float fatorStretch = CalcularFatorStretch(bracoSuperior.position, destinoMao, comprimentoBraco, comprimentoAnteBraco);

            AplicarStretchVisual(bracoSuperior, anteBraco, fatorStretch);

            float comprimentoBracoEsticado = comprimentoBraco * fatorStretch;
            float comprimentoAnteBracoEsticado = comprimentoAnteBraco * fatorStretch;

            for (int i = 0; i < 2; i++)
            {
                Vector3 posicaoCotovelo = CalcularPosicaoCotoveloEstavel(bracoSuperior, anteBraco, alvoCotovelo, destinoMao, comprimentoBracoEsticado, comprimentoAnteBracoEsticado);

                bool bracoAlinhado = AplicarRotacaoParaAlinharFilho(bracoSuperior, anteBraco, posicaoCotovelo, pesoBraco);
                bool antebracoAlinhado = AplicarRotacaoParaAlinharFilho(anteBraco, maoOsso, destinoMao, pesoAnteBraco);

                if (!bracoAlinhado)
                    AplicarLookRotationDireto(bracoSuperior, posicaoCotovelo - bracoSuperior.position, pesoBraco);

                if (!antebracoAlinhado)
                    AplicarLookRotationDireto(anteBraco, destinoMao - anteBraco.position, pesoAnteBraco);
            }

            AplicarRotacaoMaoNoControle(maoOsso, rotacaoMaoAlvo, pesoMao);
            AplicarAcompanhamentoAntebracoComMao(anteBraco, maoOsso, rotacaoMaoAlvo, esquerdo);
        }
        else if (anteBraco != null)
        {
            RestaurarStretchNatural(bracoSuperior, anteBraco);
            if (!AplicarRotacaoParaAlinharFilho(anteBraco, maoOsso, destinoMao, pesoAnteBraco))
                AplicarLookRotationDireto(anteBraco, destinoMao - anteBraco.position, pesoAnteBraco);

            AplicarRotacaoMaoNoControle(maoOsso, rotacaoMaoAlvo, pesoMao);
            AplicarAcompanhamentoAntebracoComMao(anteBraco, maoOsso, rotacaoMaoAlvo, esquerdo);
        }
    }

    private Vector3 CalcularPosicaoCotoveloEstavel(Transform bracoSuperior, Transform anteBraco, Transform alvoCotovelo, Vector3 posicaoMaoAlvo, float comprimentoBraco, float comprimentoAnteBraco)
    {
        Vector3 posicaoOmbro = bracoSuperior.position;
        Vector3 direcaoOmbroMao = posicaoMaoAlvo - posicaoOmbro;
        float distanciaOmbroMao = direcaoOmbroMao.magnitude;

        if (distanciaOmbroMao < 0.0001f)
            return anteBraco.position;

        if (comprimentoBraco < 0.0001f)
            comprimentoBraco = distanciaOmbroMao * 0.5f;

        if (comprimentoAnteBraco < 0.0001f)
            comprimentoAnteBraco = distanciaOmbroMao * 0.5f;

        Vector3 direcaoNormalizada = direcaoOmbroMao / distanciaOmbroMao;
        float alcanceMaximo = Mathf.Max(0.0001f, comprimentoBraco + comprimentoAnteBraco - 0.0001f);
        float alcanceMinimo = Mathf.Max(0.0001f, Mathf.Abs(comprimentoBraco - comprimentoAnteBraco) + 0.0001f);
        float distanciaCalculo = Mathf.Clamp(distanciaOmbroMao, alcanceMinimo, alcanceMaximo);

        Vector3 direcaoDobra = ObterDirecaoDobraCotovelo(posicaoOmbro, direcaoNormalizada, anteBraco, alvoCotovelo);
        float distanciaAoLongo = (comprimentoBraco * comprimentoBraco - comprimentoAnteBraco * comprimentoAnteBraco + distanciaCalculo * distanciaCalculo) / (2f * distanciaCalculo);
        float distanciaLateral = Mathf.Sqrt(Mathf.Max(0f, comprimentoBraco * comprimentoBraco - distanciaAoLongo * distanciaAoLongo));

        return posicaoOmbro + direcaoNormalizada * distanciaAoLongo + direcaoDobra * distanciaLateral;
    }

    private Vector3 ObterDirecaoDobraCotovelo(Vector3 posicaoOmbro, Vector3 direcaoOmbroMao, Transform anteBraco, Transform alvoCotovelo)
    {
        Vector3 direcaoHint = Vector3.zero;

        if (alvoCotovelo != null)
            direcaoHint = alvoCotovelo.position - posicaoOmbro;

        direcaoHint = Vector3.ProjectOnPlane(direcaoHint, direcaoOmbroMao);

        if (direcaoHint.sqrMagnitude < 0.0001f && anteBraco != null)
            direcaoHint = Vector3.ProjectOnPlane(anteBraco.position - posicaoOmbro, direcaoOmbroMao);

        if (direcaoHint.sqrMagnitude < 0.0001f)
            direcaoHint = Vector3.ProjectOnPlane(ObterDireitaCorpo(), direcaoOmbroMao);

        if (direcaoHint.sqrMagnitude < 0.0001f)
            direcaoHint = Vector3.ProjectOnPlane(raizAvatar != null ? raizAvatar.up : transform.up, direcaoOmbroMao);

        return direcaoHint.sqrMagnitude > 0.0001f ? direcaoHint.normalized : Vector3.up;
    }

    private bool TentarSincronizarComTwoBoneIKAtivo(Transform bracoSuperior, Transform anteBraco, Transform maoOsso, ref Transform alvoMao, ref Transform alvoCotovelo)
    {
        if (!AnimationRiggingEstaDisponivel())
            return false;

        Transform raizBusca = raizAvatar != null ? raizAvatar : transform;
        Component[] componentes = raizBusca.GetComponentsInChildren<Component>(true);

        for (int i = 0; i < componentes.Length; i++)
        {
            Component componente = componentes[i];
            if (!ComponenteTwoBoneIKAtivo(componente))
                continue;

            object dados = ObterValorMembro(componente, "data") ?? ObterValorMembro(componente, "m_Data");
            Transform root = ObterTransformMembro(dados, "root") ?? ObterTransformMembro(dados, "m_Root");
            Transform mid = ObterTransformMembro(dados, "mid") ?? ObterTransformMembro(dados, "m_Mid");
            Transform tip = ObterTransformMembro(dados, "tip") ?? ObterTransformMembro(dados, "m_Tip");
            Transform target = ObterTransformMembro(dados, "target") ?? ObterTransformMembro(dados, "m_Target");
            Transform hint = ObterTransformMembro(dados, "hint") ?? ObterTransformMembro(dados, "m_Hint");

            if (!ConstraintPertenceAoBraco(root, mid, tip, target, bracoSuperior, anteBraco, maoOsso, alvoMao))
                continue;

            if (target == null)
                continue;

            alvoMao = target;

            if (hint != null)
                alvoCotovelo = hint;

            return true;
        }

        return false;
    }

    private bool ComponenteTwoBoneIKAtivo(Component componente)
    {
        if (componente == null)
            return false;

        Type tipo = componente.GetType();
        if (tipo.FullName != "UnityEngine.Animations.Rigging.TwoBoneIKConstraint" &&
            tipo.Name != "TwoBoneIKConstraint")
        {
            return false;
        }

        if (componente is Behaviour behaviour && (!behaviour.enabled || !behaviour.gameObject.activeInHierarchy))
            return false;

        float pesoConstraint = ObterFloatMembro(componente, "weight", 1f);
        return pesoConstraint > 0f && RigAnimationRiggingAtivo(componente.transform);
    }

    private bool RigAnimationRiggingAtivo(Transform origem)
    {
        bool rigAtivo = false;
        bool rigBuilderAtivo = false;

        Transform atual = origem;
        while (atual != null)
        {
            Component[] componentes = atual.GetComponents<Component>();
            for (int i = 0; i < componentes.Length; i++)
            {
                Component componente = componentes[i];
                if (componente == null)
                    continue;

                Type tipo = componente.GetType();
                bool componenteAtivo = !(componente is Behaviour behaviour) || (behaviour.enabled && behaviour.gameObject.activeInHierarchy);
                float peso = ObterFloatMembro(componente, "weight", 1f);

                if (tipo.FullName == "UnityEngine.Animations.Rigging.Rig" || tipo.Name == "Rig")
                    rigAtivo |= componenteAtivo && peso > 0f;

                if (tipo.FullName == "UnityEngine.Animations.Rigging.RigBuilder" || tipo.Name == "RigBuilder")
                    rigBuilderAtivo |= componenteAtivo;
            }

            atual = atual.parent;
        }

        return rigAtivo && rigBuilderAtivo;
    }

    private bool ConstraintPertenceAoBraco(Transform root, Transform mid, Transform tip, Transform target, Transform bracoSuperior, Transform anteBraco, Transform maoOsso, Transform alvoMao)
    {
        if (tip != null && maoOsso != null && tip == maoOsso)
            return true;

        if (mid != null && anteBraco != null && mid == anteBraco)
            return true;

        if (root != null && bracoSuperior != null && root == bracoSuperior)
            return true;

        if (target != null && alvoMao != null && target == alvoMao)
            return true;

        return false;
    }

    private object ObterValorMembro(object objeto, string nome)
    {
        if (objeto == null || string.IsNullOrEmpty(nome))
            return null;

        Type tipo = objeto.GetType();
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;

        System.Reflection.PropertyInfo propriedade = tipo.GetProperty(nome, flags);
        if (propriedade != null)
            return propriedade.GetValue(objeto, null);

        System.Reflection.FieldInfo campo = tipo.GetField(nome, flags);
        return campo != null ? campo.GetValue(objeto) : null;
    }

    private Transform ObterTransformMembro(object objeto, string nome)
    {
        return ObterValorMembro(objeto, nome) as Transform;
    }

    private float ObterFloatMembro(object objeto, string nome, float valorPadrao)
    {
        object valor = ObterValorMembro(objeto, nome);
        return valor is float numero ? numero : valorPadrao;
    }

    private void GarantirDadosNaturaisBracos()
    {
        if (!dadosNaturaisBracosCapturados ||
            comprimentoNaturalBracoEsquerdoSuperior <= 0f ||
            comprimentoNaturalBracoEsquerdoInferior <= 0f ||
            comprimentoNaturalBracoDireitoSuperior <= 0f ||
            comprimentoNaturalBracoDireitoInferior <= 0f)
        {
            CapturarDadosNaturaisBracos();
        }
    }

    private void CapturarDadosNaturaisBracos()
    {
        escalaNaturalBracoEsquerdoSuperior = bracoEsquerdoSuperior != null ? bracoEsquerdoSuperior.localScale : Vector3.one;
        escalaNaturalBracoEsquerdoInferior = bracoEsquerdoInferior != null ? bracoEsquerdoInferior.localScale : Vector3.one;
        escalaNaturalBracoDireitoSuperior = bracoDireitoSuperior != null ? bracoDireitoSuperior.localScale : Vector3.one;
        escalaNaturalBracoDireitoInferior = bracoDireitoInferior != null ? bracoDireitoInferior.localScale : Vector3.one;

        comprimentoNaturalBracoEsquerdoSuperior = MedirComprimentoOsso(bracoEsquerdoSuperior, bracoEsquerdoInferior);
        comprimentoNaturalBracoEsquerdoInferior = MedirComprimentoOsso(bracoEsquerdoInferior, maoEsquerdaOsso);
        comprimentoNaturalBracoDireitoSuperior = MedirComprimentoOsso(bracoDireitoSuperior, bracoDireitoInferior);
        comprimentoNaturalBracoDireitoInferior = MedirComprimentoOsso(bracoDireitoInferior, maoDireitaOsso);

        dadosNaturaisBracosCapturados = true;
    }

    private float MedirComprimentoOsso(Transform inicio, Transform fim)
    {
        if (inicio == null || fim == null)
            return 0f;

        return Vector3.Distance(inicio.position, fim.position);
    }

    private float ObterComprimentoNaturalBracoSuperior(Transform bracoSuperior, Transform anteBraco)
    {
        if (bracoSuperior == bracoEsquerdoSuperior && comprimentoNaturalBracoEsquerdoSuperior > 0f)
            return comprimentoNaturalBracoEsquerdoSuperior;

        if (bracoSuperior == bracoDireitoSuperior && comprimentoNaturalBracoDireitoSuperior > 0f)
            return comprimentoNaturalBracoDireitoSuperior;

        return MedirComprimentoOsso(bracoSuperior, anteBraco);
    }

    private float ObterComprimentoNaturalAnteBraco(Transform anteBraco, Transform maoOsso, Vector3 destinoMao)
    {
        if (anteBraco == bracoEsquerdoInferior && comprimentoNaturalBracoEsquerdoInferior > 0f)
            return comprimentoNaturalBracoEsquerdoInferior;

        if (anteBraco == bracoDireitoInferior && comprimentoNaturalBracoDireitoInferior > 0f)
            return comprimentoNaturalBracoDireitoInferior;

        if (anteBraco != null && maoOsso != null)
            return Vector3.Distance(anteBraco.position, maoOsso.position);

        return anteBraco != null ? Vector3.Distance(anteBraco.position, destinoMao) : 0f;
    }

    private float CalcularFatorStretch(Vector3 posicaoOmbro, Vector3 destinoMao, float comprimentoBraco, float comprimentoAnteBraco)
    {
        if (!permitirStretch)
            return 1f;

        float comprimentoNaturalTotal = comprimentoBraco + comprimentoAnteBraco;
        if (comprimentoNaturalTotal <= 0.0001f)
            return 1f;

        float distanciaOmbroMao = Vector3.Distance(posicaoOmbro, destinoMao);
        if (distanciaOmbroMao <= comprimentoNaturalTotal)
            return 1f;

        return Mathf.Clamp(distanciaOmbroMao / comprimentoNaturalTotal, 1f, limiteStretch);
    }

    private void AplicarStretchVisual(Transform bracoSuperior, Transform anteBraco, float fatorStretch)
    {
        fatorStretch = Mathf.Max(1f, fatorStretch);
        AplicarStretchNoOsso(bracoSuperior, ObterEscalaNatural(bracoSuperior), fatorStretch);
        AplicarStretchNoOsso(anteBraco, ObterEscalaNatural(anteBraco), fatorStretch);
    }

    private void RestaurarStretchNatural(Transform bracoSuperior, Transform anteBraco)
    {
        AplicarStretchNoOsso(bracoSuperior, ObterEscalaNatural(bracoSuperior), 1f);
        AplicarStretchNoOsso(anteBraco, ObterEscalaNatural(anteBraco), 1f);
    }

    private Vector3 ObterEscalaNatural(Transform osso)
    {
        if (osso == bracoEsquerdoSuperior)
            return escalaNaturalBracoEsquerdoSuperior;

        if (osso == bracoEsquerdoInferior)
            return escalaNaturalBracoEsquerdoInferior;

        if (osso == bracoDireitoSuperior)
            return escalaNaturalBracoDireitoSuperior;

        if (osso == bracoDireitoInferior)
            return escalaNaturalBracoDireitoInferior;

        return osso != null ? osso.localScale : Vector3.one;
    }

    private void AplicarStretchNoOsso(Transform osso, Vector3 escalaBase, float fatorStretch)
    {
        if (osso == null)
            return;

        Vector3 escala = escalaBase;

        switch (ObterIndiceEixoComprimentoLocal(osso))
        {
            case 0:
                escala.x *= fatorStretch;
                break;
            case 1:
                escala.y *= fatorStretch;
                break;
            default:
                escala.z *= fatorStretch;
                break;
        }

        osso.localScale = escala;
    }

    private int ObterIndiceEixoComprimentoLocal(Transform osso)
    {
        Transform filhoComprimento = ObterFilhoComprimentoOsso(osso);
        if (osso == null || filhoComprimento == null)
            return 2;

        Vector3 direcaoLocal = filhoComprimento.parent == osso
            ? filhoComprimento.localPosition
            : osso.InverseTransformPoint(filhoComprimento.position);

        direcaoLocal.x = Mathf.Abs(direcaoLocal.x);
        direcaoLocal.y = Mathf.Abs(direcaoLocal.y);
        direcaoLocal.z = Mathf.Abs(direcaoLocal.z);

        if (direcaoLocal.x >= direcaoLocal.y && direcaoLocal.x >= direcaoLocal.z)
            return 0;

        if (direcaoLocal.y >= direcaoLocal.x && direcaoLocal.y >= direcaoLocal.z)
            return 1;

        return 2;
    }

    private Transform ObterFilhoComprimentoOsso(Transform osso)
    {
        if (osso == bracoEsquerdoSuperior)
            return bracoEsquerdoInferior;

        if (osso == bracoEsquerdoInferior)
            return maoEsquerdaOsso;

        if (osso == bracoDireitoSuperior)
            return bracoDireitoInferior;

        if (osso == bracoDireitoInferior)
            return maoDireitaOsso;

        return null;
    }

    private bool AplicarRotacaoParaAlinharFilho(Transform osso, Transform filho, Vector3 destinoFilho, float peso)
    {
        if (osso == null || filho == null || peso <= 0f)
            return false;

        Vector3 direcaoAtual = filho.position - osso.position;
        Vector3 direcaoDesejada = destinoFilho - osso.position;

        if (direcaoAtual.sqrMagnitude < 0.0001f || direcaoDesejada.sqrMagnitude < 0.0001f)
            return false;

        Quaternion delta = Quaternion.FromToRotation(direcaoAtual.normalized, direcaoDesejada.normalized);
        Quaternion rotacaoAlvo = delta * osso.rotation;
        osso.rotation = Quaternion.Slerp(osso.rotation, rotacaoAlvo, Mathf.Clamp01(peso));
        return true;
    }

    private void AplicarRotacaoMaoNoControle(Transform maoOsso, Quaternion rotacaoAlvo, float peso)
    {
        if (maoOsso == null || peso <= 0f)
            return;

        maoOsso.rotation = Quaternion.Slerp(maoOsso.rotation, rotacaoAlvo, Mathf.Clamp01(peso));
    }

    private void CapturarBaseTwistAntebracoMao()
    {
        baseTwistAntebracoMaoEsquerda = CalcularTwistMaoRelativoAntebraco(
            bracoEsquerdoInferior,
            maoEsquerdaOsso,
            maoEsquerdaOsso != null ? maoEsquerdaOsso.rotation : Quaternion.identity,
            out _
        );

        baseTwistAntebracoMaoDireita = CalcularTwistMaoRelativoAntebraco(
            bracoDireitoInferior,
            maoDireitaOsso,
            maoDireitaOsso != null ? maoDireitaOsso.rotation : Quaternion.identity,
            out _
        );

        baseTwistAntebracoMaoCapturada = true;
    }

    private void AplicarAcompanhamentoAntebracoComMao(Transform anteBraco, Transform maoOsso, Quaternion rotacaoMaoFinal, bool esquerdo)
    {
        if (!usarAcompanhamentoAntebracoComMao || anteBraco == null || maoOsso == null)
            return;

        if (!baseTwistAntebracoMaoCapturada)
            CapturarBaseTwistAntebracoMao();

        float peso = esquerdo ? pesoAcompanhamentoAntebracoEsquerdo : pesoAcompanhamentoAntebracoDireito;
        float limite = esquerdo ? limiteAcompanhamentoAntebracoEsquerdo : limiteAcompanhamentoAntebracoDireito;

        if (peso <= 0f || limite <= 0f)
            return;

        float twistAtual = CalcularTwistMaoRelativoAntebraco(anteBraco, maoOsso, rotacaoMaoFinal, out Vector3 eixoAntebraco);

        bool eixoValido = eixoAntebraco.sqrMagnitude > 0.0001f;
        if (esquerdo)
            eixoAcompanhamentoEsquerdoValido = eixoValido;
        else
            eixoAcompanhamentoDireitoValido = eixoValido;

        if (!eixoValido)
            return;

        float baseTwist = usarPoseBaseNaturalAntebraco
            ? (esquerdo ? baseTwistAntebracoMaoEsquerda : baseTwistAntebracoMaoDireita)
            : 0f;

        float twistRelativo = NormalizarAngulo180(twistAtual - baseTwist);
        float twistAplicado = Mathf.Clamp(twistRelativo * Mathf.Clamp01(peso), -limite, limite);

        if (esquerdo ? inverterAcompanhamentoAntebracoEsquerdo : inverterAcompanhamentoAntebracoDireito)
            twistAplicado *= -1f;

        if (esquerdo)
        {
            twistMaoEsquerdaGraus = twistRelativo;
            twistAplicadoAntebracoEsquerdoGraus = twistAplicado;
        }
        else
        {
            twistMaoDireitaGraus = twistRelativo;
            twistAplicadoAntebracoDireitoGraus = twistAplicado;
        }

        if (Mathf.Abs(twistAplicado) < 0.0001f)
            return;

        Vector3 posicaoMaoFinal = maoOsso.position;
        Quaternion rotacaoMaoPreservada = rotacaoMaoFinal;

        anteBraco.rotation = Quaternion.AngleAxis(twistAplicado, eixoAntebraco) * anteBraco.rotation;

        // A mão é filha do antebraço. Depois que o antebraço acompanha a torção,
        // preservamos a pose final da mão para manter a sincronização com o controle/laser.
        maoOsso.SetPositionAndRotation(posicaoMaoFinal, rotacaoMaoPreservada);
    }

    private float CalcularTwistMaoRelativoAntebraco(Transform anteBraco, Transform maoOsso, Quaternion rotacaoMaoFinal, out Vector3 eixoAntebraco)
    {
        eixoAntebraco = Vector3.zero;

        if (anteBraco == null || maoOsso == null)
            return 0f;

        eixoAntebraco = maoOsso.position - anteBraco.position;

        if (eixoAntebraco.sqrMagnitude < 0.0001f)
            return 0f;

        eixoAntebraco.Normalize();

        Quaternion deltaMaoAntebraco = rotacaoMaoFinal * Quaternion.Inverse(anteBraco.rotation);
        Quaternion twist = ExtrairTwist(deltaMaoAntebraco, eixoAntebraco);

        twist.ToAngleAxis(out float angulo, out Vector3 eixoTwist);

        if (float.IsNaN(angulo) || eixoTwist.sqrMagnitude < 0.0001f)
            return 0f;

        if (angulo > 180f)
            angulo -= 360f;

        if (Vector3.Dot(eixoTwist.normalized, eixoAntebraco) < 0f)
            angulo *= -1f;

        return NormalizarAngulo180(angulo);
    }

    private static Quaternion ExtrairTwist(Quaternion rotacao, Vector3 eixoNormalizado)
    {
        eixoNormalizado.Normalize();

        Vector3 parteVetor = new Vector3(rotacao.x, rotacao.y, rotacao.z);
        Vector3 projecao = Vector3.Project(parteVetor, eixoNormalizado);

        Quaternion twist = new Quaternion(projecao.x, projecao.y, projecao.z, rotacao.w);
        float magnitude = Mathf.Sqrt(
            twist.x * twist.x +
            twist.y * twist.y +
            twist.z * twist.z +
            twist.w * twist.w
        );

        if (magnitude < 0.0001f)
            return Quaternion.identity;

        return new Quaternion(
            twist.x / magnitude,
            twist.y / magnitude,
            twist.z / magnitude,
            twist.w / magnitude
        );
    }

    private static float NormalizarAngulo180(float angulo)
    {
        while (angulo > 180f)
            angulo -= 360f;

        while (angulo < -180f)
            angulo += 360f;

        return angulo;
    }

    private void CapturarBaseRotacaoOmbros()
    {
        baseLocalRotacaoClaviculaEsquerda = claviculaEsquerda != null ? claviculaEsquerda.localRotation : Quaternion.identity;
        baseLocalRotacaoClaviculaDireita = claviculaDireita != null ? claviculaDireita.localRotation : Quaternion.identity;
        baseRotacaoOmbroCapturada = true;
    }

    private Vector3 ObterEixoLocalAvancoOmbro()
    {
        switch (eixoLocalAvancoOmbro)
        {
            case EixoLocalAvancoOmbro.X:
                return Vector3.right;
            case EixoLocalAvancoOmbro.Y:
                return Vector3.up;
            default:
                return Vector3.forward;
        }
    }

    // Calcula e aplica um pequeno avanço do ombro (clavícula) no eixo local selecionado, só quando a mão
    // está claramente à frente do peito. Isso roda ANTES do Two Bone IK resolver o frame
    // (chamado a partir de Update, na fase "false" de AtualizarAlvosAnimationRiggingBracos),
    // então o IK sempre recalcula a mão em cima da nova posição do ombro e a mão nunca perde
    // sincronia com o controle: o ombro só empurra a base do braço, quem manda na posição final
    // da mão continua sendo o alvo/target do controle VR.
    private void AplicarAvancoOmbro(Transform clavicula, Transform alvoMao, Quaternion baseLocal, ref float anguloAtual, bool inverter, bool esquerdo)
    {
        if (clavicula == null || alvoMao == null)
        {
            anguloAtual = Mathf.Lerp(anguloAtual, 0f, FatorSuavizacao(suavidadeAvancoOmbro));
            return;
        }

        // Referência sempre a clavícula/ombro real, nunca o braço superior: o braço superior é a
        // raiz (root) do Two Bone IK, e usá-lo como substituto deixaria o IK ainda mais instável.
        Vector3 direcaoOmbroMao = alvoMao.position - clavicula.position;

        Vector3 frente = ObterFrenteCorpo();
        Vector3 direita = ObterDireitaCorpo();

        float avancoFrontal = Vector3.Dot(direcaoOmbroMao, frente);
        float lateral = Mathf.Abs(Vector3.Dot(direcaoOmbroMao, direita));

        float distanciaMaxima = Mathf.Max(distanciaAvancoOmbroMaximo, limiarAvancoOmbro + 0.01f);

        // 0 quando a mão ainda está perto do ombro/atrás dele, 1 quando já avançou bastante à frente.
        float alpha = Mathf.InverseLerp(limiarAvancoOmbro, distanciaMaxima, avancoFrontal);
        alpha = Mathf.Clamp01(alpha);

        // Reduz o avanço do ombro quando o movimento é principalmente lateral, para não abrir
        // o ombro quando a mão vai para o lado (só empurra quando a mão vai para frente do corpo).
        float fatorFrontalidade = Mathf.Clamp01(1f - (lateral / distanciaMaxima));
        alpha *= fatorFrontalidade;

        float anguloAlvo = alpha * anguloMaximoAvancoOmbro;
        if (inverter)
            anguloAlvo *= -1f;

        // Trava de segurança pública: nunca deixa o ângulo alvo passar do limite configurado,
        // mesmo que anguloMaximoAvancoOmbro esteja configurado com um valor alto.
        anguloAlvo = Mathf.Clamp(anguloAlvo, limiteMinimoOmbro, limiteMaximoOmbro);

        anguloAtual = Mathf.Lerp(anguloAtual, anguloAlvo, FatorSuavizacao(suavidadeAvancoOmbro));

        // Trava final: garante que o ângulo realmente aplicado no osso nunca ultrapasse o limite,
        // mesmo durante a suavização (evita qualquer chance de o ombro "dar a volta" sozinho).
        anguloAtual = Mathf.Clamp(anguloAtual, limiteMinimoOmbro, limiteMaximoOmbro);

        clavicula.localRotation = baseLocal * Quaternion.AngleAxis(anguloAtual, ObterEixoLocalAvancoOmbro());

        if (esquerdo)
            avancoOmbroEsquerdoGraus = anguloAtual;
        else
            avancoOmbroDireitoGraus = anguloAtual;
    }

    // Devolve o ombro suavemente para a pose natural quando o avanço está desligado ou quando
    // o Two Bone IK dos braços não está no controle (evita o ombro ficar "preso" rotacionado).
    private void RestaurarOmbroNatural(Transform clavicula, Quaternion baseLocal, ref float anguloAtual)
    {
        if (clavicula == null)
            return;

        if (Mathf.Abs(anguloAtual) < 0.001f)
        {
            anguloAtual = 0f;
            return;
        }

        anguloAtual = Mathf.Lerp(anguloAtual, 0f, FatorSuavizacao(suavidadeAvancoOmbro));

        if (Mathf.Abs(anguloAtual) < 0.01f)
            anguloAtual = 0f;

        clavicula.localRotation = baseLocal * Quaternion.AngleAxis(anguloAtual, ObterEixoLocalAvancoOmbro());
    }

    private void AplicarLookRotationDireto(Transform osso, Vector3 direcao, float peso)
    {
        if (osso == null || peso <= 0f || direcao.sqrMagnitude < 0.0001f)
            return;

        Vector3 up = raizAvatar != null ? raizAvatar.up : transform.up;
        if (up.sqrMagnitude < 0.0001f)
            up = Vector3.up;

        Vector3 direcaoNormalizada = direcao.normalized;
        up = up.normalized;

        if (Mathf.Abs(Vector3.Dot(direcaoNormalizada, up)) > 0.995f)
        {
            up = Vector3.Cross(direcaoNormalizada, transform.right);
            if (up.sqrMagnitude < 0.0001f)
                up = Vector3.Cross(direcaoNormalizada, transform.forward);
            if (up.sqrMagnitude < 0.0001f)
                up = Vector3.up;
            up.Normalize();
        }

        Quaternion rotacaoAlvo = Quaternion.LookRotation(direcaoNormalizada, up);
        osso.rotation = Quaternion.Slerp(osso.rotation, rotacaoAlvo, Mathf.Clamp01(peso));
    }

    private void AtualizarAlvosDosCotovelos()
    {
        AtualizarAlvoCotovelo(true);
        AtualizarAlvoCotovelo(false);
    }

    private void AtualizarAlvoCotoveloParaRigging(bool esquerdo)
    {
        AtualizarAlvoCotovelo(esquerdo);
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
        AtribuirPorNome(ref claviculaEsquerda, sobrescrever, ossos, "clavicula esquerda", "clavícula esquerda", "ombro esquerdo", "ombro_l", "ombrol", "leftshoulder", "leftclavicle");
        AtribuirPorNome(ref claviculaDireita, sobrescrever, ossos, "clavicula direita", "clavícula direita", "ombro direito", "ombro_r", "ombror", "rightshoulder", "rightclavicle");
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
        pesoBraco = Mathf.Clamp01(pesoBraco);
        pesoAnteBraco = Mathf.Clamp01(pesoAnteBraco);
        pesoMao = Mathf.Clamp01(pesoMao);
        pesoAnimationRiggingBracos = Mathf.Clamp01(pesoAnimationRiggingBracos);
        pesoAcompanhamentoAntebracoDireito = Mathf.Clamp01(pesoAcompanhamentoAntebracoDireito);
        pesoAcompanhamentoAntebracoEsquerdo = Mathf.Clamp01(pesoAcompanhamentoAntebracoEsquerdo);
        limiteAcompanhamentoAntebracoDireito = Mathf.Clamp(limiteAcompanhamentoAntebracoDireito, 0f, 120f);
        limiteAcompanhamentoAntebracoEsquerdo = Mathf.Clamp(limiteAcompanhamentoAntebracoEsquerdo, 0f, 120f);
        limiteStretch = Mathf.Max(1f, limiteStretch);
        limiarAvancoOmbro = Mathf.Max(0f, limiarAvancoOmbro);
        distanciaAvancoOmbroMaximo = Mathf.Max(limiarAvancoOmbro + 0.01f, distanciaAvancoOmbroMaximo);
        suavidadeAvancoOmbro = Mathf.Max(0f, suavidadeAvancoOmbro);

        // Garante que o limite mínimo nunca fique maior que o máximo (evita inverter o clamp por engano).
        limiteMaximoOmbro = Mathf.Clamp(limiteMaximoOmbro, 0f, 179f);
        limiteMinimoOmbro = Mathf.Clamp(limiteMinimoOmbro, -179f, 0f);

        // O ângulo máximo de avanço não pode passar do limite de segurança público.
        anguloMaximoAvancoOmbro = Mathf.Clamp(anguloMaximoAvancoOmbro, 0f, limiteMaximoOmbro);
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
        bool movimentacaoDiretaConfigurada = !MovimentacaoDiretaDosBracos ||
                                             (maoEsquerdaVR != null &&
                                              maoDireitaVR != null &&
                                              bracoEsquerdoSuperior != null &&
                                              bracoEsquerdoInferior != null &&
                                              maoEsquerdaOsso != null &&
                                              bracoDireitoSuperior != null &&
                                              bracoDireitoInferior != null &&
                                              maoDireitaOsso != null);
        bool cabecaEncontrada = ossoCabeca != null;
        bool bracoEsquerdoEncontrado = bracoEsquerdoSuperior != null;
        bool antebracoEsquerdoEncontrado = bracoEsquerdoInferior != null;
        bool maoEsquerdaEncontrada = maoEsquerdaOsso != null;
        bool bracoDireitoEncontrado = bracoDireitoSuperior != null;
        bool antebracoDireitoEncontrado = bracoDireitoInferior != null;
        bool maoDireitaEncontrada = maoDireitaOsso != null;
        bool targetsEncontrados = alvoMaoEsquerda != null && alvoMaoDireita != null;
        bool hintsEncontrados = alvoCotoveloEsquerdo != null && alvoCotoveloDireito != null;
        bool referenciasMaoVREncontradas = (referenciaMaoEsquerdaVR != null || maoEsquerdaVR != null) &&
                                           (referenciaMaoDireitaVR != null || maoDireitaVR != null);
        bool rigBuilderConfigurado = rigBuilderAvatar != null && rigBracosVR != null;
        bool twoBoneEsquerdoConfigurado = ConstraintTwoBoneIKConfigurado(
            ikBracoEsquerdo,
            bracoEsquerdoSuperior,
            bracoEsquerdoInferior,
            maoEsquerdaOsso,
            alvoMaoEsquerda
        );
        bool twoBoneDireitoConfigurado = ConstraintTwoBoneIKConfigurado(
            ikBracoDireito,
            bracoDireitoSuperior,
            bracoDireitoInferior,
            maoDireitaOsso,
            alvoMaoDireita
        );
        bool ombrosEncontrados = claviculaEsquerda != null && claviculaDireita != null;

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
        status.AppendLine(LinhaStatus("Movimentacao direta dos bracos configurada", movimentacaoDiretaConfigurada));
        status.AppendLine("\u2714 IK Pass: verifica\u00e7\u00e3o manual necess\u00e1ria na Base Layer do Animator.");
        status.AppendLine(LinhaStatus("Cabe\u00e7a encontrada", cabecaEncontrada));
        status.AppendLine(LinhaStatus("Bra\u00e7o esquerdo encontrado", bracoEsquerdoEncontrado));
        status.AppendLine(LinhaStatus("Antebra\u00e7o esquerdo encontrado", antebracoEsquerdoEncontrado));
        status.AppendLine(LinhaStatus("M\u00e3o esquerda encontrada", maoEsquerdaEncontrada));
        status.AppendLine(LinhaStatus("Bra\u00e7o direito encontrado", bracoDireitoEncontrado));
        status.AppendLine(LinhaStatus("Antebra\u00e7o direito encontrado", antebracoDireitoEncontrado));
        status.AppendLine(LinhaStatus("M\u00e3o direita encontrada", maoDireitaEncontrada));
        status.AppendLine(LinhaStatus("Ombros/clav\u00edculas encontrados", ombrosEncontrados));
        status.AppendLine(LinhaStatus("Targets encontrados", targetsEncontrados));
        status.AppendLine(LinhaStatus("Hints encontrados", hintsEncontrados));
        status.AppendLine(LinhaStatus("Animation Rigging instalado", animationRiggingDisponivel));
        status.AppendLine(LinhaStatus("Referencias finais das maos VR encontradas", referenciasMaoVREncontradas));
        status.AppendLine(LinhaStatus("RigBuilder/Rig dos bracos configurado", rigBuilderConfigurado));
        status.AppendLine(LinhaStatus("Two Bone IK esquerdo configurado", twoBoneEsquerdoConfigurado));
        status.AppendLine(LinhaStatus("Two Bone IK direito configurado", twoBoneDireitoConfigurado));
        status.AppendLine(LinhaStatus("Acompanhamento antebraço/mão ativo", usarAcompanhamentoAntebracoComMao));
        status.AppendLine($"  Peso acompanhamento direito: {pesoAcompanhamentoAntebracoDireito:0.00} | esquerdo: {pesoAcompanhamentoAntebracoEsquerdo:0.00}");
        status.AppendLine($"  Twist mão direita: {twistMaoDireitaGraus:0.0}° -> antebraço {twistAplicadoAntebracoDireitoGraus:0.0}° | eixo válido: {eixoAcompanhamentoDireitoValido}");
        status.AppendLine($"  Twist mão esquerda: {twistMaoEsquerdaGraus:0.0}° -> antebraço {twistAplicadoAntebracoEsquerdoGraus:0.0}° | eixo válido: {eixoAcompanhamentoEsquerdoValido}");
        status.AppendLine(LinhaStatus("Avanço do ombro ativo", usarAvancoDoOmbro));
        status.AppendLine($"  Eixo local avanco ombro: {eixoLocalAvancoOmbro}");
        status.AppendLine($"  Ângulo máximo: {anguloMaximoAvancoOmbro:0.0}° | limiar: {limiarAvancoOmbro:0.00}m | máximo em: {distanciaAvancoOmbroMaximo:0.00}m");
        status.AppendLine($"  Limite público de segurança: {limiteMinimoOmbro:0.0}° a {limiteMaximoOmbro:0.0}°");
        status.AppendLine($"  Ombro direito atual: {avancoOmbroDireitoGraus:0.0}° | esquerdo atual: {avancoOmbroEsquerdoGraus:0.0}°");
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

        if (usarAvancoDoOmbro && !ombrosEncontrados)
            status.AppendLine("Avanço do ombro está ativo, mas os ossos de clavícula/ombro não foram encontrados. O avanço não terá efeito até que Ombro Esquerdo/Direito sejam preenchidos.");

        if (MovimentacaoDiretaDosBracos && animationRiggingBracosAtivo)
            status.AppendLine("Movimentacao direta dos bracos ativa com Animation Rigging: os targets seguem os controles VR e o Two Bone IK move os bracos. O fallback manual fica desligado.");
        else if (MovimentacaoDiretaDosBracos && usarAnimationRiggingBracos && (!twoBoneEsquerdoConfigurado || !twoBoneDireitoConfigurado))
            status.AppendLine("Movimentacao direta dos bracos ativa, mas o Two Bone IK ainda nao esta totalmente configurado. O script usara o fallback manual por rotacao dos bones.");
        else if (MovimentacaoDiretaDosBracos)
            status.AppendLine("Movimentacao direta dos bracos ativa: usando fallback manual por rotacao dos bones.");
        else if (!animationRiggingDisponivel && usarAnimatorIKHumanoidComoFallback)
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
        return true;
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
