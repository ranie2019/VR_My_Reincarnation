using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class Flecha : MonoBehaviour, IDano
{
    [SerializeField, HideInInspector] private float dano = 10f; // Base dos prefabs e saves anteriores.
    [SerializeField] private string[] tagsQueRecebemDano = { "Enemy", "Player" };
    [SerializeField] private bool causarDanoUmaVez = true;

    [SerializeField] private TipoAtributoManopla atributoRestricao = TipoAtributoManopla.Destreza;
    [SerializeField, Min(0)] private int valorMinimoParaUsar;

    [SerializeField] private string[] tagsIgnoradasColisao = { "Arch" };
    [SerializeField] private bool ignorarDonoDaFlecha = true;
    [SerializeField] private bool ignorarTerrain = true;

    [SerializeField] private Transform traseiraDirecao;
    [SerializeField] private Transform pontaDirecao;

    [Serializable]
    private class ClasseFlecha : AtributosManoplas.ConfiguracaoClasse
    {
        [Min(1)] public int usosMinimos = 1;
        [Min(1)] public int usosMaximos = 1;
        [Min(0)] public int ataqueMinimo;
        [Min(0)] public int ataqueMaximo;
        public TMP_Text textoAtaque;
    }

    [SerializeField] private ClasseFlecha[] classes = {
        new ClasseFlecha { classe = ClasseManopla.Comum, chance = 85 },
        new ClasseFlecha { classe = ClasseManopla.Incomum, chance = 12, usosMinimos = 2, usosMaximos = 3 },
        new ClasseFlecha { classe = ClasseManopla.Raro, chance = 3, usosMinimos = 3, usosMaximos = 4 },
        new ClasseFlecha { classe = ClasseManopla.Epico, usosMinimos = 4, usosMaximos = 5 },
        new ClasseFlecha { classe = ClasseManopla.Unico, usosMinimos = 5, usosMaximos = 6 },
        new ClasseFlecha { classe = ClasseManopla.Lendario, usosMinimos = 6, usosMaximos = 7 },
        new ClasseFlecha { classe = ClasseManopla.Mitico, usosMinimos = 7, usosMaximos = 8 },
        new ClasseFlecha { classe = ClasseManopla.Divino, usosMinimos = 8, usosMaximos = 9 }
    };

    [SerializeField] private GameObject popup;
    [SerializeField] private TMP_Text textoNome, textoClasse, textoUsosAtuais, textoUsosMaximos, textoDescricao;
    [SerializeField, HideInInspector] private TMP_Text textoAtaque;
    [SerializeField, HideInInspector] private bool faixasAtaqueConfiguradas;
    [SerializeField] private List<Espada.LinhaAtributoEspada> textosValores = new()
    {
        new(), new(), new(), new(), new()
    };
    [SerializeField] private GameObject grupoRestricao;
    [SerializeField] private TMP_Text textoAtributoRestricao, textoValorRestricao, textoMensagemRestricao;
    [SerializeField] private string nomeExibicao;
    [SerializeField, TextArea] private string descricao;
    [SerializeField, Min(0.1f)] private float tempoPopup = 5f;
    [SerializeField] private Vector3 posicaoPopupNaCamera = new Vector3(0f, 0f, 1.5f);
    [SerializeField] private Vector3 rotacaoPopupNaCamera;
    [SerializeField, Min(0f)] private float espacamentoPopups = 0.45f;

    [SerializeField] private AudioClip somColisao;
    [SerializeField] private float volumeSomColisao = 1f;

    [SerializeField] private GameObject efeitoAoCriar;
    [SerializeField] private bool parentarEfeitoCriacaoNaFlecha = true;
    [SerializeField] private GameObject efeitoAoColidir;
    [SerializeField] private float tempoDestruirEfeitoColisao = 5f;

    private bool jaCausouDano;
    private bool colisaoProcessada;
    private bool efeitoCriacaoInstanciado;
    private bool foiLancada;
    private float tempoEmRepouso;
    private Rigidbody corpoEmVoo;
    private InventarioFlechas inventarioRetorno;
    private SlotInventario slotRetorno;
    private XRGrabInteractable unidadeRetorno;
    private Arco arcoRetorno;
    private bool grabEstavaAtivo;

    [Serializable]
    internal class EstadoUsos
    {
        public const string Formato = "my-reincarnation.flecha.v1";
        public string formato = Formato;
        public ClasseManopla classe;
        public int maximo, restantes;
        public bool possuiSorteio;
        public float ataque;
        public List<AtributosManoplas.ValorAtributo> atributos;
    }

    private EstadoUsos usos;
    private static readonly HashSet<Flecha> flechasComBonus = new();
    private StatusPlayer jogadorComBonus;
    private Arco arcoPreparando;
    private string jsonUsosRestaurado;
    private float AtaqueBase => usos != null && usos.possuiSorteio ? usos.ataque : dano;
    private XRGrabInteractable grab;
    private Camera cameraPopup;
    private TMP_Text resumoPopup;
    private float fecharPopupEm;
    private bool popupAberto;
    private Transform paiPopup;
    private Vector3 posicaoOriginalPopup, escalaOriginalPopup;
    private Quaternion rotacaoOriginalPopup;
    private float multiplicadorDano = 1f;
    private GameObject dono;
    private Transform raizDono;
    private readonly List<ParColisaoIgnorado> paresColisaoIgnorados = new();

    private readonly struct ParColisaoIgnorado
    {
        public readonly Collider Flecha;
        public readonly Collider Ignorado;

        public ParColisaoIgnorado(Collider flecha, Collider ignorado)
        {
            Flecha = flecha;
            Ignorado = ignorado;
        }
    }

    public float ObterDano()
    {
        return Mathf.Max(0f, AtaqueBase * multiplicadorDano);
    }

    internal bool PodeUsar(StatusPlayer jogador)
    {
        return valorMinimoParaUsar <= 0 || (jogador != null &&
            Espada.AtendeRestricaoAtributo(Espada.ObterAtributoTotal(jogador, atributoRestricao),
                jogadorComBonus == jogador ? ObterValorAtributo(atributoRestricao) : 0, valorMinimoParaUsar));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparBonusRuntime() => flechasComBonus.Clear();

    internal static long ObterBonusEmUso(StatusPlayer jogador, TipoAtributoManopla atributo)
    {
        long total = 0;
        foreach (var flecha in flechasComBonus)
            if (flecha != null && flecha.jogadorComBonus == jogador)
                total += flecha.ObterValorAtributo(atributo);
        return total;
    }

    private int ObterValorAtributo(TipoAtributoManopla atributo)
    {
        if (usos?.atributos == null) return 0;
        foreach (var valor in usos.atributos)
            if (valor.atributo == atributo) return valor.valor;
        return 0;
    }

    internal bool PodeDispararCom(Arco arco) => !foiLancada && (arcoPreparando == null || arcoPreparando == arco);

    internal bool IniciarBonusPreparado(Arco arco, StatusPlayer jogador)
    {
        if (!PodeDispararCom(arco) || !PodeUsar(jogador))
            return false;
        GarantirUsos();
        arcoPreparando = arco;
        DefinirJogadorComBonus(jogador);
        return true;
    }

    internal void CancelarBonusPreparado(Arco arco)
    {
        if (arcoPreparando != arco) return;
        arcoPreparando = null;
        AtualizarBonusDaMao();
    }

    private void AtualizarBonusDaMao()
    {
        // O arco e o disparo usam a mesma fonte de bônus, mesmo quando o item do arco vem de uma pilha oculta.
        if (foiLancada || arcoPreparando != null) return;

        var jogador = ObterJogadorSegurando();
        if (jogador != null) GarantirUsos();
        DefinirJogadorComBonus(jogador);
    }

    private StatusPlayer ObterJogadorSegurando()
    {
        if (!isActiveAndEnabled || grab == null || !grab.isActiveAndEnabled || !grab.isSelected ||
            EstadoItemInventario.EstaNoInventario(this))
            return null;

        foreach (var interactor in grab.interactorsSelecting)
        {
            if (interactor is not Component controle || interactor is XRSocketInteractor) continue;
            var jogador = controle.GetComponentInParent<StatusPlayer>();
            if (jogador != null) return jogador;
        }
        return null;
    }

    private void DefinirJogadorComBonus(StatusPlayer jogador)
    {
        var anterior = jogadorComBonus;
        jogadorComBonus = jogador;
        if (jogador != null) flechasComBonus.Add(this);
        else flechasComBonus.Remove(this);
        if (anterior == jogador) return;
        if (anterior != null) anterior.AtualizarAtributosEquipamentos();
        if (jogador != null) jogador.AtualizarAtributosEquipamentos();
    }

    public GameObject ObterDono()
    {
        return dono;
    }

    public void DefinirDono(GameObject novoDono)
    {
        dono = novoDono;
        raizDono = dono != null && dono.transform != null ? dono.transform.root : null;
    }

    public void DefinirMultiplicadorDano(float valor)
    {
        multiplicadorDano = Mathf.Max(0f, valor);
    }

    public bool TentarObterDirecaoVisual(out Vector3 direcao)
    {
        direcao = Vector3.zero;
        if (traseiraDirecao == null || pontaDirecao == null || traseiraDirecao == pontaDirecao)
            return false;

        Vector3 diferenca = pontaDirecao.position - traseiraDirecao.position;
        if (diferenca.sqrMagnitude < 0.000001f)
            return false;

        direcao = diferenca.normalized;
        return true;
    }

    public int ConfigurarColisoesIgnoradas(
        GameObject arco,
        GameObject donoDisparo,
        IEnumerable<Collider> collidersExtras)
    {
        RestaurarColisoesIgnoradas();
        DefinirDono(donoDisparo);

        Collider[] collidersFlecha = GetComponentsInChildren<Collider>(true);
        HashSet<Collider> collidersIgnorados = new();
        AdicionarColliders(arco, collidersIgnorados);
        AdicionarColliders(donoDisparo, collidersIgnorados);

        if (collidersExtras != null)
        {
            foreach (Collider colliderExtra in collidersExtras)
            {
                if (colliderExtra != null)
                    collidersIgnorados.Add(colliderExtra);
            }
        }

        for (int i = 0; i < collidersFlecha.Length; i++)
        {
            Collider colliderFlecha = collidersFlecha[i];
            if (colliderFlecha == null)
                continue;

            foreach (Collider colliderIgnorado in collidersIgnorados)
            {
                if (colliderIgnorado == null || colliderIgnorado == colliderFlecha ||
                    ColliderPertenceAPropriaFlecha(colliderIgnorado))
                {
                    continue;
                }

                Physics.IgnoreCollision(colliderFlecha, colliderIgnorado, true);
                paresColisaoIgnorados.Add(new ParColisaoIgnorado(colliderFlecha, colliderIgnorado));
            }
        }

        return paresColisaoIgnorados.Count;
    }

    public void PrepararFisicaParaDisparo(bool usarGravidade)
    {
        Rigidbody rb = ObterCorpoDaFlecha();
        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.useGravity = usarGravidade;
        rb.detectCollisions = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public void MarcarComoLancada(GameObject donoDaFlecha)
    {
        DefinirDono(donoDaFlecha);
        if (foiLancada) return;
        FecharPopup();
        GarantirUsos();
        foiLancada = true;
        arcoPreparando = null;
        var jogador = donoDaFlecha != null ? donoDaFlecha.GetComponentInChildren<StatusPlayer>(true) : null;
        DefinirJogadorComBonus(jogador);
        tempoEmRepouso = 0f;
        corpoEmVoo = ObterCorpoDaFlecha();
        jaCausouDano = false;
        colisaoProcessada = false;
    }

    internal void DefinirRetorno(InventarioFlechas inventario, SlotInventario slot, XRGrabInteractable unidade, Arco arco)
    {
        inventarioRetorno = inventario;
        slotRetorno = slot;
        unidadeRetorno = unidade;
        arcoRetorno = arco;
        grabEstavaAtivo = unidade.enabled;
        unidade.enabled = false;
    }

    public void ResetarEstadoParaInventarioOuPrefab()
    {
        RestaurarColisoesIgnoradas();
        foiLancada = false;
        arcoPreparando = null;
        DefinirJogadorComBonus(null);
        corpoEmVoo = null;
        tempoEmRepouso = 0f;
        jaCausouDano = false;
        colisaoProcessada = false;
        dono = null;
        raizDono = null;
    }

    private void Awake()
    {
        InstanciarEfeitoCriacao();
        ConfigurarPopup();
        grab = GetComponentInParent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grab != null) grab.hoverEntered.AddListener(AbrirPopup);
        if (foiLancada) DefinirJogadorComBonus(dono != null ? dono.GetComponentInChildren<StatusPlayer>(true) : null);
    }

    private void GarantirUsos()
    {
        if (usos != null && usos.possuiSorteio) return;
        bool legado = usos != null;
        ClasseFlecha configuracao = legado ? ObterClasse(usos.classe) : SortearClasse();
        if (!legado)
        {
            int quantidade = configuracao == null || configuracao.classe == ClasseManopla.Comum
                ? 1 : SortearInteiro(configuracao.usosMinimos, configuracao.usosMaximos, 2);
            usos = new EstadoUsos { classe = configuracao?.classe ?? ClasseManopla.Comum, maximo = quantidade, restantes = quantidade };
        }

        usos.ataque = legado || !faixasAtaqueConfiguradas || configuracao == null
            ? dano : SortearInteiro(configuracao.ataqueMinimo, configuracao.ataqueMaximo, 0);
        usos.atributos = new List<AtributosManoplas.ValorAtributo>();
        if (configuracao?.atributos != null)
        {
            var tipos = new HashSet<TipoAtributoManopla>();
            var faixas = configuracao.atributos.FindAll(faixa => faixa != null &&
                Enum.IsDefined(typeof(TipoAtributoManopla), faixa.atributo) &&
                Espada.FaixaSorteioValida(faixa.minimo, faixa.maximo, 0) && tipos.Add(faixa.atributo));
            int minimo = Mathf.Clamp(configuracao.quantidadeMinima, 0, faixas.Count);
            int maximo = Mathf.Clamp(configuracao.quantidadeMaxima, minimo, faixas.Count);
            usos.atributos = Espada.SortearValoresAtributos(faixas, minimo, maximo);
        }
        usos.possuiSorteio = true;
        jsonUsosRestaurado = null;
    }

    private ClasseFlecha ObterClasse(ClasseManopla classe) =>
        classes == null ? null : Array.Find(classes, item => item != null && item.classe == classe);

    private ClasseFlecha SortearClasse() => SortearClassePonderada(classes);

    internal static T SortearClassePonderada<T>(T[] classes) where T : AtributosManoplas.ConfiguracaoClasse
    {
        if (classes == null) return null;
        float total = 0f;
        foreach (var classe in classes)
            if (classe != null && classe.chance > 0f && !float.IsInfinity(classe.chance))
                total += Mathf.Min(classe.chance, 100f);
        float sorteio = UnityEngine.Random.value * total;
        T ultima = null;
        foreach (var classe in classes)
        {
            if (classe == null || !(classe.chance > 0f) || float.IsInfinity(classe.chance)) continue;
            ultima = classe;
            sorteio -= Mathf.Min(classe.chance, 100f);
            if (sorteio < 0f) return classe;
        }
        return ultima ?? Array.Find(classes, item => item != null && item.classe == ClasseManopla.Comum);
    }

    internal static int SortearInteiro(int minimo, int maximo, int piso)
    {
        minimo = Mathf.Clamp(minimo, piso, int.MaxValue - 1);
        maximo = Mathf.Clamp(maximo, minimo, int.MaxValue - 1);
        return UnityEngine.Random.Range(minimo, maximo + 1);
    }

    internal string CapturarUsosJson()
    {
        if (usos == null) GarantirUsos();
        return jsonUsosRestaurado ?? JsonUtility.ToJson(usos);
    }

    internal static EstadoUsos LerUsosJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || !json.Contains(EstadoUsos.Formato)) return null;
        var estado = JsonUtility.FromJson<EstadoUsos>(json);
        if (estado == null || estado.formato != EstadoUsos.Formato ||
            !Enum.IsDefined(typeof(ClasseManopla), estado.classe) ||
            estado.restantes < 1 || estado.restantes > estado.maximo ||
            (estado.classe == ClasseManopla.Comum ? estado.maximo != 1 : estado.maximo < 2))
            throw new System.IO.InvalidDataException("Usos da flecha inválidos no save.");
        if (estado.possuiSorteio)
        {
            var tipos = new HashSet<TipoAtributoManopla>();
            if (float.IsNaN(estado.ataque) || float.IsInfinity(estado.ataque) || estado.ataque < 0f ||
                estado.atributos == null || estado.atributos.Exists(valor => valor == null || valor.valor < 0 ||
                    !Enum.IsDefined(typeof(TipoAtributoManopla), valor.atributo) || !tipos.Add(valor.atributo)))
                throw new System.IO.InvalidDataException("Ataque ou atributos da flecha inválidos no save.");
        }
        return estado;
    }

    internal void RestaurarUsos(EstadoUsos estado, string json)
    {
        usos = estado;
        jsonUsosRestaurado = estado != null ? json : null;
    }

    private void ConfigurarPopup()
    {
        if (popup == null)
        {
            foreach (var filho in GetComponentsInChildren<Canvas>(true))
                if (string.Equals(filho.name, "Popup", StringComparison.OrdinalIgnoreCase))
                {
                    popup = filho.gameObject;
                    break;
                }
        }
        if (popup == gameObject) popup = null;
        if (popup != null) popup.SetActive(false);
    }

    private TMP_Text ObterTextoPopup(TMP_Text texto, string nome)
    {
        if (texto != null) return texto;
        foreach (var candidato in popup.GetComponentsInChildren<TMP_Text>(true))
            if (string.Equals(candidato.name, nome, StringComparison.OrdinalIgnoreCase)) return candidato;
        return null;
    }

    private void AbrirPopup(HoverEnterEventArgs args)
    {
        if (!isActiveAndEnabled || foiLancada || !Espada.EhLaserDoPopup(args.interactorObject) || EstaSegurada()) return;
        cameraPopup = Camera.main;
        if (cameraPopup == null) return;
        GarantirUsos();
        if (popup == null) CriarPopupSimples();
        textoNome = ObterTextoPopup(textoNome, "nome");
        textoClasse = ObterTextoPopup(textoClasse, "Valor classe");
        TMP_Text textoAtaqueDaClasse = ObterTextoPopup(ObterClasse(usos.classe)?.textoAtaque ?? textoAtaque, "Ataque Valor");
        if (classes != null)
            foreach (var classe in classes)
                if (classe?.textoAtaque != null && classe.textoAtaque != textoAtaqueDaClasse)
                    classe.textoAtaque.text = "";
        textoUsosAtuais = ObterTextoPopup(textoUsosAtuais, "Uso Atual") ?? ObterTextoPopup(null, "Durabilidade Atual");
        textoUsosMaximos = ObterTextoPopup(textoUsosMaximos, "Uso Total") ?? ObterTextoPopup(null, "Durabilidade Limite");
        textoDescricao = ObterTextoPopup(textoDescricao, "Descricao");
        var rotuloUsos = ObterTextoPopup(null, "Texto Durabilidade");
        if (rotuloUsos != null) rotuloUsos.text = "Usos";
        string nome = string.IsNullOrWhiteSpace(nomeExibicao)
            ? GetComponentInParent<ItemPersistente>()?.ObterNomeItem() ?? (grab != null ? grab.name : gameObject.name)
            : nomeExibicao.Trim();
        var jogador = cameraPopup.GetComponentInParent<StatusPlayer>();
        string restricao = AtualizarRestricaoPopup(jogador);
        if (resumoPopup != null)
        {
            var atributos = usos.atributos.ConvertAll(valor => $"{valor.atributo} +{valor.valor}");
            resumoPopup.text = $"{nome}\n{usos.classe} · Ataque {AtaqueBase:0.##}\nUsos {usos.restantes}/{usos.maximo}\n" +
                $"{string.Join(" · ", atributos)}\n{restricao}{descricao}";
        }
        if (textoNome != null) textoNome.text = nome;
        if (textoClasse != null) textoClasse.text = usos.classe.ToString();
        if (textoAtaqueDaClasse != null) textoAtaqueDaClasse.text = AtaqueBase.ToString("0.##");
        if (textoUsosAtuais != null) textoUsosAtuais.text = usos.restantes.ToString();
        if (textoUsosMaximos != null) textoUsosMaximos.text = usos.maximo.ToString();
        if (textoDescricao != null) textoDescricao.text = descricao;
        for (int i = 0; i < 5; i++)
        {
            var linha = textosValores != null && i < textosValores.Count ? textosValores[i] : null;
            var raiz = popup.transform.Find("Fundo/" + i);
            var nomeAtributo = linha?.textoNome ?? raiz?.Find("Texto")?.GetComponent<TMP_Text>();
            var valorAtributo = linha?.textoValor ?? raiz?.Find("Numero")?.GetComponent<TMP_Text>();
            var valor = i < usos.atributos.Count ? usos.atributos[i] : null;
            if (raiz != null) raiz.gameObject.SetActive(valor != null);
            if (nomeAtributo != null) nomeAtributo.text = valor == null ? "" :
                valor.atributo == TipoAtributoManopla.Mana ? "MP" : valor.atributo.ToString();
            if (valorAtributo != null) valorAtributo.text = valor == null ? "" : "+" + valor.valor;
        }

        if (!popupAberto)
        {
            Transform alvo = popup.transform;
            paiPopup = alvo.parent;
            posicaoOriginalPopup = alvo.localPosition;
            rotacaoOriginalPopup = alvo.localRotation;
            escalaOriginalPopup = alvo.localScale;
            alvo.SetParent(null, true);
        }
        popupAberto = true;
        popup.SetActive(true);
        fecharPopupEm = Time.unscaledTime + Mathf.Max(0.1f, tempoPopup);
        PosicionarPopup();
    }

    private string AtualizarRestricaoPopup(StatusPlayer jogador)
    {
        if (grupoRestricao == null)
            grupoRestricao = popup.transform.Find("Fundo/Restricao")?.gameObject;
        bool mostrar = valorMinimoParaUsar > 0 && !PodeUsar(jogador);
        if (grupoRestricao != null)
        {
            grupoRestricao.SetActive(mostrar);
            if (textoAtributoRestricao == null)
                textoAtributoRestricao = grupoRestricao.transform.Find("Atributo")?.GetComponent<TMP_Text>();
            if (textoValorRestricao == null)
                textoValorRestricao = grupoRestricao.transform.Find("Valor")?.GetComponent<TMP_Text>();
            if (textoMensagemRestricao == null)
                textoMensagemRestricao = grupoRestricao.transform.Find("Descricao")?.GetComponent<TMP_Text>();
        }
        if (textoAtributoRestricao != null) textoAtributoRestricao.text = mostrar ? atributoRestricao.ToString() : "";
        if (textoValorRestricao != null) textoValorRestricao.text = mostrar ? valorMinimoParaUsar.ToString() : "";
        if (textoMensagemRestricao != null) textoMensagemRestricao.text = mostrar ? "Seu valor está abaixo do mínimo" : "";
        return mostrar ? $"Restrição: {atributoRestricao} {valorMinimoParaUsar}\n" : "";
    }

    private void CriarPopupSimples()
    {
        popup = CriarPopupSimples(transform, "Popup da Flecha", out resumoPopup);
    }

    internal static GameObject CriarPopupSimples(Transform parent, string nome, out TMP_Text resumoPopup)
    {
        var popup = new GameObject(nome, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.Image));
        popup.SetActive(false);
        var painel = (RectTransform)popup.transform;
        painel.sizeDelta = new Vector2(330f, 150f);
        painel.localScale = Vector3.one * 0.002f;
        painel.SetParent(parent, true);
        popup.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var fundo = popup.GetComponent<UnityEngine.UI.Image>();
        fundo.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        fundo.raycastTarget = false;

        var texto = new GameObject("Resumo", typeof(RectTransform), typeof(TextMeshProUGUI));
        var retangulo = (RectTransform)texto.transform;
        retangulo.SetParent(painel, false);
        retangulo.anchorMin = Vector2.zero;
        retangulo.anchorMax = Vector2.one;
        retangulo.offsetMin = new Vector2(12f, 8f);
        retangulo.offsetMax = new Vector2(-12f, -8f);
        resumoPopup = texto.GetComponent<TextMeshProUGUI>();
        resumoPopup.fontSize = 25f;
        resumoPopup.enableAutoSizing = true;
        resumoPopup.alignment = TextAlignmentOptions.Center;
        resumoPopup.raycastTarget = false;
        return popup;
    }

    private bool EstaSegurada()
    {
        if (grab == null) return false;
        foreach (var interactor in grab.interactorsSelecting)
            if (interactor != null && !(interactor is XRSocketInteractor)) return true;
        return false;
    }

    private void LateUpdate()
    {
        AtualizarBonusDaMao();
        if (!popupAberto) return;
        if (foiLancada || EstaSegurada() || popup == null || !popup.activeSelf || Time.unscaledTime >= fecharPopupEm)
            FecharPopup();
        else
            PosicionarPopup();
    }

    private void PosicionarPopup()
    {
        LayoutPopupsEquipamentos.Atualizar(popup, cameraPopup, posicaoPopupNaCamera,
            rotacaoPopupNaCamera, espacamentoPopups);
    }

    internal void FecharPopup()
    {
        if (!popupAberto || popup == null) return;
        popupAberto = false;
        LayoutPopupsEquipamentos.Remover(popup);
        popup.SetActive(false);
        popup.transform.SetParent(paiPopup, false);
        popup.transform.localPosition = posicaoOriginalPopup;
        popup.transform.localRotation = rotacaoOriginalPopup;
        popup.transform.localScale = escalaOriginalPopup;
    }

    private void OnDisable()
    {
        if (grab != null) grab.hoverEntered.RemoveListener(AbrirPopup);
        FecharPopup();
        if (arcoPreparando == null) DefinirJogadorComBonus(null);
    }

    private void FixedUpdate()
    {
        if (!foiLancada) return;
        if (corpoEmVoo == null || corpoEmVoo.IsSleeping())
        {
            FinalizarUso();
            return;
        }

        bool emRepouso = corpoEmVoo.linearVelocity.sqrMagnitude < 0.0025f &&
            corpoEmVoo.angularVelocity.sqrMagnitude < 0.01f;
        tempoEmRepouso = emRepouso ? tempoEmRepouso + Time.fixedDeltaTime : 0f;
        if (tempoEmRepouso >= 0.25f) FinalizarUso();
    }

    private void FinalizarUso()
    {
        if (!foiLancada) return;
        ResetarEstadoParaInventarioOuPrefab();
        usos.restantes--;
        jsonUsosRestaurado = null;
        var unidade = unidadeRetorno;
        unidadeRetorno = null;
        GameObject objeto = unidade != null ? unidade.gameObject : gameObject;

        if (usos.restantes == 0 || unidade == null)
        {
            objeto.GetComponent<ItemPersistente>()?.RegistrarConsumoPermanente();
            objeto.SetActive(false);
            Destroy(objeto);
        }
        else
        {
            unidade.enabled = grabEstavaAtivo;
            if (inventarioRetorno != null && inventarioRetorno.DevolverFlecha(unidade, slotRetorno) && arcoRetorno != null)
                arcoRetorno.AtualizarInventarioArcoUI();
        }

        inventarioRetorno = null;
        slotRetorno = null;
        arcoRetorno = null;
        SaveManager.Instancia?.SolicitarSalvamentoInventario();
    }

    public bool Disparar(Vector3 direcao, float forca)
    {
        if (!isActiveAndEnabled || foiLancada || !float.IsFinite(forca) || forca <= 0f ||
            !float.IsFinite(direcao.sqrMagnitude) || direcao.sqrMagnitude < 0.000001f)
            return false;

        jaCausouDano = false;
        colisaoProcessada = false;

        Rigidbody rb = ObterCorpoDaFlecha();
        if (rb == null)
            return false;

        PrepararFisicaParaDisparo(true);
        rb.AddForce(direcao.normalized * forca, ForceMode.Impulse);

        if (!foiLancada)
            MarcarComoLancada(dono);

        return true;
    }

    private Rigidbody ObterCorpoDaFlecha()
    {
        var corpo = GetComponent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();
        if (corpo != null) return corpo;
        // Alguns prefabs colocam Flecha no filho visual e a física na raiz pegável.
        return GetComponentInParent<XRGrabInteractable>(true)?.GetComponent<Rigidbody>();
    }

    private void OnValidate()
    {
        var configuradas = new List<ClasseFlecha>(classes ?? Array.Empty<ClasseFlecha>());
        foreach (ClasseManopla classe in Enum.GetValues(typeof(ClasseManopla)))
        {
            if (configuradas.Exists(config => config != null && config.classe == classe)) continue;
            int minimo = Mathf.Max(1, (int)classe + 1);
            configuradas.Add(new ClasseFlecha { classe = classe, usosMinimos = minimo, usosMaximos = minimo + 1 });
        }
        if (classes == null || classes.Length != configuradas.Count) classes = configuradas.ToArray();
        if (!faixasAtaqueConfiguradas)
        {
            int ataqueInicial = Mathf.Max(0, Mathf.RoundToInt(dano));
            foreach (var config in classes)
            {
                if (config == null) continue;
                config.ataqueMinimo = config.ataqueMaximo = ataqueInicial;
                config.textoAtaque = textoAtaque;
                config.quantidadeMinima = Mathf.Min((int)config.classe, 4);
                config.quantidadeMaxima = config.quantidadeMinima + 1;
                config.atributos = new List<AtributosManoplas.FaixaAtributo>();
                foreach (TipoAtributoManopla atributo in Enum.GetValues(typeof(TipoAtributoManopla)))
                    if (atributo != TipoAtributoManopla.Critico && atributo != TipoAtributoManopla.Defesa)
                        config.atributos.Add(new AtributosManoplas.FaixaAtributo { atributo = atributo });
            }
            faixasAtaqueConfiguradas = true;
        }
        dano = Mathf.Max(0f, dano);
        multiplicadorDano = Mathf.Max(0f, multiplicadorDano);
        volumeSomColisao = Mathf.Max(0f, volumeSomColisao);
        tempoDestruirEfeitoColisao = Mathf.Max(0f, tempoDestruirEfeitoColisao);
    }

    private void OnDestroy()
    {
        DefinirJogadorComBonus(null);
        paresColisaoIgnorados.Clear();
    }

    private static void AdicionarColliders(GameObject raiz, HashSet<Collider> destino)
    {
        if (raiz == null || destino == null)
            return;

        Collider[] colliders = raiz.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                destino.Add(colliders[i]);
        }
    }

    private void RestaurarColisoesIgnoradas()
    {
        for (int i = 0; i < paresColisaoIgnorados.Count; i++)
        {
            ParColisaoIgnorado par = paresColisaoIgnorados[i];
            if (par.Flecha != null && par.Ignorado != null)
                Physics.IgnoreCollision(par.Flecha, par.Ignorado, false);
        }

        paresColisaoIgnorados.Clear();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        Vector3 ponto = transform.position;
        Vector3 normal = -transform.forward;

        if (collision.contactCount > 0)
        {
            ContactPoint contato = collision.GetContact(0);
            ponto = contato.point;
            normal = contato.normal;
        }

        ProcessarColisao(collision.collider, ponto, normal);
    }

    private void OnTriggerEnter(Collider other)
    {
        Vector3 ponto = other != null ? other.ClosestPoint(transform.position) : transform.position;
        Vector3 normal = transform.forward.sqrMagnitude > 0.0001f ? -transform.forward : Vector3.up;
        ProcessarColisao(other, ponto, normal);
    }

    private void ProcessarColisao(Collider outroCollider, Vector3 pontoColisao, Vector3 normalColisao)
    {
        if (outroCollider == null)
            return;

        if (EstadoItemInventario.EstaNoInventario(this) || EstadoItemInventario.EstaNoInventario(outroCollider))
            return;

        if (!foiLancada)
            return;

        if (DeveIgnorarColisao(outroCollider))
            return;

        if (!colisaoProcessada)
        {
            colisaoProcessada = true;
            TocarSomColisao(pontoColisao);
            InstanciarEfeitoColisao(pontoColisao, normalColisao);
        }

        bool acertouAlvo = TentarResolverAlvoValido(outroCollider, out GameObject alvo);
        if (acertouAlvo && (!causarDanoUmaVez || !jaCausouDano))
        {
            int danoFinal = Mathf.RoundToInt(ObterDano());
            if (danoFinal > 0 && TentarAplicarDano(alvo, danoFinal))
                jaCausouDano = true;
        }

        if (acertouAlvo) FinalizarUso();
    }

    private bool TentarResolverAlvoValido(Collider outroCollider, out GameObject alvo)
    {
        alvo = null;

        if (outroCollider == null || tagsQueRecebemDano == null || tagsQueRecebemDano.Length == 0)
            return false;

        Transform transformComTag = ProcurarTransformComTagPermitida(outroCollider.transform);
        if (transformComTag == null && outroCollider.attachedRigidbody != null)
            transformComTag = ProcurarTransformComTagPermitida(outroCollider.attachedRigidbody.transform);

        if (transformComTag == null && outroCollider.transform.root != null &&
            TagEstaPermitida(outroCollider.transform.root.tag))
        {
            transformComTag = outroCollider.transform.root;
        }

        Component receptor = ReceptorDanoInimigo.Resolver(outroCollider);
        StatusPlayer player = outroCollider.GetComponentInParent<StatusPlayer>();
        alvo = transformComTag == null ? null : receptor != null ? receptor.gameObject :
            player != null ? player.gameObject : transformComTag.gameObject;
        return alvo != null;
    }

    private bool TentarAplicarDano(GameObject alvo, int danoFinal)
    {
        if (alvo == null || danoFinal <= 0)
            return false;

        Component inimigo = ReceptorDanoInimigo.Resolver(alvo.transform);
        if (inimigo != null)
        {
            if (ReceptorDanoInimigo.EstaMorto(inimigo)) return false;
            ReceptorDanoInimigo.Aplicar(inimigo, danoFinal, gameObject);
            return true;
        }
        Component[] componentes = ColetarComponentesParaDano(alvo);
        for (int i = 0; i < componentes.Length; i++)
        {
            Component componente = componentes[i];
            if (componente == null || componente == this)
                continue;

            if (TentarChamarMetodoDano(componente, "ReceberDano", danoFinal))
                return true;

            if (TentarChamarMetodoDano(componente, "TomarDano", danoFinal))
                return true;

            if (TentarChamarMetodoDano(componente, "AplicarDano", danoFinal))
                return true;
        }

        return false;
    }

    private bool TentarChamarMetodoDano(Component componente, string nomeMetodo, int danoFinal)
    {
        Type tipo = componente.GetType();

        MethodInfo metodoIntComOrigem = tipo.GetMethod(
            nomeMetodo,
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(int), typeof(GameObject) },
            null);

        if (metodoIntComOrigem != null)
            return InvocarMetodoDano(metodoIntComOrigem, componente, danoFinal, gameObject);

        MethodInfo metodoFloatComOrigem = tipo.GetMethod(
            nomeMetodo,
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(float), typeof(GameObject) },
            null);

        if (metodoFloatComOrigem != null)
            return InvocarMetodoDano(metodoFloatComOrigem, componente, (float)danoFinal, gameObject);

        MethodInfo metodoInt = tipo.GetMethod(
            nomeMetodo,
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(int) },
            null);

        if (metodoInt != null)
            return InvocarMetodoDano(metodoInt, componente, danoFinal);

        MethodInfo metodoFloat = tipo.GetMethod(
            nomeMetodo,
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(float) },
            null);

        return metodoFloat != null && InvocarMetodoDano(metodoFloat, componente, (float)danoFinal);
    }

    private bool InvocarMetodoDano(MethodInfo metodo, Component componente, params object[] parametros)
    {
        try
        {
            metodo.Invoke(componente, parametros);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private Component[] ColetarComponentesParaDano(GameObject alvo)
    {
        List<Component> componentes = new();
        HashSet<Component> visitados = new();

        AdicionarComponentes(alvo.GetComponents<Component>(), componentes, visitados);
        AdicionarComponentes(alvo.GetComponentsInParent<Component>(true), componentes, visitados);


        Rigidbody rb = alvo.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            AdicionarComponentes(rb.GetComponents<Component>(), componentes, visitados);
            AdicionarComponentes(rb.GetComponentsInParent<Component>(true), componentes, visitados);

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

    private bool DeveIgnorarColisao(Collider outroCollider)
    {
        if (outroCollider == null)
            return true;

        if (ColliderPertenceAPropriaFlecha(outroCollider))
            return true;

        if (EhTerrain(outroCollider))
            return true;

        if (ColliderTemTagIgnorada(outroCollider))
            return true;

        return ignorarDonoDaFlecha && ColliderPertenceAoDono(outroCollider);
    }

    private bool EhTerrain(Collider outroCollider)
    {
        if (!ignorarTerrain || outroCollider == null)
            return false;

        if (outroCollider is TerrainCollider)
            return true;

        if (outroCollider.GetComponent<Terrain>() != null ||
            outroCollider.GetComponentInParent<Terrain>() != null)
        {
            return true;
        }

        return outroCollider.attachedRigidbody != null &&
               outroCollider.attachedRigidbody.GetComponentInParent<Terrain>() != null;
    }

    private bool ColliderTemTagIgnorada(Collider outroCollider)
    {
        if (outroCollider == null || tagsIgnoradasColisao == null || tagsIgnoradasColisao.Length == 0)
            return false;

        if (TransformTemTagPermitida(outroCollider.transform, tagsIgnoradasColisao))
            return true;

        if (outroCollider.attachedRigidbody != null &&
            TransformTemTagPermitida(outroCollider.attachedRigidbody.transform, tagsIgnoradasColisao))
        {
            return true;
        }

        Transform root = outroCollider.transform.root;
        return root != null && TagEstaNaLista(root.tag, tagsIgnoradasColisao);
    }

    private bool ColliderPertenceAoDono(Collider outroCollider)
    {
        if (outroCollider == null || (dono == null && raizDono == null))
            return false;

        if (TransformPertenceAoDono(outroCollider.transform))
            return true;

        return outroCollider.attachedRigidbody != null &&
               TransformPertenceAoDono(outroCollider.attachedRigidbody.transform);
    }

    private bool TransformPertenceAoDono(Transform origem)
    {
        if (origem == null || (dono == null && raizDono == null))
            return false;

        if (dono != null)
        {
            Transform donoTransform = dono.transform;
            if (origem == donoTransform || origem.IsChildOf(donoTransform))
                return true;
        }

        Transform rootOrigem = origem.root;
        return raizDono != null && rootOrigem != null && rootOrigem == raizDono;
    }

    private bool ColliderPertenceAPropriaFlecha(Collider outroCollider)
    {
        if (outroCollider == null)
            return false;

        Transform outro = outroCollider.transform;
        return outro == transform || outro.IsChildOf(transform);
    }

    private void InstanciarEfeitoCriacao()
    {
        if (efeitoCriacaoInstanciado || efeitoAoCriar == null)
            return;

        efeitoCriacaoInstanciado = true;

        Transform parent = parentarEfeitoCriacaoNaFlecha ? transform : null;
        GameObject efeito = Instantiate(efeitoAoCriar, transform.position, transform.rotation, parent);

        if (parentarEfeitoCriacaoNaFlecha && efeito != null)
        {
            efeito.transform.localPosition = Vector3.zero;
            efeito.transform.localRotation = Quaternion.identity;
        }
    }

    private void InstanciarEfeitoColisao(Vector3 ponto, Vector3 normal)
    {
        if (efeitoAoColidir == null)
            return;

        Quaternion rotacao = normal.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(normal.normalized)
            : Quaternion.identity;

        GameObject efeito = Instantiate(efeitoAoColidir, ponto, rotacao);
        if (efeito != null && tempoDestruirEfeitoColisao > 0f)
            Destroy(efeito, tempoDestruirEfeitoColisao);
    }

    private void TocarSomColisao(Vector3 ponto)
    {
        if (somColisao == null || volumeSomColisao <= 0f)
            return;

        AudioSource.PlayClipAtPoint(somColisao, ponto, volumeSomColisao);
    }

    private Transform ProcurarTransformComTagPermitida(Transform origem)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (TagEstaPermitida(atual.tag))
                return atual;

            atual = atual.parent;
        }

        return null;
    }

    private bool TransformTemTagPermitida(Transform origem, string[] tagsPermitidas)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (TagEstaNaLista(atual.tag, tagsPermitidas))
                return true;

            atual = atual.parent;
        }

        return false;
    }

    private bool TagEstaPermitida(string tagAtual)
    {
        return TagEstaNaLista(tagAtual, tagsQueRecebemDano);
    }

    private bool TagEstaNaLista(string tagAtual, string[] tagsPermitidas)
    {
        if (string.IsNullOrWhiteSpace(tagAtual) || tagsPermitidas == null)
            return false;

        for (int i = 0; i < tagsPermitidas.Length; i++)
        {
            string tagPermitida = tagsPermitidas[i];
            if (string.IsNullOrWhiteSpace(tagPermitida))
                continue;

            if (string.Equals(tagAtual, tagPermitida.Trim(), StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
