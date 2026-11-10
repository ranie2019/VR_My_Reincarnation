using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class Espada : MonoBehaviour, IDano
{
    [SerializeField, HideInInspector] private int danoEspada = 1;
    [Tooltip("Dano final = dano da espada + Forca total do dono x multiplicador. Zero mantem somente o dano da espada.")]
    [SerializeField, Min(0f)] private float multiplicadorForca = 1f;

    [Header("Texto do ataque sorteado")]
    [Tooltip("Arraste o texto do numero do ataque no popup. Exibe apenas o ataque base sorteado desta arma, sem bonus do player.")]
    [SerializeField] private TMP_Text textoAtaqueSorteado;

    [SerializeField, HideInInspector] protected float vidaMaxima = 20f;
    [SerializeField, HideInInspector] protected float vidaAtual = 20f;
    [Min(0)] public int desgastePorDanoCausado = 1;
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("destruirEspadaAoZerar")]
    private bool destruirAoZerarDurabilidade = true;

    [Header("Textos da durabilidade: atual / limite sorteado")]
    [InspectorName("Texto Durabilidade Atual")] public TMP_Text textoValorAtual;
    [InspectorName("Texto Durabilidade Maxima")] public TMP_Text textoValorTotal;

    [Tooltip("Atributo total exigido. HP e MP usam o limite maximo do player. O bonus da propria arma e descontado na verificacao.")]
    [SerializeField] private TipoAtributoManopla atributoRestricao = TipoAtributoManopla.Forca;
    [SerializeField, Min(0), UnityEngine.Serialization.FormerlySerializedAs("forcaMinimaParaUsar")]
    protected int valorMinimoParaUsar = 10;
    [Header("Grupo visual da restricao (oculto quando o requisito e atendido)")]
    [Tooltip("Objeto pai dos quatro textos. Se vazio, procura o grupo Restricao dentro do popup.")]
    [SerializeField] private GameObject grupoRestricao;
    [Header("Quatro textos da linha de restricao")]
    [SerializeField, InspectorName("Restricao")] private TMP_Text textoRotuloRestricao;
    [SerializeField, InspectorName("Atributos")] private TMP_Text textoAtributoRestricao;
    [SerializeField, InspectorName("Valor"), UnityEngine.Serialization.FormerlySerializedAs("textoRestricao")]
    private TMP_Text textoValorRestricao;
    [SerializeField, InspectorName("Descricao")] private TMP_Text textoMensagemRestricao;
    [SerializeField] private string rotuloRestricaoPopup = "Restricao:";
    [SerializeField] private string mensagemAbaixoMinimo = "Seu valor esta abaixo do minimo";

    [Header("Tags que recebem dano")]
    [SerializeField] private string[] tagsAlvoDano;

    [Header("Cooldown")]
    [SerializeField] private float cooldownDanoMesmoAlvo = 0.35f;
    [SerializeField] private float tempoIgnorarDonoAposSoltar = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somHit;
    [SerializeField] private AudioClip somQuebra;
    [SerializeField] private float volumeHit = 1f;
    [SerializeField] private float volumeQuebra = 1f;
    [SerializeField] private float cooldownSomHit = 0.1f;

    private Transform donoAtualPlayer;
    private Transform ultimoDonoPlayer;
    private XRGrabInteractable grabInteractable;
    private bool quebrada;
    private float ignorarUltimoDonoAte;
    private float proximoSomHitPermitido;
    private readonly Dictionary<int, float> proximoDanoPermitidoPorAlvo = new();
    private static readonly CultureInfo CulturaDurabilidade = CultureInfo.GetCultureInfo("pt-BR");

    protected void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        CapturarConfiguracaoLegada();
        ResolverPopup();
        GarantirGeracao();
        NormalizarVida();
        AtualizarPopup();
        FecharPopup();
    }

    protected void OnEnable()
    {
        if (!espadasAtivas.Contains(this)) espadasAtivas.Add(this);
        if (migracaoLegadaPendente) StartCoroutine(FinalizarMigracaoLegada());
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
            grabInteractable.hoverEntered.AddListener(OnHoverEntered);
            grabInteractable.hoverExited.AddListener(OnHoverExited);
        }
    }

    protected void OnDisable()
    {
        var statusAnterior = GetStatusPlayerDonoAtual();
        espadasAtivas.Remove(this);
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
            grabInteractable.hoverEntered.RemoveListener(OnHoverEntered);
            grabInteractable.hoverExited.RemoveListener(OnHoverExited);
        }

        FecharPopup();
        donoAtualPlayer = null;
        ultimoDonoPlayer = null;
        ignorarUltimoDonoAte = 0f;
        proximoDanoPermitidoPorAlvo.Clear();
        statusAnterior?.AtualizarAtributosEquipamentos();
    }

    protected void OnValidate()
    {
        valorMinimoParaUsar = Mathf.Max(0, valorMinimoParaUsar);
        danoEspada = Mathf.Max(0, danoEspada);
        vidaMaxima = Mathf.Max(1f, ValorPositivoFinito(vidaMaxima));
        vidaAtual = Mathf.Clamp(ValorPositivoFinito(vidaAtual), 0f, vidaMaxima);
        desgastePorDanoCausado = Mathf.Max(0, desgastePorDanoCausado);
        cooldownDanoMesmoAlvo = Mathf.Max(0f, cooldownDanoMesmoAlvo);
        tempoIgnorarDonoAposSoltar = Mathf.Max(0f, tempoIgnorarDonoAposSoltar);
        volumeHit = Mathf.Max(0f, volumeHit);
        volumeQuebra = Mathf.Max(0f, volumeQuebra);
        cooldownSomHit = Mathf.Max(0f, cooldownSomHit);
        AtualizarTextoDurabilidade(false);
    }

    protected virtual bool DestruirAoQuebrar => destruirAoZerarDurabilidade;

    protected void Update()
    {
        if (Application.isPlaying && !quebrada && vidaAtual <= 0f)
            QuebrarEspada();
    }

    protected void LateUpdate()
    {
        if (EstaSendoSegurada())
        {
            if ((popup != null && popup.activeSelf) || fechamentoPopup != null) FecharPopup();
            return;
        }
        if (popup != null && popup.activeInHierarchy)
        {
            AtualizarPopup();
            PosicionarPopup();
        }
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        ProcessarPossivelDano(collision.collider);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        ProcessarPossivelDano(other);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args == null || args.interactorObject == null || args.interactorObject is XRSocketInteractor) return;
        FecharPopup();
        Transform interactorTransform = ObterTransformInteractor(args.interactorObject);
        Transform novoDono = EncontrarPlayerDonoAPartirDoTransform(interactorTransform);
        donoAtualPlayer = novoDono;
        ultimoDonoPlayer = novoDono;
        ignorarUltimoDonoAte = 0f;
        GetStatusPlayerDonoAtual()?.AtualizarAtributosEquipamentos();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        Transform donoAntesDeSoltar = donoAtualPlayer;
        var statusAnterior = GetStatusPlayerDonoAtual();
        AtualizarDonoPelaSelecaoAtual();
        statusAnterior?.AtualizarAtributosEquipamentos();

        if (donoAtualPlayer != null)
        {
            ultimoDonoPlayer = donoAtualPlayer;
            ignorarUltimoDonoAte = 0f;
            return;
        }

        if (donoAntesDeSoltar != null && tempoIgnorarDonoAposSoltar > 0f)
        {
            ultimoDonoPlayer = donoAntesDeSoltar;
            ignorarUltimoDonoAte = Time.time + tempoIgnorarDonoAposSoltar;
        }
    }

    public GameObject GetDonoAtual()
    {
        if (donoAtualPlayer == null)
            AtualizarDonoPelaSelecaoAtual();

        return donoAtualPlayer != null ? donoAtualPlayer.gameObject : null;
    }

    public TipoAtributoManopla AtributoExigido => Gerado && resultado.restricaoDefinida ? resultado.atributoExigido : atributoRestricao;
    public int RequisitoExigido => Gerado && resultado.restricaoDefinida ? resultado.requisitoExigido : valorMinimoParaUsar;

    public bool PodeUsar(StatusPlayer player)
    {
        var tipo = AtributoExigido;
        int bonusDestaArma = player != null && !EstadoItemInventario.EstaNoInventario(this) &&
            GetStatusPlayerDonoAtual() == player ? ObterValor(tipo) : 0;
        return player == null ? RequisitoExigido <= 0 :
            AtendeRestricaoAtributo(ObterAtributoTotal(player, tipo), bonusDestaArma, RequisitoExigido);
    }

    internal static bool AtendeRestricaoAtributo(int total, int bonusDaPropriaArma, int requisito)
    {
        long valorDisponivel = Math.Max(0L, (long)total - Math.Max(0, bonusDaPropriaArma));
        return valorDisponivel >= Math.Max(0, requisito);
    }

    internal static int ObterAtributoTotal(StatusPlayer player, TipoAtributoManopla atributo)
    {
        if (player == null) return 0;
        switch (atributo)
        {
            case TipoAtributoManopla.Forca: return player.GetForcaTotal();
            case TipoAtributoManopla.Constituicao: return player.GetConstituicaoTotal();
            case TipoAtributoManopla.Agilidade: return player.GetAgilidadeTotal();
            case TipoAtributoManopla.Velocidade: return player.GetVelocidadeTotal();
            case TipoAtributoManopla.Inteligencia: return player.GetInteligenciaTotal();
            case TipoAtributoManopla.Espirito: return player.GetEspiritoTotal();
            case TipoAtributoManopla.Destreza: return player.GetDestrezaTotal();
            case TipoAtributoManopla.Vigor: return player.GetVigorTotal();
            case TipoAtributoManopla.Percepcao: return player.GetPercepcaoTotal();
            case TipoAtributoManopla.Resistencia: return player.GetResistenciaTotal();
            case TipoAtributoManopla.Carisma: return player.GetCarismaTotal();
            case TipoAtributoManopla.Sorte: return player.GetSorteTotal();
            case TipoAtributoManopla.Mana: return player.GetManaMaxima();
            case TipoAtributoManopla.Critico: return player.GetCriticoTotal();
            case TipoAtributoManopla.Defesa: return player.GetDefesaTotal();
            case TipoAtributoManopla.HP: return player.GetVidaMaxima();
            default: return 0;
        }
    }

    public float ObterDano() => ObterDanoCalculado();

    protected virtual int ObterDanoCalculado()
    {
        if (quebrada || vidaAtual <= 0f || EstadoItemInventario.EstaNoInventario(this)) return 0;
        GameObject dono = GetDonoAtual();
        StatusPlayer status = dono != null ? dono.GetComponentInChildren<StatusPlayer>(true) : null;
        if (!PodeUsar(status)) return 0;
        return CalcularDanoComForca(danoEspada, status != null ? status.GetForcaTotal() : 0, multiplicadorForca);
    }

    internal static int CalcularDanoComForca(int baseArma, int forcaTotal, float multiplicador)
    {
        double fator = float.IsNaN(multiplicador) || float.IsInfinity(multiplicador) ? 0d : Math.Max(0d, multiplicador);
        double total = Math.Max(0, baseArma) + (double)Math.Max(0, forcaTotal) * fator;
        return (int)Math.Min(int.MaxValue, Math.Round(total, MidpointRounding.AwayFromZero));
    }

    public GameObject ObterDono()
    {
        return GetDonoAtual();
    }

    // ===== Propriedades p�blicas (usadas pelo Reparar) =====
    public float VidaAtual => vidaAtual;
    public float VidaMaxima => vidaMaxima;
    public float VidaMaximaLegada => configuracaoLegadaCapturada ? vidaLegadaConfigurada : vidaMaxima;
    public float VidaFaltando => Mathf.Max(0, vidaMaxima - vidaAtual);
    public bool EstaQuebrada => quebrada || vidaAtual <= 0;

    // ===================== REPARO (usado pelo script Reparar) =====================

    /// <summary>Chamado pelo Reparar.cs quando o jogador paga para reparar a espada.</summary>
    public void RepararCompleto()
    {
        quebrada = false;
        vidaAtual = vidaMaxima;
        AtualizarTextoDurabilidade(true);
    }

    /// <summary>Usado ao restaurar um save (ver ItemPersistente.AplicarSaveData/EscreverDurabilidade).</summary>
    public void DefinirVida(int atual, int maxima)
    {
        vidaMaxima = Mathf.Max(1, maxima);
        vidaAtual = Mathf.Clamp(atual, 0, vidaMaxima);
        quebrada = vidaAtual <= 0;
        AtualizarTextoDurabilidade(true);
    }

    /// <summary>Chamado via reflexao pelo ItemPersistente apos escrever a durabilidade.</summary>
    public void AtualizarDurabilidadeVisual()
    {
        AtualizarTextoDurabilidade(true);
    }

    public StatusPlayer GetStatusPlayerDonoAtual()
    {
        GameObject dono = GetDonoAtual();
        if (dono == null)
            return null;

        StatusPlayer statusPlayer = dono.GetComponent<StatusPlayer>();
        if (statusPlayer != null)
            return statusPlayer;

        statusPlayer = dono.GetComponentInParent<StatusPlayer>();
        if (statusPlayer != null)
            return statusPlayer;

        return dono.transform.root != null
            ? dono.transform.root.GetComponentInChildren<StatusPlayer>(true)
            : null;
    }

    private void ProcessarPossivelDano(Collider outroCollider)
    {
        if (outroCollider == null || quebrada || vidaAtual <= 0)
            return;

        if (EstadoItemInventario.EstaNoInventario(this) || EstadoItemInventario.EstaNoInventario(outroCollider))
            return;

        GameObject objetoTocado = outroCollider.gameObject;
        if (EhParteDaPropriaEspada(objetoTocado))
            return;

        if (!TagEhAlvoValido(objetoTocado, out GameObject alvoResolvido))
            return;

        if (alvoResolvido == null || EhParteDaPropriaEspada(alvoResolvido))
            return;

        if (PertenceAoDonoAtual(alvoResolvido) || PertenceAoDonoRecente(alvoResolvido) || EstaAcopladoAoMesmoDono(alvoResolvido) || EstaAcopladoAoDonoRecente(alvoResolvido))
            return;

        int dano = ObterDanoCalculado();
        if (dano <= 0 || !PodeAplicarDanoAgora(alvoResolvido))
            return;

        bool danoAplicado = TentarAplicarDano(alvoResolvido, dano);
        if (!danoAplicado)
            return;

        TocarSomHit();
        ReduzirVidaDaEspada();
    }

    private void NormalizarVida()
    {
        vidaMaxima = Mathf.Max(1, vidaMaxima);

        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        AtualizarTextoDurabilidade(false);
    }

    private void AtualizarTextoDurabilidade(bool criarSeNecessario = false)
    {
        ResolverPopup();
        var classe = ObterConfiguracaoDoPopup();
        TMP_Text atual = classe?.textoDurabilidadeAtual != null ? classe.textoDurabilidadeAtual : textoValorAtual;
        TMP_Text total = classe?.textoDurabilidadeMaxima != null ? classe.textoDurabilidadeMaxima : textoValorTotal;
        if (atual != null) atual.text = vidaAtual.ToString("0.##", CulturaDurabilidade);
        if (total != null) total.text = vidaMaxima.ToString("0.##", CulturaDurabilidade);
    }

    private bool PodeAplicarDanoAgora(GameObject alvo)
    {
        if (cooldownDanoMesmoAlvo <= 0f) return true;
        int idAlvo = ObterIdCooldown(alvo);
        if (proximoDanoPermitidoPorAlvo.TryGetValue(idAlvo, out float proximoTempo) && Time.time < proximoTempo)
            return false;
        proximoDanoPermitidoPorAlvo[idAlvo] = Time.time + cooldownDanoMesmoAlvo;
        return true;
    }

    private int ObterIdCooldown(GameObject alvo)
    {
        if (alvo == null) return 0;
        Rigidbody rb = alvo.GetComponentInParent<Rigidbody>();
        if (rb != null) return rb.GetInstanceID();
        return alvo.transform.root != null ? alvo.transform.root.gameObject.GetInstanceID() : alvo.GetInstanceID();
    }

    private bool EhParteDaPropriaEspada(GameObject obj)
    {
        if (obj == null)
            return false;

        Transform t = obj.transform;
        return t == transform || t.IsChildOf(transform);
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
            if (grabInteractable.interactorsSelecting[i] is XRSocketInteractor) continue;
            Transform interactorTransform = ObterTransformInteractor(grabInteractable.interactorsSelecting[i]);
            Transform player = EncontrarPlayerDonoAPartirDoTransform(interactorTransform);

            if (player == null)
                continue;

            donoAtualPlayer = player;
            return;
        }
    }

    private bool TagEhAlvoValido(GameObject obj, out GameObject alvoResolvido)
    {
        alvoResolvido = null;

        if (obj == null || tagsAlvoDano == null || tagsAlvoDano.Length == 0)
            return false;

        Transform alvoComTag = EncontrarTransformComTagAlvo(obj.transform);
        if (alvoComTag != null)
        {
            alvoResolvido = alvoComTag.gameObject;
            return true;
        }

        Rigidbody rb = obj.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            alvoComTag = EncontrarTransformComTagAlvo(rb.transform);
            if (alvoComTag != null)
            {
                alvoResolvido = alvoComTag.gameObject;
                return true;
            }
        }

        if (obj.transform.root != null)
        {
            alvoComTag = EncontrarTransformComTagAlvo(obj.transform.root);
            if (alvoComTag != null)
            {
                alvoResolvido = alvoComTag.gameObject;
                return true;
            }
        }

        return false;
    }

    private Transform EncontrarTransformComTagAlvo(Transform origem)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (TagConfigurada(atual.gameObject))
                return atual;

            atual = atual.parent;
        }

        return null;
    }

    private bool TagConfigurada(GameObject obj)
    {
        if (obj == null || tagsAlvoDano == null)
            return false;

        string tagObjeto = obj.tag;
        for (int i = 0; i < tagsAlvoDano.Length; i++)
        {
            string tagPermitida = tagsAlvoDano[i];
            if (string.IsNullOrWhiteSpace(tagPermitida))
                continue;

            if (string.Equals(tagObjeto, tagPermitida.Trim(), StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private bool PertenceAoDonoAtual(GameObject obj)
    {
        if (donoAtualPlayer == null || obj == null)
            return false;

        Transform alvo = obj.transform;
        if (alvo == donoAtualPlayer || alvo.IsChildOf(donoAtualPlayer))
            return true;

        Transform playerDoAlvo = EncontrarPlayerDonoAPartirDoTransform(alvo);
        return playerDoAlvo == donoAtualPlayer;
    }

    private bool EstaAcopladoAoMesmoDono(GameObject obj)
    {
        if (donoAtualPlayer == null || obj == null)
            return false;

        if (InteractableEstaSelecionadoPeloDono(obj.GetComponentInParent<XRGrabInteractable>()))
            return true;

        XRGrabInteractable[] interactablesFilhos = obj.GetComponentsInChildren<XRGrabInteractable>(true);
        for (int i = 0; i < interactablesFilhos.Length; i++)
        {
            if (InteractableEstaSelecionadoPeloDono(interactablesFilhos[i]))
                return true;
        }

        return false;
    }

    private bool PertenceAoDonoRecente(GameObject obj)
    {
        if (!DeveIgnorarUltimoDono() || obj == null)
            return false;

        Transform alvo = obj.transform;
        if (alvo == ultimoDonoPlayer || alvo.IsChildOf(ultimoDonoPlayer))
            return true;

        Transform playerDoAlvo = EncontrarPlayerDonoAPartirDoTransform(alvo);
        return playerDoAlvo == ultimoDonoPlayer;
    }

    private bool EstaAcopladoAoDonoRecente(GameObject obj)
    {
        if (!DeveIgnorarUltimoDono() || obj == null)
            return false;

        if (InteractableEstaSelecionadoPeloDono(obj.GetComponentInParent<XRGrabInteractable>(), ultimoDonoPlayer))
            return true;

        XRGrabInteractable[] interactablesFilhos = obj.GetComponentsInChildren<XRGrabInteractable>(true);
        for (int i = 0; i < interactablesFilhos.Length; i++)
        {
            if (InteractableEstaSelecionadoPeloDono(interactablesFilhos[i], ultimoDonoPlayer))
                return true;
        }

        return false;
    }

    private bool DeveIgnorarUltimoDono()
    {
        return ultimoDonoPlayer != null && Time.time <= ignorarUltimoDonoAte;
    }

    private bool InteractableEstaSelecionadoPeloDono(XRGrabInteractable interactable)
    {
        return InteractableEstaSelecionadoPeloDono(interactable, donoAtualPlayer);
    }

    private bool InteractableEstaSelecionadoPeloDono(XRGrabInteractable interactable, Transform dono)
    {
        if (interactable == null || interactable == grabInteractable || dono == null)
            return false;

        for (int i = 0; i < interactable.interactorsSelecting.Count; i++)
        {
            Transform interactorTransform = ObterTransformInteractor(interactable.interactorsSelecting[i]);
            Transform player = EncontrarPlayerDonoAPartirDoTransform(interactorTransform);

            if (player == dono)
                return true;
        }

        return false;
    }

    private bool TentarAplicarDano(GameObject alvo, int dano)
    {
        if (alvo == null || dano <= 0)
            return false;

        if (TentarChamarReceberDano(alvo, dano))
            return true;

        if (EhPlayer(alvo))
        {
            { }
            return false;
        }

        return false;
    }

    private bool TentarChamarReceberDano(GameObject alvo, int dano)
    {
        Component[] componentes = ColetarComponentesParaDano(alvo);
        for (int i = 0; i < componentes.Length; i++)
        {
            Component componente = componentes[i];
            if (componente == null || componente == this)
                continue;

            MethodInfo metodoComOrigem = componente.GetType().GetMethod(
                "ReceberDano",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(int), typeof(GameObject) },
                null);

            if (metodoComOrigem != null)
            {
                try
                {
                    metodoComOrigem.Invoke(componente, new object[] { dano, gameObject });
                    return true;
                }
                catch (Exception)
                {
                    { }
                    return false;
                }
            }

            MethodInfo metodo = componente.GetType().GetMethod(
                "ReceberDano",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(int) },
                null);

            if (metodo == null)
                continue;

            try
            {
                metodo.Invoke(componente, new object[] { dano });
                return true;
            }
            catch (Exception)
            {
                { }
                return false;
            }
        }

        return false;
    }

    private Component[] ColetarComponentesParaDano(GameObject alvo)
    {
        List<Component> componentes = new();
        HashSet<Component> visitados = new();

        AdicionarComponentes(alvo.GetComponents<Component>(), componentes, visitados);
        AdicionarComponentes(alvo.GetComponentsInParent<Component>(true), componentes, visitados);
        AdicionarComponentes(alvo.GetComponentsInChildren<Component>(true), componentes, visitados);

        Rigidbody rb = alvo.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            AdicionarComponentes(rb.GetComponents<Component>(), componentes, visitados);
            AdicionarComponentes(rb.GetComponentsInParent<Component>(true), componentes, visitados);
            AdicionarComponentes(rb.GetComponentsInChildren<Component>(true), componentes, visitados);
        }

        return componentes.ToArray();
    }

    private void AdicionarComponentes(Component[] origem, List<Component> destino, HashSet<Component> visitados)
    {
        if (origem == null)
            return;

        for (int i = 0; i < origem.Length; i++)
        {
            Component componente = origem[i];
            if (componente == null || !visitados.Add(componente))
                continue;

            destino.Add(componente);
        }
    }

    private bool EhPlayer(GameObject obj)
    {
        return EncontrarPlayerDonoAPartirDoTransform(obj != null ? obj.transform : null) != null;
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

    private void ReduzirVidaDaEspada()
    {
        AplicarDesgaste(desgastePorDanoCausado);
    }

    public void AplicarDesgasteCombate(float quantidade) => AplicarDesgaste(quantidade);

    private void AplicarDesgaste(double quantidade)
    {
        if (quebrada || double.IsNaN(quantidade) || double.IsInfinity(quantidade) || quantidade < 0d ||
            EstadoItemInventario.EstaNoInventario(this)) return;
        if (vidaAtual != ultimaVidaPublicada) vidaPrecisa = vidaAtual;
        vidaPrecisa = Math.Max(0d, vidaPrecisa - quantidade);
        vidaAtual = (float)vidaPrecisa;
        ultimaVidaPublicada = vidaAtual;
        AtualizarTextoDurabilidade();
        if (vidaAtual <= 0f) QuebrarEspada();
    }

    private void QuebrarEspada()
    {
        if (quebrada)
            return;

        quebrada = true;
        vidaAtual = 0;
        AtualizarTextoDurabilidade(true);
        TocarSomQuebra();

        FecharPopup();
        if (!DestruirAoQuebrar)
            return;

        GetComponent<ItemPersistente>()?.RegistrarConsumoPermanente();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void TocarSomHit()
    {
        if (Time.time < proximoSomHitPermitido)
            return;

        proximoSomHitPermitido = Time.time + cooldownSomHit;

        if (audioSource != null && somHit != null)
            audioSource.PlayOneShot(somHit, volumeHit);
    }

    private void TocarSomQuebra()
    {
        if (somQuebra != null)
            AudioSource.PlayClipAtPoint(somQuebra, transform.position, volumeQuebra);
    }

    [Serializable]
    public class ConfiguracaoClasseEspada : AtributosManoplas.ConfiguracaoClasse
    {
        [HideInInspector, Min(0), InspectorName("Ataque Mínimo")] public int danoMinimo = 2;
        [HideInInspector, Min(0), InspectorName("Ataque Máximo")] public int danoMaximo = 5;
        [HideInInspector] public TMP_Text textoAtaque;
        [HideInInspector, Min(1), InspectorName("Durabilidade Mínima")] public int vidaMinima = 50;
        [HideInInspector, Min(1), InspectorName("Durabilidade Máxima")] public int vidaMaxima = 100;
        [HideInInspector] public TMP_Text textoDurabilidadeAtual;
        [HideInInspector] public TMP_Text textoDurabilidadeMaxima;
    }

    [Serializable]
    public class EstadoEspada
    {
        public int versao = 1;
        public bool gerado;
        public ClasseManopla classe;
        public AfinidadeDivinaManopla afinidade;
        public int dano;
        public bool restricaoDefinida;
        public TipoAtributoManopla atributoExigido;
        public int requisitoExigido;
        public float vidaMaxima;
        public List<AtributosManoplas.ValorAtributo> atributos = new List<AtributosManoplas.ValorAtributo>();
    }

    [Header("Identidade atual do popup")]
    [SerializeField] private GameObject prefabObjeto;
    [SerializeField, TextArea(3, 8)] private string descricao;
    [Header("Classes: chances independentes; zero desativa")]
    [SerializeField] private List<ConfiguracaoClasseEspada> classes = CriarClassesPadrao();
    [SerializeField, HideInInspector] private EstadoEspada resultado;
    private bool resultadoDestaInstancia;
    // Preserva exatamente os extras antigos durante a verificacao transacional do save.
    private string estadoJsonAnteriorSemRestricao;
    public bool Gerado => resultado != null && resultado.gerado && resultadoDestaInstancia;
    public int DanoBase => danoEspada;
    public ClasseManopla Classe => Gerado ? resultado.classe : ClasseManopla.Comum;
    public AfinidadeDivinaManopla Afinidade => Gerado ? resultado.afinidade : AfinidadeDivinaManopla.Nenhuma;

    private double vidaPrecisa;
    private float ultimaVidaPublicada = float.NaN;

    [Header("Popup existente: sem botao de equipar")]
    [SerializeField] private GameObject popup;
    [SerializeField] private TMP_Text textoNome;
    [SerializeField] private TMP_Text textoClasse;
    [SerializeField] private TMP_Text textoDescricao;
    [Header("Cinco linhas: nome e valor; ordem de cima para baixo")]
    [SerializeField] private List<LinhaAtributoEspada> textosValores = CriarLinhasPopup();
    [SerializeField] private Camera cameraJogador;
    [SerializeField] private Vector3 posicaoPopupNaCamera = new Vector3(0f, 0f, 1.5f);
    [SerializeField] private Vector3 rotacaoPopupNaCamera;
    [SerializeField, Min(0f)] private float espacamentoHorizontalPopups = 0.45f;
    [Tooltip("Segundos que o popup permanece aberto apos o laser entrar. Segurar a espada fecha imediatamente.")]
    [Min(0f)] public float tempoParaFecharPopup = 5f;
    private Coroutine fechamentoPopup;
    private Transform paiOriginalPopup;
    private Vector3 posicaoOriginalPopup, escalaOriginalPopup, escalaMundoPopup;
    private Quaternion rotacaoOriginalPopup;
    private bool posePopupCapturada, popupIsolado;
    private GameObject raizPopup;
    private static readonly List<Espada> espadasAtivas = new List<Espada>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparRegistroArmas() => espadasAtivas.Clear();

    [Serializable]
    public class LinhaAtributoEspada
    {
        public TMP_Text textoNome;
        public TMP_Text textoValor;
    }

    private static List<LinhaAtributoEspada> CriarLinhasPopup()
    {
        var linhas = new List<LinhaAtributoEspada>();
        for (int i = 0; i < 5; i++) linhas.Add(new LinhaAtributoEspada());
        return linhas;
    }

    private static List<ConfiguracaoClasseEspada> CriarClassesPadrao()
    {
        var configuracoes = new List<ConfiguracaoClasseEspada>();
        int[] min = {0, 1, 2, 3, 4, 6, 14, 14};
        int[] max = {1, 2, 3, 4, 5, 8, 14, 14};
        for (int i = 0; i < 8; i++)
        {
            var c = new ConfiguracaoClasseEspada { classe = (ClasseManopla)i,
                chance = i == 0 ? 70f : i == 1 ? 25f : i == 2 ? 5f : 0f,
                quantidadeMinima = min[i], quantidadeMaxima = max[i] };
            foreach (TipoAtributoManopla tipo in Enum.GetValues(typeof(TipoAtributoManopla)))
                if (tipo != TipoAtributoManopla.Critico && tipo != TipoAtributoManopla.Defesa)
                    c.atributos.Add(new AtributosManoplas.FaixaAtributo { atributo = tipo, minimo = 1, maximo = 5 });
            configuracoes.Add(c);
        }
        return configuracoes;
    }

    private ConfiguracaoClasseEspada ObterConfiguracaoDoPopup()
    {
        if (!Gerado || classes == null) return null;
        return classes.Find(classe => classe != null && classe.classe == resultado.classe);
    }

    public bool GarantirGeracao()
    {
        if (Gerado || migracaoLegadaPendente) return true;
        if (classes == null) classes = CriarClassesPadrao();
        var ativas = new List<ConfiguracaoClasseEspada>();
        var vistas = new HashSet<ClasseManopla>();
        foreach (var c in classes)
        {
            if (c == null || c.chance <= 0f) continue;
            if (!ConfiguracaoValida(c) || !vistas.Add(c.classe) ||
                !FaixaSorteioValida(c.danoMinimo, c.danoMaximo, 0) ||
                !FaixaSorteioValida(c.vidaMinima, c.vidaMaxima, 1))
            { Debug.LogError("[Espada] Configuracao de classe/atributos invalida.", this); return false; }
            ativas.Add(c);
        }
        if (ativas.Count == 0)
        { Debug.LogError("[Espada] Ative uma classe com chance maior que zero.", this); return false; }
        var escolhida = Flecha.SortearClassePonderada(ativas.ToArray());
        var novo = new EstadoEspada { gerado = true, classe = escolhida.classe,
            afinidade = AtributosManoplas.SortearAfinidade(escolhida.classe, () => UnityEngine.Random.value),
            dano = UnityEngine.Random.Range(escolhida.danoMinimo, escolhida.danoMaximo + 1),
            vidaMaxima = UnityEngine.Random.Range(escolhida.vidaMinima, escolhida.vidaMaxima + 1) };
        if (!DefinirRestricaoNova(novo)) return false;
        novo.atributos = SortearValoresAtributos(escolhida.atributos, escolhida.quantidadeMinima, escolhida.quantidadeMaxima);
        resultado = novo; resultadoDestaInstancia = true; danoEspada = novo.dano; vidaMaxima = novo.vidaMaxima; vidaAtual = vidaMaxima;
        ultimaVidaPublicada = float.NaN;
        AtualizarPopup();
        return true;
    }

    internal static List<AtributosManoplas.ValorAtributo> SortearValoresAtributos(
        List<AtributosManoplas.FaixaAtributo> faixas, int minimo, int maximo)
    {
        var valores = new List<AtributosManoplas.ValorAtributo>();
        var pool = new List<AtributosManoplas.FaixaAtributo>(faixas);
        int quantidade = UnityEngine.Random.Range(minimo, maximo + 1);
        for (int i = 0; i < quantidade; i++)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            var faixa = pool[index]; pool.RemoveAt(index);
            valores.Add(new AtributosManoplas.ValorAtributo { atributo = faixa.atributo,
                valor = UnityEngine.Random.Range(faixa.minimo, faixa.maximo + 1) });
        }
        return valores;
    }

    protected virtual bool DefinirRestricaoNova(EstadoEspada estado)
    {
        if (!Enum.IsDefined(typeof(TipoAtributoManopla), atributoRestricao) || valorMinimoParaUsar < 0)
        { Debug.LogError("[Espada] Restricao invalida.", this); return false; }
        estado.restricaoDefinida = true;
        estado.atributoExigido = atributoRestricao;
        estado.requisitoExigido = valorMinimoParaUsar;
        return true;
    }

    internal static bool FaixaSorteioValida(int minimo, int maximo, int piso)
    {
        return minimo >= piso && maximo >= minimo && maximo < int.MaxValue;
    }

    internal static bool ConfiguracaoValida(ConfiguracaoClasseEspada c)
    {
        if (c == null || !Enum.IsDefined(typeof(ClasseManopla), c.classe) ||
            float.IsNaN(c.chance) || float.IsInfinity(c.chance) ||
            c.atributos == null || c.quantidadeMinima < 0 || c.quantidadeMaxima < c.quantidadeMinima ||
            c.quantidadeMaxima > c.atributos.Count) return false;
        var tipos = new HashSet<TipoAtributoManopla>();
        foreach (var f in c.atributos)
            if (f == null || !Enum.IsDefined(typeof(TipoAtributoManopla), f.atributo) || !tipos.Add(f.atributo) ||
                f.minimo < 0 || f.maximo < f.minimo || f.maximo == int.MaxValue) return false;
        return true;
    }

    public string ExportarEstadoJson() => Gerado ? (!resultado.restricaoDefinida && estadoJsonAnteriorSemRestricao != null ? estadoJsonAnteriorSemRestricao : JsonUtility.ToJson(resultado)) : null;
    public static EstadoEspada LerEValidarEstadoJson(string json)
    {
        var estado = JsonUtility.FromJson<EstadoEspada>(json);
        if (estado == null || estado.versao != 1 || !estado.gerado || estado.dano < 0 ||
            float.IsNaN(estado.vidaMaxima) || float.IsInfinity(estado.vidaMaxima) || estado.vidaMaxima <= 0f ||
            !Enum.IsDefined(typeof(ClasseManopla), estado.classe) ||
            !Enum.IsDefined(typeof(AfinidadeDivinaManopla), estado.afinidade) || estado.atributos == null)
            throw new System.IO.InvalidDataException("Estado de espada invalido.");
        if (estado.restricaoDefinida && (!Enum.IsDefined(typeof(TipoAtributoManopla), estado.atributoExigido) || estado.requisitoExigido < 0))
            throw new System.IO.InvalidDataException("Restricao de espada invalida.");
        var tipos = new HashSet<TipoAtributoManopla>();
        foreach (var v in estado.atributos)
            if (v == null || v.valor < 0 || !Enum.IsDefined(typeof(TipoAtributoManopla), v.atributo) || !tipos.Add(v.atributo))
                throw new System.IO.InvalidDataException("Atributo de espada invalido.");
        return estado;
    }
    public void RestaurarEstadoJson(string json)
    {
        resultado = LerEValidarEstadoJson(json);
        estadoJsonAnteriorSemRestricao = resultado.restricaoDefinida ? null : json;
        resultadoDestaInstancia = true;
        migracaoLegadaPendente = false;
        danoEspada = resultado.dano; vidaMaxima = resultado.vidaMaxima;
        ultimaVidaPublicada = float.NaN;
        AtualizarPopup();
    }

    // Save anterior a este sistema: conserva dano/durabilidade existentes, sem inventar novos bonus.
    public void RestaurarEstadoLegado(float atual, float maxima)
    {
        CapturarConfiguracaoLegada();
        resultado = null;
        migracaoLegadaPendente = true;
        danoEspada = danoLegadoConfigurado;
        vidaMaxima = maxima > 0f ? maxima : vidaLegadaConfigurada;
        vidaAtual = atual >= 0f ? Mathf.Clamp(atual, 0f, vidaMaxima) : vidaMaxima;
        ultimaVidaPublicada = float.NaN;
        if (isActiveAndEnabled) StartCoroutine(FinalizarMigracaoLegada());
        AtualizarTextoDurabilidade();
    }
    private IEnumerator FinalizarMigracaoLegada()
    {
        yield return null; // Confirma o snapshot antigo antes de publicar os extras novos.
        if (!migracaoLegadaPendente) yield break;
        migracaoLegadaPendente = false;
        resultado = new EstadoEspada { gerado = true, dano = danoEspada, vidaMaxima = vidaMaxima,
            restricaoDefinida = true, atributoExigido = atributoRestricao, requisitoExigido = valorMinimoParaUsar };
        resultadoDestaInstancia = true;
        AtualizarPopup();
    }
    private int danoLegadoConfigurado;
    private float vidaLegadaConfigurada;
    private bool configuracaoLegadaCapturada, migracaoLegadaPendente;
    private void CapturarConfiguracaoLegada()
    {
        if (configuracaoLegadaCapturada) return;
        danoLegadoConfigurado = danoEspada;
        vidaLegadaConfigurada = Mathf.Max(1f, vidaMaxima);
        configuracaoLegadaCapturada = true;
    }

    public int ObterValor(TipoAtributoManopla tipo)
    {
        if (!Gerado || quebrada || vidaAtual <= 0f) return 0;
        foreach (var v in resultado.atributos) if (v.atributo == tipo) return v.valor;
        return 0;
    }
    public static int ObterBonusEmpunhado(StatusPlayer player, TipoAtributoManopla tipo)
    {
        if (player == null) return 0;
        long total = 0;
        foreach (var espada in espadasAtivas)
            if (espada != null && espada.isActiveAndEnabled && !EstadoItemInventario.EstaNoInventario(espada) &&
                espada.GetStatusPlayerDonoAtual() == player) total += espada.ObterValor(tipo);
        return (int)Math.Min(int.MaxValue, total);
    }

    private static float ValorPositivoFinito(float valor) => float.IsNaN(valor) || float.IsInfinity(valor) ? 0f : Mathf.Max(0f, valor);

    private void ResolverPopup()
    {
        if (popup == null) { var t = transform.Find("PopUp"); if (t != null) popup = t.gameObject; }
        if (popup == null) return;
        if (grupoRestricao == null)
            foreach (var t in popup.GetComponentsInChildren<Transform>(true))
                if (t != popup.transform && t.name.Equals("Restricao", StringComparison.OrdinalIgnoreCase) &&
                    t.GetComponent<TMP_Text>() == null)
                { grupoRestricao = t.gameObject; break; }
        foreach (var t in popup.GetComponentsInChildren<TMP_Text>(true))
        {
            string n = t.name.ToLowerInvariant().Replace(" ", "");
            if (textoNome == null && n == "nome") textoNome = t;
            if (textoClasse == null && (n == "classe" || n == "valorclasse")) textoClasse = t;
            if (n == "classe(1)") textoClasse = t;
            if (textoDescricao == null && n == "descricao") textoDescricao = t;
            if (textoValorRestricao == null && t.transform.parent != null &&
                t.transform.parent.name.Equals("Restricao", StringComparison.OrdinalIgnoreCase) && n == "numero") textoValorRestricao = t;
        }
    }

    public void AtualizarPopup()
    {
        ResolverPopup(); AtualizarTextoDurabilidade();
        var playerPopup = GetStatusPlayerDonoAtual();
        if (cameraJogador == null) cameraJogador = Camera.main;
        if (playerPopup == null && cameraJogador != null) playerPopup = cameraJogador.GetComponentInParent<StatusPlayer>();
        AtualizarRestricaoPopup(playerPopup);
        if (!Gerado) return;
        TMP_Text ataquePorClasse = ObterConfiguracaoDoPopup()?.textoAtaque;
        TMP_Text ataque = ataquePorClasse != null ? ataquePorClasse : textoAtaqueSorteado;
        if (ataque != null) ataque.text = danoEspada.ToString(CultureInfo.InvariantCulture);
        if (textoNome != null) textoNome.text = prefabObjeto != null ? prefabObjeto.name : GetComponent<ItemPersistente>()?.PrefabReferencia != null ?
            GetComponent<ItemPersistente>().PrefabReferencia.name : name.Replace("(Clone)", "").Trim();
        if (textoDescricao != null) textoDescricao.text = descricao ?? "";
        if (textoClasse != null) textoClasse.text = resultado.classe + (resultado.afinidade == AfinidadeDivinaManopla.Nenhuma ? "" : " - " + resultado.afinidade);
        if (textosValores == null) textosValores = CriarLinhasPopup();
        for (int i = 0; i < textosValores.Count; i++)
        {
            var linha = textosValores[i]; if (linha == null) continue;
            var v = i < 5 && i < resultado.atributos.Count ? resultado.atributos[i] : null;
            if (linha.textoNome != null) linha.textoNome.text = v == null ? "" : v.atributo == TipoAtributoManopla.Mana ? "MP" : v.atributo.ToString();
            if (linha.textoValor != null) linha.textoValor.text = v == null ? "" : "+" + v.valor;
        }
    }
    private void AtualizarRestricaoPopup(StatusPlayer player)
    {
        bool mostrar = player != null && !PodeUsar(player);
        if (textoRotuloRestricao != null) textoRotuloRestricao.text = rotuloRestricaoPopup ?? "";
        if (textoAtributoRestricao != null) textoAtributoRestricao.text = AtributoExigido == TipoAtributoManopla.Mana ? "MP" : AtributoExigido.ToString();
        if (textoValorRestricao != null) textoValorRestricao.text = RequisitoExigido.ToString(CultureInfo.InvariantCulture);
        if (textoMensagemRestricao != null)
            textoMensagemRestricao.text = mostrar ? mensagemAbaixoMinimo ?? "" : "";
        DefinirVisibilidadeRestricao(mostrar);
    }

    private void DefinirVisibilidadeRestricao(bool mostrar)
    {
        // Nunca desativar o proprio popup caso uma referencia incorreta seja atribuida.
        if (grupoRestricao != null && popup != null && grupoRestricao != popup &&
            grupoRestricao.transform.IsChildOf(popup.transform) && grupoRestricao.activeSelf != mostrar)
            grupoRestricao.SetActive(mostrar);
        // Fallback para modelos em desenvolvimento sem um grupo pai configurado.
        if (textoRotuloRestricao != null) textoRotuloRestricao.enabled = mostrar;
        if (textoAtributoRestricao != null) textoAtributoRestricao.enabled = mostrar;
        if (textoValorRestricao != null) textoValorRestricao.enabled = mostrar;
        if (textoMensagemRestricao != null) textoMensagemRestricao.enabled = mostrar;
    }

    internal static bool EhLaserDoPopup(IXRHoverInteractor interactor)
    {
        if (interactor is XRGazeInteractor) return false;
        if (interactor is XRRayInteractor) return true;
        // O NearFar combina mao e laser. A curva ativa exclui o hover por proximidade.
        return interactor is NearFarInteractor &&
            interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.ICurveInteractionDataProvider curva && curva.isActive;
    }

    private bool EstaSendoSegurada()
    {
        if (grabInteractable == null) return false;
        foreach (var interactor in grabInteractable.interactorsSelecting)
            if (interactor != null && !(interactor is XRSocketInteractor)) return true;
        return false;
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (args == null || !EhLaserDoPopup(args.interactorObject) || EstaSendoSegurada() ||
            !isActiveAndEnabled || quebrada || !GarantirGeracao()) return;
        ResolverPopup(); if (popup == null) return;
        CancelarFechamentoPopup(); PrepararPopup(); popup.SetActive(true);
        foreach (var c in popup.GetComponentsInChildren<Canvas>(true)) c.enabled = true;
        foreach (var g in popup.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.enabled = true;
        AtualizarPopup(); PosicionarPopup();
        fechamentoPopup = StartCoroutine(FecharPopupDepois());
    }
    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (args == null || !EhLaserDoPopup(args.interactorObject)) return;
        if (EstaSendoSegurada()) { FecharPopup(); return; }
        // Sair do laser nao reinicia o prazo iniciado na abertura.
    }
    private IEnumerator FecharPopupDepois() { yield return new WaitForSeconds(ValorPositivoFinito(tempoParaFecharPopup)); fechamentoPopup = null; FecharPopup(); }
    private void CancelarFechamentoPopup() { if (fechamentoPopup != null) StopCoroutine(fechamentoPopup); fechamentoPopup = null; }
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
    protected void OnDestroy()
    {
        FecharPopup(); espadasAtivas.Remove(this);
        if (raizPopup != null) Destroy(raizPopup);
    }
}
