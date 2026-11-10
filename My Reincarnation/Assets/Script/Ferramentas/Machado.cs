using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class Machado : MonoBehaviour
{
    [Serializable]
    private class ClasseMachado : AtributosManoplas.ConfiguracaoClasse
    {
        [Min(0)] public int ataqueMinimo;
        [Min(0)] public int ataqueMaximo;
        public TMP_Text textoAtaque;
        [Min(1)] public int durabilidadeMinima;
        [Min(1)] public int durabilidadeMaxima;
        public TMP_Text textoDurabilidadeAtual;
        public TMP_Text textoDurabilidadeMaxima;
    }

    [Serializable]
    internal class EstadoSorteio
    {
        internal const string Formato = "my-reincarnation.machado.v1";
        public string formato = Formato;
        public string anteriores;
        public ClasseManopla classe;
        public int ataque;
        public int durabilidadeMaxima;
        public List<AtributosManoplas.ValorAtributo> atributos;
    }

    [SerializeField] private ClasseMachado[] classes =
    {
        CriarClassePadrao(ClasseManopla.Comum, 85),
        CriarClassePadrao(ClasseManopla.Incomum, 12),
        CriarClassePadrao(ClasseManopla.Raro, 3),
        CriarClassePadrao(ClasseManopla.Epico),
        CriarClassePadrao(ClasseManopla.Unico),
        CriarClassePadrao(ClasseManopla.Lendario),
        CriarClassePadrao(ClasseManopla.Mitico),
        CriarClassePadrao(ClasseManopla.Divino)
    };

    private static ClasseMachado CriarClassePadrao(ClasseManopla classe, float chance = 0)
    {
        var configuracao = new ClasseMachado { classe = classe, chance = chance };
        foreach (TipoAtributoManopla atributo in Enum.GetValues(typeof(TipoAtributoManopla)))
            if (AtributoValido(atributo))
                configuracao.atributos.Add(new AtributosManoplas.FaixaAtributo { atributo = atributo });
        return configuracao;
    }

    [SerializeField] private List<Espada.LinhaAtributoEspada> textosValores = new()
    {
        new(), new(), new(), new(), new()
    };
    [SerializeField] private TipoAtributoManopla atributoRestricao = TipoAtributoManopla.Forca;
    [SerializeField, Min(0)] private int valorMinimoParaUsar;
    [SerializeField] private GameObject grupoRestricao;
    [SerializeField] private TMP_Text textoAtributoRestricao, textoValorRestricao, textoMensagemRestricao;
    private EstadoSorteio sorteio;
    private string jsonSorteioRestaurado;
    private bool durabilidadeRestaurada;
    private TMP_Text resumoPopup;

    [Header("Popup do machado: laser, sem botao equipar")]
    [SerializeField] private GameObject popup;
    [SerializeField] private GameObject prefabObjeto;
    [SerializeField] private string nomeExibicao;
    [SerializeField, TextArea(3, 8)] private string descricao;
    [SerializeField] private TMP_Text textoNome;
    [SerializeField] private TMP_Text textoClasse;
    [SerializeField] private TMP_Text textoDescricao;
    [SerializeField] private Camera cameraJogador;
    [SerializeField] private Vector3 posicaoPopupNaCamera = new Vector3(0, 0, 1.5f);
    [SerializeField] private Vector3 rotacaoPopupNaCamera;
    [SerializeField, Min(0)] private float espacamentoHorizontalPopups = 0.45f;
    [Min(0)] public float tempoParaFecharPopup = 5f;
    private Coroutine fechamentoPopup;
    private Transform paiOriginalPopup;
    private Vector3 posicaoOriginalPopup, escalaOriginalPopup, escalaMundoPopup;
    private Quaternion rotacaoOriginalPopup;
    private bool posePopupCapturada, popupIsolado;
    private GameObject raizPopup;

    [SerializeField] private int vidaMaxima = 100;
    [SerializeField] private int vidaAtual = 100;
    [Min(1)] public int desgastePorDano = 1;
    [SerializeField] private bool destruirQuandoVidaZerar = false;

    [Header("Texto Durabilidade")]
    public TMP_Text textoValorAtual;
    public TMP_Text textoValorTotal;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [FormerlySerializedAs("somHit")]
    [SerializeField] private AudioClip somCorte;
    [SerializeField] private AudioClip somOutros;
    [SerializeField] private AudioClip somQuebra;
    [FormerlySerializedAs("volumeHit")]
    [SerializeField] private float volumeCorte = 1f;
    [SerializeField] private float volumeOutros = 1f;
    [SerializeField] private float volumeQuebra = 1f;
    [FormerlySerializedAs("cooldownSomHit")]
    [SerializeField] private float cooldownSomColisao = 0.1f;

    private const int AtaquePadrao = 1;
    private const float IntervaloEntreGolpesCombate = 0.35f;
    private const float VelocidadeMinimaGolpe = 0.65f;
    private const float RotacaoMinimaGolpe = 90f;
    private float proximoGolpeCombate;
    private Transform donoAtualPlayer;
    private StatusPlayer jogadorComBonus;
    private static readonly HashSet<Machado> machadosEmpunhados = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparMachadosEmpunhados() => machadosEmpunhados.Clear();
    private XRGrabInteractable grabInteractable;
    private bool quebrado;
    private float proximoSomColisaoPermitido;
    private Vector3 ultimaPosicaoAudioSegurado;
    private Quaternion ultimaRotacaoAudioSegurado;
    private float velocidadeLinearAudioSegurado;
    private float velocidadeAngularAudioSegurado;
    private float proximoAudioSeguradoPermitido;
    private bool temAmostraAudioSegurado;
    private class BloqueioArvore
    {
        public VidaArvore arvore;
        public Collider[] collidersArvore;
        public int fixedUpdatesFora;
        public bool aguardandoChecagemSaida;
    }

    private readonly HashSet<int> arvoresBloqueadasAteSairRealmente = new();
    private readonly Dictionary<int, BloqueioArvore> bloqueiosArvores = new();
    private readonly List<int> idsArvoresParaDesbloquear = new();
    private readonly HashSet<Collider> contatosAudioSeguradoAtivos = new();
    private static readonly CultureInfo CulturaDurabilidade = CultureInfo.GetCultureInfo("pt-BR");
    private const float VelocidadeMinimaAudioSegurado = 0.25f;
    private const float VelocidadeAngularMinimaAudioSegurado = 45f;
    private const float CooldownMinimoAudioSegurado = 0.2f;
    private const int FixedUpdatesForaArvoreParaDesbloquear = 3;
    private const float MargemSeparacaoRealArvore = 0.02f;

    public int VidaAtual => vidaAtual;
    public int VidaMaxima => vidaMaxima;
    public bool Quebrado => quebrado || vidaAtual <= 0;
    public int CalcularQuantidadeColetaPorRaridade(int quantidadeNormal)
    {
        GarantirSorteio();
        // A classe sorteada controla a coleta, com o teto ja usado pelo jogo.
        int multiplicador = Mathf.Clamp((int)sorteio.classe + 1, 1, 7);
        return (int)Math.Min(int.MaxValue, (long)Mathf.Max(1, quantidadeNormal) * multiplicador);
    }

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        ResolverPopupMachado();
        NormalizarVida();
        AtualizarTextoDurabilidade();
        OcultarCanvasLegado();
        FecharPopup();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (grabInteractable != null)
        {
            grabInteractable.hoverEntered.AddListener(OnHoverPopupMachado);
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
            AtualizarDonoPelaSelecaoAtual();
            AtualizarBonusEmpunhado();
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.hoverEntered.RemoveListener(OnHoverPopupMachado);
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        FecharPopup();
        donoAtualPlayer = null;
        DefinirJogadorComBonus(null);
        LimparContatosArvores();
        contatosAudioSeguradoAtivos.Clear();
    }

    private void OnDestroy()
    {
        FecharPopup();
        if (raizPopup != null) Destroy(raizPopup);
        LimparContatosArvores();
    }

    private void OnValidate()
    {
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        if (!Application.isPlaying && classes != null)
            foreach (var classe in classes)
            {
                if (classe == null) continue;
                ObterFaixaDurabilidade(classe, classe.classe, out int minimo, out int maximo);
                classe.durabilidadeMinima = minimo;
                classe.durabilidadeMaxima = maximo;
                if (classe.textoDurabilidadeAtual == null) classe.textoDurabilidadeAtual = textoValorAtual;
                if (classe.textoDurabilidadeMaxima == null) classe.textoDurabilidadeMaxima = textoValorTotal;
            }
        volumeCorte = Mathf.Max(0f, volumeCorte);
        volumeOutros = Mathf.Max(0f, volumeOutros);
        volumeQuebra = Mathf.Max(0f, volumeQuebra);
        cooldownSomColisao = Mathf.Max(0f, cooldownSomColisao);
        AtualizarTextoDurabilidade();
    }

    private void LateUpdate()
    {
        ResolverPopupMachado();
        if (EstaEmpunhado() || Quebrado) FecharPopup();
        else if (popupIsolado && popup.activeInHierarchy) PosicionarPopup();
        // A retirada por laser pode concluir a transferencia do slot apos o evento XR.
        if ((jogadorComBonus != null) != PodeConcederBonus())
            AtualizarBonusEmpunhado();
        AtualizarMovimentoAudioSegurado();
    }

    private void FixedUpdate()
    {
        AtualizarBloqueiosArvoresPorSeparacaoReal();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Quebrado || collision == null)
            return;

        Collider other = collision.collider;
        if (EstadoItemInventario.EstaNoInventario(this) || EstadoItemInventario.EstaNoInventario(other))
            return;

        if (!TentarAplicarDanoPorContato(other))
        {
            bool podeTocarSom = AudioColisaoFiltro.PodeTocarSomDeColisao(collision);
            if (podeTocarSom)
                TentarTocarAudioColisao(other, "OnCollisionEnter", EstaSendoSegurado());
            else
                LogAudioFerramenta("OnCollisionEnter", other, "bloqueado pelo filtro", false, false);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (Quebrado || collision == null || !EstaSendoSegurado())
            return;

        Collider other = collision.collider;
        if (EstadoItemInventario.EstaNoInventario(this) || EstadoItemInventario.EstaNoInventario(other))
            return;

        if (other == null)
        {
            LogAudioFerramenta("OnCollisionStay", null, "collider nulo", false, false);
            return;
        }

        if (ColliderPertenceAArvore(other))
            return;

        bool jaEstavaEmContato = contatosAudioSeguradoAtivos.Contains(other);
        if (jaEstavaEmContato)
        {
            LogAudioFerramenta("OnCollisionStay", other, "contato ja registrado", true, false);
            return;
        }

        if (!AudioColisaoFiltro.PodeTocarSomDeColisao(collision))
        {
            LogAudioFerramenta("OnCollisionStay", other, "bloqueado pelo filtro", false, false);
            return;
        }

        if (!DeveTocarAudioSegurado())
        {
            LogAudioFerramenta("OnCollisionStay", other, "sem movimento minimo ou em cooldown", false, false);
            return;
        }

        TentarTocarAudioColisao(other, "OnCollisionStay", true);
        proximoAudioSeguradoPermitido = Time.time + CooldownMinimoAudioSegurado;
    }

    private void OnCollisionExit(Collision collision)
    {
        Collider other = collision != null ? collision.collider : null;
        bool estavaEmContato = other != null && contatosAudioSeguradoAtivos.Contains(other);
        RemoverContatoAudioSegurado(other);
        MarcarArvoreParaChecagemDeSaidaReal(other);
        LogAudioFerramenta("OnCollisionExit", other, estavaEmContato ? "contato liberado" : "contato nao estava registrado", estavaEmContato, false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Quebrado)
            return;

        if (EstadoItemInventario.EstaNoInventario(this) || EstadoItemInventario.EstaNoInventario(other))
            return;

        TentarAplicarDanoPorContato(other);
    }

    private void OnTriggerExit(Collider other)
    {
        MarcarArvoreParaChecagemDeSaidaReal(other);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args == null || args.interactorObject == null) return;
        if (args.interactorObject is XRSocketInteractor) return;
        FecharPopup();
        GarantirSorteio();
        Transform interactorTransform = ObterTransformInteractor(args.interactorObject);
        donoAtualPlayer = EncontrarPlayerDonoAPartirDoTransform(interactorTransform);
        AtualizarBonusEmpunhado();
        ResetarAmostraAudioSegurado();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        AtualizarDonoPelaSelecaoAtual();
        AtualizarBonusEmpunhado();
        ResetarAmostraAudioSegurado();
    }

    private void TentarTocarAudioColisao(Collider other, string origem, bool controlarContatoAtivo)
    {
        if (other == null)
        {
            LogAudioFerramenta(origem, null, "collider nulo", false, false);
            return;
        }

        bool jaEstavaEmContato = contatosAudioSeguradoAtivos.Contains(other);
        if (controlarContatoAtivo && jaEstavaEmContato)
        {
            LogAudioFerramenta(origem, other, "contato ja registrado", true, false);
            return;
        }

        if (DeveIgnorarObjeto(other))
        {
            LogAudioFerramenta(origem, other, "objeto ignorado", jaEstavaEmContato, false);
            return;
        }

        bool tocou = TocarSomColisao(somOutros, volumeOutros);

        if (controlarContatoAtivo)
            RegistrarContatoAudioSegurado(other);

        LogAudioFerramenta(origem, other, "Outros", jaEstavaEmContato, tocou);
    }

    private void RegistrarContatoAudioSegurado(Collider other)
    {
        if (other != null)
            contatosAudioSeguradoAtivos.Add(other);
    }

    private void RemoverContatoAudioSegurado(Collider other)
    {
        if (other != null)
            contatosAudioSeguradoAtivos.Remove(other);
    }

    private bool TentarAplicarDanoPorContato(Collider other)
    {
        // Somente uma selecao de mao permite atacar. Sockets, quedas e inventario
        // nao devem transformar o machado em uma fonte de dano por contato.
        if (!EstaEmpunhado() || other == null || DeveIgnorarObjeto(other))
            return false;

        if (donoAtualPlayer == null) return false;
        StatusPlayer jogador = donoAtualPlayer.GetComponentInChildren<StatusPlayer>(true);
        if (!PodeUsar(jogador)) return false;

        VidaArvore arvore = BuscarVidaArvore(other);
        if (arvore != null)
        {
            if (!BloquearArvoreAteSairRealmente(arvore) ||
                !arvore.ReceberDanoDeMachado(ObterAtaqueBase(), gameObject))
                return false;
        }
        else if (!TentarGolpearAlvoDeCombate(other))
        {
            return false;
        }

        TocarSomColisao(somCorte, volumeCorte);
        RegistrarContatoAudioSegurado(other);
        ReduzirVidaDoMachado();
        return true;
    }

    private bool TentarGolpearAlvoDeCombate(Collider contato)
    {
        if (Time.time < proximoGolpeCombate ||
            (velocidadeLinearAudioSegurado < VelocidadeMinimaGolpe &&
             velocidadeAngularAudioSegurado < RotacaoMinimaGolpe))
            return false;

        Component alvo = ReceptorDanoInimigo.Resolver(contato);
        StatusPlayer outroJogador = contato.GetComponentInParent<StatusPlayer>();
        if (outroJogador == null && contato.attachedRigidbody != null)
            outroJogador = contato.attachedRigidbody.GetComponentInParent<StatusPlayer>();

        if (alvo == null && outroJogador == null) return false;
        if (alvo != null && ReceptorDanoInimigo.EstaMorto(alvo)) return false;
        if (outroJogador != null && outroJogador.transform.IsChildOf(donoAtualPlayer)) return false;

        int ataque = ObterAtaqueBase();
        GameObject atacante = donoAtualPlayer.gameObject;
        // Registra antes de callbacks de dano para evitar impactos reentrantes.
        proximoGolpeCombate = Time.time + IntervaloEntreGolpesCombate;
        if (alvo != null)
        {
            ReceptorDanoInimigo.Aplicar(alvo, ataque, atacante);
            return true;
        }

        return outroJogador.TentarReceberDano(ataque, atacante);
    }

    private int ObterAtaqueBase()
    {
        GarantirSorteio();
        return sorteio.ataque > 0 ? sorteio.ataque : AtaquePadrao;
    }

    internal static bool ControlaImpacto(Collider contato)
    {
        return contato != null && (contato.GetComponentInParent<Machado>() != null ||
            (contato.attachedRigidbody != null && contato.attachedRigidbody.GetComponent<Machado>() != null));
    }

    private bool BloquearArvoreAteSairRealmente(VidaArvore arvore)
    {
        if (arvore == null)
            return false;

        int id = arvore.GetInstanceID();
        if (!arvoresBloqueadasAteSairRealmente.Add(id))
            return false;

        bloqueiosArvores[id] = new BloqueioArvore
        {
            arvore = arvore,
            collidersArvore = arvore.GetComponentsInChildren<Collider>(true),
            fixedUpdatesFora = 0,
            aguardandoChecagemSaida = false
        };

        return true;
    }

    private void MarcarArvoreParaChecagemDeSaidaReal(Collider colliderContato)
    {
        VidaArvore arvore = BuscarVidaArvore(colliderContato);
        if (arvore == null)
            return;

        int id = arvore.GetInstanceID();
        if (bloqueiosArvores.TryGetValue(id, out BloqueioArvore bloqueio))
            bloqueio.aguardandoChecagemSaida = true;
    }

    private void AtualizarBloqueiosArvoresPorSeparacaoReal()
    {
        if (arvoresBloqueadasAteSairRealmente.Count == 0)
            return;

        idsArvoresParaDesbloquear.Clear();
        Collider[] collidersMachado = GetComponentsInChildren<Collider>(true);

        foreach (KeyValuePair<int, BloqueioArvore> par in bloqueiosArvores)
        {
            int id = par.Key;
            BloqueioArvore bloqueio = par.Value;

            if (bloqueio == null || bloqueio.arvore == null)
            {
                idsArvoresParaDesbloquear.Add(id);
                continue;
            }

            if (ExisteSobreposicaoRealComArvore(collidersMachado, bloqueio))
            {
                bloqueio.fixedUpdatesFora = 0;
                bloqueio.aguardandoChecagemSaida = false;
                continue;
            }

            bloqueio.fixedUpdatesFora++;
            bloqueio.aguardandoChecagemSaida = true;

            if (bloqueio.fixedUpdatesFora >= FixedUpdatesForaArvoreParaDesbloquear)
                idsArvoresParaDesbloquear.Add(id);
        }

        for (int i = 0; i < idsArvoresParaDesbloquear.Count; i++)
            DesbloquearArvore(idsArvoresParaDesbloquear[i]);
    }

    private bool ExisteSobreposicaoRealComArvore(Collider[] collidersMachado, BloqueioArvore bloqueio)
    {
        if (collidersMachado == null || bloqueio == null || bloqueio.arvore == null)
            return false;

        if (bloqueio.collidersArvore == null || bloqueio.collidersArvore.Length == 0)
            bloqueio.collidersArvore = bloqueio.arvore.GetComponentsInChildren<Collider>(true);

        Collider[] collidersArvore = bloqueio.collidersArvore;
        if (collidersArvore == null || collidersArvore.Length == 0)
            return false;

        for (int i = 0; i < collidersMachado.Length; i++)
        {
            Collider colliderMachado = collidersMachado[i];
            if (!ColliderValidoParaSeparacao(colliderMachado))
                continue;

            for (int j = 0; j < collidersArvore.Length; j++)
            {
                Collider colliderArvore = collidersArvore[j];
                if (!ColliderValidoParaSeparacao(colliderArvore))
                    continue;

                if (CollidersAindaSeTocamOuSobrepoem(colliderMachado, colliderArvore))
                    return true;
            }
        }

        return false;
    }

    private bool CollidersAindaSeTocamOuSobrepoem(Collider colliderMachado, Collider colliderArvore)
    {
        Bounds boundsMachado = colliderMachado.bounds;
        Bounds boundsArvore = colliderArvore.bounds;
        boundsMachado.Expand(MargemSeparacaoRealArvore * 2f);
        boundsArvore.Expand(MargemSeparacaoRealArvore * 2f);

        if (!boundsMachado.Intersects(boundsArvore))
            return false;

        if (Physics.ComputePenetration(
            colliderMachado,
            colliderMachado.transform.position,
            colliderMachado.transform.rotation,
            colliderArvore,
            colliderArvore.transform.position,
            colliderArvore.transform.rotation,
            out _,
            out _))
        {
            return true;
        }

        float margemQuadrada = MargemSeparacaoRealArvore * MargemSeparacaoRealArvore;
        Vector3 pontoMachado = colliderMachado.ClosestPoint(colliderArvore.bounds.center);
        Vector3 pontoArvore = colliderArvore.ClosestPoint(pontoMachado);
        if ((pontoMachado - pontoArvore).sqrMagnitude <= margemQuadrada)
            return true;

        pontoArvore = colliderArvore.ClosestPoint(colliderMachado.bounds.center);
        pontoMachado = colliderMachado.ClosestPoint(pontoArvore);
        return (pontoMachado - pontoArvore).sqrMagnitude <= margemQuadrada;
    }

    private static bool ColliderValidoParaSeparacao(Collider collider)
    {
        return collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
    }

    private void DesbloquearArvore(int id)
    {
        arvoresBloqueadasAteSairRealmente.Remove(id);
        bloqueiosArvores.Remove(id);
    }

    private bool ColliderPertenceAArvore(Collider other)
    {
        if (other == null || DeveIgnorarObjeto(other))
            return false;

        return BuscarVidaArvore(other) != null;
    }

    private void LimparContatosArvores()
    {
        arvoresBloqueadasAteSairRealmente.Clear();
        bloqueiosArvores.Clear();
        idsArvoresParaDesbloquear.Clear();
    }

    private void NormalizarVida()
    {
        vidaMaxima = Mathf.Max(1, vidaMaxima);

        if (vidaAtual <= 0)
            vidaAtual = vidaMaxima;

        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        quebrado = vidaAtual <= 0;
        AtualizarTextoDurabilidade();
    }

    private void EncontrarTextosDurabilidadeSeNecessario()
    {
        if (textoValorAtual != null && textoValorTotal != null)
            return;

        TMP_Text[] textos = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textos.Length; i++)
        {
            TMP_Text texto = textos[i];
            if (texto == null)
                continue;

            string nome = NormalizarNome(texto.gameObject.name);

            if (textoValorAtual == null && (nome == "valoratual" || nome == "atual"))
                textoValorAtual = texto;

            if (textoValorTotal == null && (nome == "valortotal" || nome == "total"))
                textoValorTotal = texto;
        }

    }

    public void AtualizarDurabilidadeVisual()
    {
        AtualizarTextoDurabilidade();
    }

    private void AtualizarTextoDurabilidade()
    {
        EncontrarTextosDurabilidadeSeNecessario();

        var classe = ObterConfiguracaoSorteada();
        var textoAtual = classe?.textoDurabilidadeAtual != null ? classe.textoDurabilidadeAtual : textoValorAtual;
        var textoTotal = classe?.textoDurabilidadeMaxima != null ? classe.textoDurabilidadeMaxima : textoValorTotal;
        if (textoAtual != null) textoAtual.text = FormatarValorDurabilidade(vidaAtual);
        if (textoTotal != null) textoTotal.text = FormatarValorDurabilidade(vidaMaxima);
    }

    private string FormatarValorDurabilidade(int valor)
    {
        return Mathf.Max(0, valor).ToString("N0", CulturaDurabilidade);
    }

    private Transform ObterTransformInteractor(IXRSelectInteractor interactor)
    {
        return (interactor as MonoBehaviour)?.transform;
    }

    private void AtualizarDonoPelaSelecaoAtual()
    {
        donoAtualPlayer = null;

        if (grabInteractable == null || grabInteractable.interactorsSelecting.Count == 0)
            return;

        for (int i = 0; i < grabInteractable.interactorsSelecting.Count; i++)
        {
            var interactor = grabInteractable.interactorsSelecting[i];
            if (interactor is XRSocketInteractor) continue;
            Transform interactorTransform = ObterTransformInteractor(interactor);
            Transform player = EncontrarPlayerDonoAPartirDoTransform(interactorTransform);

            if (player == null)
                continue;

            donoAtualPlayer = player;
            return;
        }
    }

    private void AtualizarMovimentoAudioSegurado()
    {
        if (!EstaSendoSegurado())
        {
            ResetarAmostraAudioSegurado();
            return;
        }

        if (!temAmostraAudioSegurado || Time.deltaTime <= 0f)
        {
            ultimaPosicaoAudioSegurado = transform.position;
            ultimaRotacaoAudioSegurado = transform.rotation;
            velocidadeLinearAudioSegurado = 0f;
            velocidadeAngularAudioSegurado = 0f;
            temAmostraAudioSegurado = true;
            return;
        }

        velocidadeLinearAudioSegurado = (transform.position - ultimaPosicaoAudioSegurado).magnitude / Time.deltaTime;
        velocidadeAngularAudioSegurado = Quaternion.Angle(ultimaRotacaoAudioSegurado, transform.rotation) / Time.deltaTime;
        ultimaPosicaoAudioSegurado = transform.position;
        ultimaRotacaoAudioSegurado = transform.rotation;
    }

    private bool DeveTocarAudioSegurado()
    {
        return EstaSendoSegurado() &&
               Time.time >= proximoAudioSeguradoPermitido &&
               (velocidadeLinearAudioSegurado >= VelocidadeMinimaAudioSegurado ||
                velocidadeAngularAudioSegurado >= VelocidadeAngularMinimaAudioSegurado);
    }

    private bool EstaSendoSegurado()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable != null && grabInteractable.interactorsSelecting.Count > 0)
            return true;

        if (donoAtualPlayer == null)
            AtualizarDonoPelaSelecaoAtual();

        return donoAtualPlayer != null;
    }

    private void ResetarAmostraAudioSegurado()
    {
        temAmostraAudioSegurado = false;
        velocidadeLinearAudioSegurado = 0f;
        velocidadeAngularAudioSegurado = 0f;
        proximoAudioSeguradoPermitido = 0f;
    }

    private Transform EncontrarPlayerDonoAPartirDoTransform(Transform origem)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (string.Equals(atual.tag, "Player", StringComparison.Ordinal))
                return atual;

            atual = atual.parent;
        }

        return null;
    }

    private string NormalizarNome(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        return texto.Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty);
    }

    private void ReduzirVidaDoMachado()
    {
        if (quebrado)
            return;

        vidaAtual = Mathf.Max(0, vidaAtual - Mathf.Max(1, desgastePorDano));
        AtualizarTextoDurabilidade();

        if (vidaAtual <= 0)
            QuebrarMachado();
    }

    private void QuebrarMachado()
    {
        if (quebrado)
            return;

        quebrado = true;
        DefinirJogadorComBonus(null);
        FecharPopup();
        vidaAtual = 0;
        AtualizarTextoDurabilidade();
        TocarSomQuebra();

        if (destruirQuandoVidaZerar)
        {
            GetComponent<ItemPersistente>()?.RegistrarConsumoPermanente();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    private bool TocarSomColisao(AudioClip clip, float volume)
    {
        if (Time.time < proximoSomColisaoPermitido)
            return false;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null || clip == null)
            return false;

        proximoSomColisaoPermitido = Time.time + cooldownSomColisao;
        audioSource.PlayOneShot(clip, volume);
        return true;
    }

    private void TocarSomQuebra()
    {
        if (somQuebra != null)
            AudioSource.PlayClipAtPoint(somQuebra, transform.position, volumeQuebra);
    }

    private VidaArvore BuscarVidaArvore(Collider alvo)
    {
        if (alvo == null)
            return null;

        VidaArvore vida = alvo.GetComponentInParent<VidaArvore>();
        if (vida != null)
            return vida;

        Rigidbody alvoRb = alvo.attachedRigidbody;
        if (alvoRb != null)
        {
            vida = alvoRb.GetComponentInParent<VidaArvore>();
            if (vida != null)
                return vida;
        }

        return null;
    }

    private bool DeveIgnorarObjeto(Collider alvo)
    {
        if (alvo == null)
            return true;

        if (DeveIgnorarObjeto(alvo.gameObject))
            return true;

        Rigidbody alvoRb = alvo.attachedRigidbody;
        return alvoRb != null && DeveIgnorarObjeto(alvoRb.gameObject);
    }

    private bool DeveIgnorarObjeto(GameObject objeto)
    {
        if (objeto == null)
            return true;

        Transform alvo = objeto.transform;
        if (alvo == transform || alvo.IsChildOf(transform))
            return true;

        if (donoAtualPlayer == null)
            AtualizarDonoPelaSelecaoAtual();

        if (donoAtualPlayer == null)
            return false;

        if (alvo == donoAtualPlayer || alvo.IsChildOf(donoAtualPlayer))
            return true;

        Transform playerDoAlvo = EncontrarPlayerDonoAPartirDoTransform(alvo);
        return playerDoAlvo == donoAtualPlayer;
    }

    private void LogAudioFerramenta(string origem, Collider other, string audioEscolhido, bool jaEstavaEmContato, bool tocou)
    {
    }
    private void ResolverPopupMachado()
    {
        if (popup != null) return;
        var t = transform.Find("PopUp") ?? transform.Find("Popup");
        if (t != null) popup = t.gameObject;
    }

    private void OcultarCanvasLegado()
    {
        Canvas canvas = textoValorAtual != null ? textoValorAtual.GetComponentInParent<Canvas>(true) : null;
        if (canvas != null && textoValorTotal != null &&
            textoValorTotal.GetComponentInParent<Canvas>(true) == canvas &&
            canvas.transform != transform && canvas.transform.IsChildOf(transform) &&
            (popup == null || !canvas.transform.IsChildOf(popup.transform)))
            canvas.enabled = false;
    }

    private bool EstaEmpunhado()
    {
        if (grabInteractable == null) return false;
        foreach (var interactor in grabInteractable.interactorsSelecting)
            if (interactor != null && !(interactor is XRSocketInteractor)) return true;
        return false;
    }

    private void OnHoverPopupMachado(HoverEnterEventArgs args)
    {
        if (args == null || !Espada.EhLaserDoPopup(args.interactorObject) ||
            EstaEmpunhado() || Quebrado || !isActiveAndEnabled)
            return;
        if (cameraJogador == null) cameraJogador = Camera.main;
        if (cameraJogador == null) return;

        ResolverPopupMachado();
        if (popup == null)
            popup = Flecha.CriarPopupSimples(transform, "Popup do Machado", out resumoPopup);
        GarantirSorteio();
        CancelarFechamentoPopup();
        PrepararPopup();
        // O inventario desativa o Canvas e seus textos ao guardar o item.
        foreach (var canvas in popup.GetComponentsInChildren<Canvas>(true))
            canvas.enabled = true;
        foreach (var graphic in popup.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            graphic.enabled = true;
        AtualizarPopup();
        popup.SetActive(true);
        PosicionarPopup();
        fechamentoPopup = StartCoroutine(FecharPopupDepois());
    }

    internal void GarantirSorteio()
    {
        if (sorteio != null) return;

        var configuracao = Flecha.SortearClassePonderada(classes);
        var tipos = new HashSet<TipoAtributoManopla>();
        var faixas = configuracao?.atributos?.FindAll(faixa => faixa != null &&
            AtributoValido(faixa.atributo) && Espada.FaixaSorteioValida(faixa.minimo, faixa.maximo, 0) &&
            tipos.Add(faixa.atributo)) ?? new List<AtributosManoplas.FaixaAtributo>();
        int minimo = Mathf.Clamp(configuracao?.quantidadeMinima ?? 0, 0, faixas.Count);
        int maximo = Mathf.Clamp(configuracao?.quantidadeMaxima ?? 0, minimo, faixas.Count);
        var classe = configuracao?.classe ?? ClasseManopla.Comum;
        if (!durabilidadeRestaurada)
        {
            ObterFaixaDurabilidade(configuracao, classe, out int limiteMinimo, out int limiteMaximo);
            vidaMaxima = Flecha.SortearInteiro(limiteMinimo, limiteMaximo, 1);
            vidaAtual = vidaMaxima;
        }
        sorteio = new EstadoSorteio
        {
            classe = classe,
            durabilidadeMaxima = vidaMaxima,
            ataque = configuracao != null && configuracao.ataqueMaximo > 0
                ? Flecha.SortearInteiro(configuracao.ataqueMinimo, configuracao.ataqueMaximo, 1) : 0,
            atributos = Espada.SortearValoresAtributos(faixas, minimo, maximo)
        };
        AtualizarTextoDurabilidade();
    }

    private void ObterFaixaDurabilidade(ClasseMachado config, ClasseManopla classe, out int minimo, out int maximo)
    {
        maximo = config != null && config.durabilidadeMaxima > 0
            ? config.durabilidadeMaxima
            : (int)Math.Min(int.MaxValue - 1, (long)Mathf.Max(1, vidaMaxima) + 2L * (int)classe + 1);
        maximo = Mathf.Clamp(maximo, 1, int.MaxValue - 1);
        minimo = config != null && config.durabilidadeMinima > 0
            ? Mathf.Clamp(config.durabilidadeMinima, 1, int.MaxValue - 1)
            : Mathf.Max(1, maximo - 2);
        maximo = Mathf.Max(minimo, maximo);
    }

    private ClasseMachado ObterConfiguracaoSorteada() =>
        sorteio != null && classes != null
            ? Array.Find(classes, classe => classe != null && classe.classe == sorteio.classe)
            : null;

    private static bool AtributoValido(TipoAtributoManopla atributo) =>
        Enum.IsDefined(typeof(TipoAtributoManopla), atributo) &&
        atributo != TipoAtributoManopla.Critico && atributo != TipoAtributoManopla.Defesa;

    internal void PrepararSorteioParaSalvar()
    {
        if (!durabilidadeRestaurada)
            GarantirSorteio();
    }

    internal string CapturarSorteioJson(string anteriores)
    {
        if (sorteio == null) return anteriores;
        if (jsonSorteioRestaurado != null) return jsonSorteioRestaurado;
        sorteio.anteriores = anteriores;
        return JsonUtility.ToJson(sorteio);
    }

    internal static EstadoSorteio LerSorteioJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || !json.Contains(EstadoSorteio.Formato)) return null;

        var estado = JsonUtility.FromJson<EstadoSorteio>(json);
        var tipos = new HashSet<TipoAtributoManopla>();
        if (estado == null || estado.formato != EstadoSorteio.Formato ||
            !Enum.IsDefined(typeof(ClasseManopla), estado.classe) || estado.ataque < 0 || estado.durabilidadeMaxima < 0 ||
            estado.atributos == null || estado.atributos.Exists(valor => valor == null || valor.valor < 0 ||
                !AtributoValido(valor.atributo) || !tipos.Add(valor.atributo)))
            throw new System.IO.InvalidDataException("Sorteio do machado inválido no save.");
        return estado;
    }

    internal void RestaurarSorteio(string json, float durabilidadeSalva)
    {
        sorteio = LerSorteioJson(json);
        jsonSorteioRestaurado = sorteio != null ? json : null;
        durabilidadeRestaurada = durabilidadeSalva >= 0f;
        if (sorteio != null && sorteio.durabilidadeMaxima > 0)
            vidaMaxima = sorteio.durabilidadeMaxima;
        if (durabilidadeRestaurada)
            quebrado = durabilidadeSalva <= 0f;
    }

    internal Action CapturarSorteioParaCarga()
    {
        var estado = sorteio;
        var json = jsonSorteioRestaurado;
        bool restaurada = durabilidadeRestaurada;
        bool estavaQuebrado = quebrado;
        return () =>
        {
            sorteio = estado;
            jsonSorteioRestaurado = json;
            durabilidadeRestaurada = restaurada;
            quebrado = estavaQuebrado;
        };
    }

    private bool PodeConcederBonus() => isActiveAndEnabled && !Quebrado &&
        donoAtualPlayer != null && EstaEmpunhado() && !EstadoItemInventario.EstaNoInventario(this);

    private void AtualizarBonusEmpunhado()
    {
        StatusPlayer jogador = PodeConcederBonus()
            ? donoAtualPlayer.GetComponentInChildren<StatusPlayer>(true)
            : null;
        if (jogador != null) GarantirSorteio();
        DefinirJogadorComBonus(jogador);
    }

    private void DefinirJogadorComBonus(StatusPlayer jogador)
    {
        StatusPlayer anterior = jogadorComBonus;
        jogadorComBonus = jogador;
        if (jogador != null) machadosEmpunhados.Add(this);
        else machadosEmpunhados.Remove(this);
        if (anterior == jogador) return;
        if (anterior != null) anterior.AtualizarAtributosEquipamentos();
        if (jogador != null) jogador.AtualizarAtributosEquipamentos();
    }

    private int ObterValorAtributo(TipoAtributoManopla atributo)
    {
        if (sorteio == null) return 0;
        foreach (var valor in sorteio.atributos)
            if (valor.atributo == atributo) return valor.valor;
        return 0;
    }

    internal static long ObterBonusEmpunhado(StatusPlayer jogador, TipoAtributoManopla atributo)
    {
        if (jogador == null) return 0;
        long total = 0;
        foreach (var machado in machadosEmpunhados)
            if (machado != null && machado.jogadorComBonus == jogador && machado.PodeConcederBonus())
                total += machado.ObterValorAtributo(atributo);
        return total;
    }

    private bool PodeUsar(StatusPlayer jogador)
    {
        if (valorMinimoParaUsar <= 0) return true;
        int bonusProprio = jogador != null && jogadorComBonus == jogador && PodeConcederBonus()
            ? ObterValorAtributo(atributoRestricao) : 0;
        return Espada.AtendeRestricaoAtributo(Espada.ObterAtributoTotal(jogador, atributoRestricao),
            bonusProprio, valorMinimoParaUsar);
    }

    public void AtualizarPopup()
    {
        if (popup == null || sorteio == null) return;
        string nome = !string.IsNullOrWhiteSpace(nomeExibicao) ? nomeExibicao.Trim() :
            prefabObjeto != null ? prefabObjeto.name : name.Replace("(Clone)", "").Trim();
        string ataque = ObterAtaqueBase().ToString();
        StatusPlayer jogador = cameraJogador != null ? cameraJogador.GetComponentInParent<StatusPlayer>() : null;
        bool restrito = !PodeUsar(jogador);

        if (resumoPopup != null)
        {
            var atributos = sorteio.atributos.GetRange(0, Mathf.Min(5, sorteio.atributos.Count))
                .ConvertAll(valor => $"{NomeAtributo(valor.atributo)} +{valor.valor}");
            string restricao = restrito ? $"Restrição: {NomeAtributo(atributoRestricao)} {valorMinimoParaUsar}\n" : "";
            resumoPopup.text = $"{nome}\n{sorteio.classe} · Ataque {ataque}\nDurabilidade {vidaAtual}/{vidaMaxima}\n" +
                $"{string.Join(" · ", atributos)}\n{restricao}{descricao}";
            return;
        }

        textoNome ??= BuscarTextoPopup("Fundo/nome");
        textoClasse ??= BuscarTextoPopup("Fundo/Valor classe");
        textoDescricao ??= BuscarTextoPopup("Fundo/Descricao");
        textoValorAtual = ResolverTextoDurabilidadePopup(textoValorAtual, "Fundo/Durabilidade/Durabilidade Atual");
        textoValorTotal = ResolverTextoDurabilidadePopup(textoValorTotal, "Fundo/Durabilidade/Durabilidade Limite");
        if (textoNome != null) textoNome.text = nome;
        if (textoClasse != null) textoClasse.text = sorteio.classe.ToString();
        if (textoDescricao != null) textoDescricao.text = descricao;
        AtualizarTextoDurabilidade();

        var classeSorteada = ObterConfiguracaoSorteada();
        var textoAtaque = classeSorteada?.textoAtaque ?? BuscarTextoPopup("Fundo/Ataque/Ataque Valor");
        if (classes != null)
            foreach (var classe in classes)
                if (classe?.textoAtaque != null && classe.textoAtaque != textoAtaque)
                    classe.textoAtaque.text = "";
        if (textoAtaque != null) textoAtaque.text = ataque;

        for (int i = 0; i < 5; i++)
        {
            var linha = textosValores != null && i < textosValores.Count ? textosValores[i] : null;
            var raiz = popup.transform.Find("Fundo/" + i);
            var textoAtributo = linha?.textoNome ?? raiz?.Find("Texto")?.GetComponent<TMP_Text>();
            var textoValor = linha?.textoValor ?? raiz?.Find("Numero")?.GetComponent<TMP_Text>();
            var valor = i < sorteio.atributos.Count ? sorteio.atributos[i] : null;
            if (raiz != null) raiz.gameObject.SetActive(valor != null);
            if (textoAtributo != null) textoAtributo.text = valor != null ? NomeAtributo(valor.atributo) : "";
            if (textoValor != null) textoValor.text = valor != null ? "+" + valor.valor : "";
        }

        if (grupoRestricao == null) grupoRestricao = popup.transform.Find("Fundo/Restricao")?.gameObject;
        if (grupoRestricao != null)
        {
            grupoRestricao.SetActive(restrito);
            textoAtributoRestricao ??= grupoRestricao.transform.Find("Atributo")?.GetComponent<TMP_Text>();
            textoValorRestricao ??= grupoRestricao.transform.Find("Valor")?.GetComponent<TMP_Text>();
            textoMensagemRestricao ??= grupoRestricao.transform.Find("Descricao")?.GetComponent<TMP_Text>();
        }
        if (textoAtributoRestricao != null) textoAtributoRestricao.text = restrito ? NomeAtributo(atributoRestricao) : "";
        if (textoValorRestricao != null) textoValorRestricao.text = restrito ? valorMinimoParaUsar.ToString() : "";
        if (textoMensagemRestricao != null) textoMensagemRestricao.text = restrito ? "Seu valor está abaixo do mínimo" : "";
    }

    private TMP_Text BuscarTextoPopup(string caminho) => popup.transform.Find(caminho)?.GetComponent<TMP_Text>();

    private TMP_Text ResolverTextoDurabilidadePopup(TMP_Text texto, string caminho) =>
        texto != null && texto.transform.IsChildOf(popup.transform) ? texto : BuscarTextoPopup(caminho) ?? texto;

    private static string NomeAtributo(TipoAtributoManopla atributo) =>
        atributo == TipoAtributoManopla.Mana ? "MP" : atributo.ToString();

    private IEnumerator FecharPopupDepois()
    {
        float tempo = float.IsNaN(tempoParaFecharPopup) || float.IsInfinity(tempoParaFecharPopup) ? 0 : Mathf.Max(0, tempoParaFecharPopup);
        yield return new WaitForSeconds(tempo);
        fechamentoPopup = null; FecharPopup();
    }
    private void CancelarFechamentoPopup()
    {
        if (fechamentoPopup != null) StopCoroutine(fechamentoPopup);
        fechamentoPopup = null;
    }
    private void PrepararPopup()
    {
        if (popup == null || popupIsolado) return;
        var t = popup.transform;
        if (!posePopupCapturada)
        {
            paiOriginalPopup = t.parent; posicaoOriginalPopup = t.localPosition; rotacaoOriginalPopup = t.localRotation;
            escalaOriginalPopup = t.localScale; var e = t.lossyScale;
            escalaMundoPopup = new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z)); posePopupCapturada = true;
        }
        if (raizPopup == null)
        {
            raizPopup = new GameObject("[Popup Arma] " + name); raizPopup.hideFlags = HideFlags.DontSave;
            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raizPopup, gameObject.scene);
        }
        t.SetParent(raizPopup.transform, false); t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity;
        t.localScale = escalaMundoPopup; popupIsolado = true;
    }
    private void PosicionarPopup()
    {
        if (cameraJogador == null) cameraJogador = Camera.main;
        if (popup != null && cameraJogador != null)
            LayoutPopupsEquipamentos.Atualizar(popup, cameraJogador, posicaoPopupNaCamera, rotacaoPopupNaCamera, espacamentoHorizontalPopups);
    }
    public void FecharPopup()
    {
        CancelarFechamentoPopup(); LayoutPopupsEquipamentos.Remover(popup);
        if (popup == null) return;
        popup.SetActive(false);
        if (!popupIsolado) return;
        popupIsolado = false;
        var t = popup.transform; t.SetParent(paiOriginalPopup != null ? paiOriginalPopup : transform, false);
        t.localPosition = posicaoOriginalPopup; t.localRotation = rotacaoOriginalPopup; t.localScale = escalaOriginalPopup;
    }
}
