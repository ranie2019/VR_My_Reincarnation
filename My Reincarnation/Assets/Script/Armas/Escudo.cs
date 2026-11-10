using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class Escudo : MonoBehaviour
{
    [Serializable]
    private class ClasseEscudo : AtributosManoplas.ConfiguracaoClasse
    {
        [Min(1)] public int defesaMinima;
        [Min(1)] public int defesaMaxima;
        public TMP_Text textoDefesa;
        [Min(1)] public int durabilidadeMinima;
        [Min(1)] public int durabilidadeMaxima;
        public TMP_Text textoDurabilidadeAtual;
        public TMP_Text textoDurabilidadeMaxima;
    }

    [Serializable]
    internal class EstadoSorteio
    {
        internal const string Formato = "my-reincarnation.escudo.v1";
        public string formato = Formato;
        public string anteriores;
        public ClasseManopla classe;
        public int defesa;
        public int durabilidadeMaxima;
        public List<AtributosManoplas.ValorAtributo> atributos;
    }

    [SerializeField] private ClasseEscudo[] classes =
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

    private static ClasseEscudo CriarClassePadrao(ClasseManopla classe, float chance = 0)
    {
        var configuracao = new ClasseEscudo
        {
            classe = classe,
            chance = chance,
            defesaMinima = 3 + (int)classe,
            defesaMaxima = 5 + (int)classe,
            quantidadeMinima = Mathf.Min((int)classe, 4),
            quantidadeMaxima = Mathf.Min((int)classe + 1, 5)
        };
        foreach (TipoAtributoManopla atributo in Enum.GetValues(typeof(TipoAtributoManopla)))
            if (AtributoValido(atributo))
                configuracao.atributos.Add(new AtributosManoplas.FaixaAtributo { atributo = atributo });
        return configuracao;
    }

    [SerializeField] private List<Espada.LinhaAtributoEspada> textosValores = new() { new(), new(), new(), new(), new() };
    [SerializeField] private TipoAtributoManopla atributoRestricao = TipoAtributoManopla.Constituicao;
    [SerializeField, Min(0)] private int valorMinimoParaUsar;
    [SerializeField] private GameObject grupoRestricao;
    [SerializeField] private TMP_Text textoAtributoRestricao, textoValorRestricao, textoMensagemRestricao;

    private EstadoSorteio sorteio;
    private bool durabilidadeRestaurada;
    private static readonly HashSet<Escudo> escudosEmpunhados = new();
    private StatusPlayer jogadorComBonus;
    private TMP_Text resumoPopup;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparEscudosEmpunhados() => escudosEmpunhados.Clear();

    [Header("Popup do escudo: laser, sem botao equipar")]
    [SerializeField] private GameObject popup;
    [SerializeField] private GameObject prefabObjeto;
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

    [Header("Vida / Durabilidade")]
    [SerializeField] private int vidaMaxima = 30;
    [SerializeField] private int vidaAtual = 30;
    [SerializeField] private bool destruirQuandoVidaZerar = true;
    [SerializeField, Min(1)] private int desgastePorDano = 1;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somBloqueio;
    [SerializeField] private AudioClip somQuebrar;
    [SerializeField] private float volumeBloqueio = 1f;
    [SerializeField] private float volumeQuebrar = 1f;

    private Transform donoAtualPlayer;
    private XRGrabInteractable grabInteractable;
    private bool quebrado;
    private readonly Dictionary<Collider, (Component fonte, int quantidade)> contatosDeDano = new();
    private readonly List<Collider> contatosInvalidos = new();
    private static readonly CultureInfo CulturaDurabilidade = CultureInfo.GetCultureInfo("pt-BR");

    public int VidaAtual => vidaAtual;
    public int VidaMaxima => vidaMaxima;
    public bool Quebrado => quebrado || vidaAtual <= 0;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        ResolverPopupEscudo();
        NormalizarVida();
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
            grabInteractable.hoverEntered.AddListener(OnHoverPopupEscudo);
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
            grabInteractable.hoverEntered.RemoveListener(OnHoverPopupEscudo);
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        FecharPopup();
        DefinirJogadorComBonus(null);
        donoAtualPlayer = null;
        contatosDeDano.Clear();
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
            }
        desgastePorDano = Mathf.Max(1, desgastePorDano);
        volumeBloqueio = Mathf.Max(0f, volumeBloqueio);
        volumeQuebrar = Mathf.Max(0f, volumeQuebrar);
    }

    private void LateUpdate()
    {
        AtualizarBonusEmpunhado();
        if (!popupIsolado || popup == null) return;
        if (PopupBloqueadoPorSelecao() || Quebrado) FecharPopup();
        else if (popup.activeInHierarchy) PosicionarPopup();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null)
            return;

        ProcessarBloqueio(collision.collider, AudioColisaoFiltro.PodeTocarSomDeColisao(collision));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        ProcessarBloqueio(other, false);
    }

    private void OnCollisionExit(Collision collision) => EncerrarContato(collision.collider);
    private void OnTriggerExit(Collider other) => EncerrarContato(other);

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args == null || args.interactorObject == null) return;
        if (args.interactorObject is XRSocketInteractor) return;
        FecharPopup();
        Transform interactorTransform = ObterTransformInteractor(args.interactorObject);
        donoAtualPlayer = EncontrarPlayerDonoAPartirDoTransform(interactorTransform);
        AtualizarBonusEmpunhado();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        AtualizarDonoPelaSelecaoAtual();
        AtualizarBonusEmpunhado();
        if (donoAtualPlayer == null) contatosDeDano.Clear();
    }

    public bool EstaProtegendoPlayer(Transform player)
    {
        if (!PodeConcederBonus() || player == null)
            return false;

        return player == donoAtualPlayer || player.IsChildOf(donoAtualPlayer);
    }

    private void RegistrarImpacto(bool tocarSom)
    {
        GarantirSorteio();
        if (tocarSom)
            TocarSomBloqueio();
        ReduzirVidaDoEscudo();
    }

    internal static bool IgnorarNoAtaque(Collider contato, Transform alvo)
    {
        var escudo = contato != null ? contato.GetComponentInParent<Escudo>() : null;
        return escudo != null && !escudo.EstaProtegendoPlayer(alvo);
    }

    internal static bool TentarInterceptarGolpe(Collider contato, GameObject atacante, Transform alvo,
        float ataque, out float restante)
    {
        restante = ataque;
        var escudo = contato != null ? contato.GetComponentInParent<Escudo>() : null;
        if (escudo == null || !escudo.EstaProtegendoPlayer(alvo)) return false;

        var jogador = alvo.GetComponentInParent<StatusPlayer>() ?? alvo.GetComponentInChildren<StatusPlayer>(true);
        if (!float.IsFinite(ataque) || ataque <= 0f || atacante == null ||
            escudo.PertenceAoDonoAtual(atacante) || !escudo.PodeUsar(jogador))
            return true;

        escudo.GarantirSorteio();
        restante = Mathf.Max(0f, ataque - escudo.sorteio.defesa);
        if (restante < ataque)
            escudo.RegistrarImpacto(true);
        return true;
    }

    private void ProcessarBloqueio(Collider contato, bool tocarSom)
    {
        // Slime e Goblin confirmam o ataque no proprio fluxo de combate.
        if (!PodeConcederBonus() || !PodeUsar(jogadorComBonus) ||
            contato.GetComponentInParent<IDano>() is not Component componente ||
            componente is SlimeIA || componente is GoblinIA ||
            EstadoItemInventario.EstaNoInventario(componente))
            return;

        var fonte = (IDano)componente;
        var atacante = fonte.ObterDono();
        if (atacante == null || PertenceAoDonoAtual(atacante))
            return;

        float dano = fonte.ObterDano();
        if (float.IsFinite(dano) && dano > 0f && RegistrarContato(contato, componente))
            RegistrarImpacto(tocarSom);
    }

    private bool RegistrarContato(Collider contato, Component fonte)
    {
        bool primeiroDaFonte = true;
        foreach (var entrada in contatosDeDano)
        {
            Collider collider = entrada.Key;
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || entrada.Value.fonte == null)
                contatosInvalidos.Add(collider);
            else if (entrada.Value.fonte == fonte)
                primeiroDaFonte = false;
        }

        foreach (var invalido in contatosInvalidos)
            contatosDeDano.Remove(invalido);
        contatosInvalidos.Clear();

        contatosDeDano.TryGetValue(contato, out var anterior);
        contatosDeDano[contato] = (fonte, anterior.quantidade + 1);
        return primeiroDaFonte;
    }

    private void EncerrarContato(Collider contato)
    {
        if (contato == null || !contatosDeDano.TryGetValue(contato, out var atual)) return;
        if (atual.quantidade <= 1) contatosDeDano.Remove(contato);
        else contatosDeDano[contato] = (atual.fonte, atual.quantidade - 1);
    }

    private void NormalizarVida()
    {
        vidaMaxima = Mathf.Max(1, vidaMaxima);

        if (vidaAtual <= 0 && !durabilidadeRestaurada)
            vidaAtual = vidaMaxima;

        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
    }

    public void AtualizarDurabilidadeVisual()
    {
        if (popupIsolado && popup != null && popup.activeInHierarchy)
            AtualizarPopup();
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

    private bool PertenceAoDonoAtual(GameObject objeto) =>
        donoAtualPlayer != null && objeto != null && objeto.transform.IsChildOf(donoAtualPlayer);

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

    private void ReduzirVidaDoEscudo()
    {
        if (Quebrado)
            return;

        vidaAtual = Mathf.Max(0, vidaAtual - Mathf.Max(1, desgastePorDano));
        AtualizarDurabilidadeVisual();

        if (vidaAtual <= 0)
            QuebrarEscudo();
    }

    private void QuebrarEscudo()
    {
        if (quebrado)
            return;

        quebrado = true;
        DefinirJogadorComBonus(null);
        FecharPopup();
        vidaAtual = 0;
        AtualizarDurabilidadeVisual();
        TocarSomQuebrar();

        if (destruirQuandoVidaZerar)
        {
            GetComponent<ItemPersistente>()?.RegistrarConsumoPermanente();
            Destroy(gameObject);
        }
    }

    private void TocarSomBloqueio()
    {
        if (audioSource != null && somBloqueio != null)
            audioSource.PlayOneShot(somBloqueio, volumeBloqueio);
    }

    private void TocarSomQuebrar()
    {
        if (somQuebrar != null)
            AudioSource.PlayClipAtPoint(somQuebrar, transform.position, volumeQuebrar);
    }
    private void ResolverPopupEscudo()
    {
        if (popup != null) return;
        var t = transform.Find("PopUp") ?? transform.Find("Popup");
        if (t != null) popup = t.gameObject;
    }

    private bool PopupBloqueadoPorSelecao()
    {
        if (grabInteractable == null) return false;
        foreach (var interactor in grabInteractable.interactorsSelecting)
            if (interactor != null && !(interactor is XRSocketInteractor)) return true;
        return false;
    }

    private void OnHoverPopupEscudo(HoverEnterEventArgs args)
    {
        if (args == null || !Espada.EhLaserDoPopup(args.interactorObject) || PopupBloqueadoPorSelecao() || Quebrado) return;
        ResolverPopupEscudo();
        if (!isActiveAndEnabled) return;
        if (popup == null) popup = Flecha.CriarPopupSimples(transform, "Popup do Escudo", out resumoPopup);
        GarantirSorteio();
        CancelarFechamentoPopup(); PrepararPopup(); popup.SetActive(true);
        foreach (var c in popup.GetComponentsInChildren<Canvas>(true)) c.enabled = true;
        foreach (var g in popup.GetComponentsInChildren<Graphic>(true)) g.enabled = true;
        AtualizarPopup(); PosicionarPopup();
        fechamentoPopup = StartCoroutine(FecharPopupDepois());
    }

    private TMP_Text BuscarTextoPopup(string caminho) => popup.transform.Find(caminho)?.GetComponent<TMP_Text>();

    public void AtualizarPopup()
    {
        if (popup == null || sorteio == null) return;

        textoNome ??= BuscarTextoPopup("Fundo/nome");
        textoClasse ??= BuscarTextoPopup("Fundo/Valor classe");
        textoDescricao ??= BuscarTextoPopup("Fundo/Descricao");
        string nome = prefabObjeto != null ? prefabObjeto.name : name.Replace("(Clone)", "").Trim();
        var jogador = (cameraJogador != null ? cameraJogador : Camera.main)?.GetComponentInParent<StatusPlayer>();
        bool restrito = !PodeUsar(jogador);
        if (resumoPopup != null)
        {
            var atributos = sorteio.atributos.ConvertAll(valor => $"{NomeAtributo(valor.atributo)} +{valor.valor}");
            string restricao = restrito ? $"Restrição: {NomeAtributo(atributoRestricao)} {valorMinimoParaUsar}\n" : "";
            resumoPopup.text = $"{nome}\n{sorteio.classe} · Defesa {sorteio.defesa}\nDurabilidade {vidaAtual}/{vidaMaxima}\n" +
                $"{string.Join(" · ", atributos)}\n{restricao}{descricao}";
            return;
        }

        if (textoNome != null) textoNome.text = nome;
        if (textoClasse != null) textoClasse.text = sorteio.classe.ToString();
        if (textoDescricao != null) textoDescricao.text = descricao ?? "";
        var classeSorteada = ObterConfiguracaoSorteada();
        var valorDefesa = classeSorteada?.textoDefesa ?? BuscarTextoPopup("Fundo/Defesa/Defesa Valor")
            ?? BuscarTextoPopup("Fundo/Ataque/Ataque Valor");
        var rotuloLegado = BuscarTextoPopup("Fundo/Ataque/Ataque Texto");
        if (rotuloLegado != null) rotuloLegado.text = "DEFESA:";
        if (classes != null)
            foreach (var classe in classes)
                if (classe?.textoDefesa != null && classe.textoDefesa != valorDefesa) classe.textoDefesa.text = "";
        if (valorDefesa != null) valorDefesa.text = sorteio.defesa.ToString();
        var durabilidadeAtual = classeSorteada?.textoDurabilidadeAtual ??
            BuscarTextoPopup("Fundo/Durabilidade/Durabilidade Atual");
        var durabilidadeMaxima = classeSorteada?.textoDurabilidadeMaxima ??
            BuscarTextoPopup("Fundo/Durabilidade/Durabilidade Limite");
        if (durabilidadeAtual != null) durabilidadeAtual.text = vidaAtual.ToString("N0", CulturaDurabilidade);
        if (durabilidadeMaxima != null) durabilidadeMaxima.text = vidaMaxima.ToString("N0", CulturaDurabilidade);

        for (int i = 0; i < 5; i++)
        {
            var linha = textosValores != null && i < textosValores.Count ? textosValores[i] : null;
            var raiz = popup.transform.Find("Fundo/" + i);
            var nomeAtributo = linha?.textoNome ?? raiz?.Find("Texto")?.GetComponent<TMP_Text>();
            var valorAtributo = linha?.textoValor ?? raiz?.Find("Numero")?.GetComponent<TMP_Text>();
            var valor = i < sorteio.atributos.Count ? sorteio.atributos[i] : null;
            if (raiz != null) raiz.gameObject.SetActive(valor != null);
            if (nomeAtributo != null) nomeAtributo.text = valor != null ? NomeAtributo(valor.atributo) : "";
            if (valorAtributo != null) valorAtributo.text = valor != null ? "+" + valor.valor : "";
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
            defesa = configuracao != null
                ? Flecha.SortearInteiro(configuracao.defesaMinima, configuracao.defesaMaxima, 1) : 3,
            atributos = Espada.SortearValoresAtributos(faixas, minimo, maximo)
        };
    }

    private void ObterFaixaDurabilidade(ClasseEscudo config, ClasseManopla classe, out int minimo, out int maximo)
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

    private ClasseEscudo ObterConfiguracaoSorteada() =>
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

        AjustarLimiteParaDurabilidadeAtual(vidaAtual);
    }

    private void AjustarLimiteParaDurabilidadeAtual(int atual)
    {
        // Saves antigos de cena podem ter apenas o valor atual ou um limite desatualizado.
        vidaMaxima = Mathf.Max(vidaMaxima, atual);
        if (sorteio != null)
            sorteio.durabilidadeMaxima = vidaMaxima;
    }

    internal string CapturarSorteioJson(string anteriores)
    {
        if (sorteio == null) return anteriores;
        if (sorteio.anteriores == null && anteriores?.Contains(EstadoSorteio.Formato) != true)
            sorteio.anteriores = anteriores;
        return JsonUtility.ToJson(sorteio);
    }

    internal static EstadoSorteio LerSorteioJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || !json.Contains(EstadoSorteio.Formato)) return null;

        var estado = JsonUtility.FromJson<EstadoSorteio>(json);
        var tipos = new HashSet<TipoAtributoManopla>();
        if (estado == null || estado.formato != EstadoSorteio.Formato ||
            !Enum.IsDefined(typeof(ClasseManopla), estado.classe) || estado.defesa < 1 || estado.durabilidadeMaxima < 1 ||
            estado.atributos == null || estado.atributos.Exists(valor => valor == null || valor.valor < 0 ||
                !AtributoValido(valor.atributo) || !tipos.Add(valor.atributo)))
            throw new System.IO.InvalidDataException("Sorteio do escudo inválido no save.");
        return estado;
    }

    internal void RestaurarSorteio(string json, float durabilidadeSalva)
    {
        sorteio = LerSorteioJson(json);
        durabilidadeRestaurada = durabilidadeSalva >= 0f;
        if (sorteio != null)
            vidaMaxima = sorteio.durabilidadeMaxima;
        if (durabilidadeRestaurada)
        {
            AjustarLimiteParaDurabilidadeAtual(Mathf.RoundToInt(durabilidadeSalva));
            quebrado = durabilidadeSalva <= 0f;
        }
    }

    internal Action CapturarSorteioParaCarga()
    {
        var estado = sorteio;
        bool restaurada = durabilidadeRestaurada;
        bool estavaQuebrado = quebrado;
        return () =>
        {
            sorteio = estado;
            durabilidadeRestaurada = restaurada;
            quebrado = estavaQuebrado;
        };
    }

    private bool PodeConcederBonus() => isActiveAndEnabled && !Quebrado &&
        donoAtualPlayer != null && PopupBloqueadoPorSelecao() && !EstadoItemInventario.EstaNoInventario(this);

    private void AtualizarBonusEmpunhado()
    {
        StatusPlayer jogador = PodeConcederBonus()
            ? donoAtualPlayer.GetComponentInChildren<StatusPlayer>(true)
            : null;
        if (jogador != null && sorteio == null) GarantirSorteio();
        DefinirJogadorComBonus(jogador);
    }

    private void DefinirJogadorComBonus(StatusPlayer jogador)
    {
        StatusPlayer anterior = jogadorComBonus;
        jogadorComBonus = jogador;
        if (jogador != null) escudosEmpunhados.Add(this);
        else escudosEmpunhados.Remove(this);
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
        foreach (var escudo in escudosEmpunhados)
            if (escudo != null && escudo.jogadorComBonus == jogador && escudo.PodeConcederBonus())
                total += escudo.ObterValorAtributo(atributo);
        return total;
    }

    private bool PodeUsar(StatusPlayer jogador)
    {
        if (valorMinimoParaUsar <= 0) return true;
        int bonusProprio = jogador != null && jogadorComBonus == jogador && PodeConcederBonus()
            ? ObterValorAtributo(atributoRestricao) : 0;
        return jogador != null && Espada.AtendeRestricaoAtributo(Espada.ObterAtributoTotal(jogador, atributoRestricao),
            bonusProprio, valorMinimoParaUsar);
    }

    private void OnDestroy()
    {
        DefinirJogadorComBonus(null);
        FecharPopup();
        if (raizPopup != null) Destroy(raizPopup);
    }
}
