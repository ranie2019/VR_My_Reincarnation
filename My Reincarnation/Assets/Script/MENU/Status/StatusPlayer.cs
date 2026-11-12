using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class StatusPlayer : MonoBehaviour
{
    private const float MultiplicadorVidaManaPorNivel = 1.10f;
    private const int ValorInicialAtributo = 10;

    public event Action StatusAlterado;

    [Header("Identidade")]
    [SerializeField] private string nome = "Ranie";
    [SerializeField] private string raca = "Humano";
    [SerializeField] private string classe = "Aventureiro";
    [SerializeField] private string titulo = "Reencarnado";

    [Header("Level")]
    [SerializeField] private int nivel;
    [SerializeField] private int experienciaAtual;
    [SerializeField] private int experienciaParaProximoNivel = 100;
    [SerializeField] private float multiplicadorExperienciaPorNivel = 1.10f;
    [SerializeField] private int pontosStatusDisponiveis;
    [SerializeField] private int pontosStatusPorNivel = 10;

    [Header("Moeda REIN")]
    [SerializeField] private CarteiraReinPlayer carteiraRein;

    [Header("Level Up")]
    public AudioSource audioSource;
    public AudioClip somLevelUp;
    public ParticleSystem efeitoLevelUp;

    [Header("Vida e Mana")]
    [SerializeField] private int vidaAtual = 100;
    [SerializeField] private int vidaMaxima = 100;
    [SerializeField] private int manaAtual = 50;
    [SerializeField] private int manaMaxima = 50;

    [Header("Regeneracao de Mana")]
    [SerializeField] private bool regenerarManaAutomaticamente = true;
    [SerializeField, Min(0)] private int quantidadeManaRecuperada = 1;
    [SerializeField, Min(0.1f)] private float intervaloRecuperacaoMana = 30f;
    [SerializeField] private bool regenerarManaEnquantoMorto = false;

    [Header("Dano Recebido")]
    [SerializeField] private string[] tagsQueCausamDanoNoPlayer;
    [SerializeField] private float danoFallback = 1f;
    [SerializeField] private float intervaloEntreDanos = 0.25f;
    [SerializeField] private bool debugDanoRecebido = true;

    [Header("Morte e Respawn")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Vector3 posicaoRespawn = new Vector3(601.119995f, 0.5f, -430.820007f);
    [SerializeField] private Vector3 rotacaoRespawnEuler = Vector3.zero;
    [SerializeField] private GameObject efeitoMortePrefab;
    [SerializeField] private float tempoAntesRespawn = 2f;
    [SerializeField] private bool debugMortePlayer = true;

    [Header("Atributos Base")]
    [SerializeField] private int forca = ValorInicialAtributo;
    [SerializeField] private int constituicao = ValorInicialAtributo;
    [SerializeField] private int agilidade = ValorInicialAtributo;
    [SerializeField] private int velocidade = ValorInicialAtributo;
    [SerializeField] private int inteligencia = ValorInicialAtributo;
    [SerializeField] private int espirito = ValorInicialAtributo;
    [SerializeField] private int sorte = ValorInicialAtributo;

    [Header("Atributos Extras")]
    [SerializeField] private int destreza = ValorInicialAtributo;
    [SerializeField] private int vigor = ValorInicialAtributo;
    [SerializeField] private int percepcao = ValorInicialAtributo;
    [SerializeField] private int resistencia = ValorInicialAtributo;
    [SerializeField] private int mana;
    [SerializeField] private int carisma = ValorInicialAtributo;
    [SerializeField] private int critico;
    [SerializeField] private int defesa;

    [Header("Bonus Atributos Base")]
    [SerializeField] private int bonusForca;
    [SerializeField] private int bonusConstituicao;
    [SerializeField] private int bonusAgilidade;
    [SerializeField] private int bonusVelocidade;
    [SerializeField] private int bonusInteligencia;
    [SerializeField] private int bonusEspirito;
    [SerializeField] private int bonusSorte;

    [Header("Bonus Atributos Extras")]
    [SerializeField] private int bonusDestreza;
    [SerializeField] private int bonusVigor;
    [SerializeField] private int bonusPercepcao;
    [SerializeField] private int bonusResistencia;
    [SerializeField] private int bonusMana;
    [SerializeField] private int bonusCarisma;
    [SerializeField] private int bonusCritico;
    [SerializeField] private int bonusDefesa;

    private float proximoTempoPodeReceberDano;
    private float vidaAtualReal;
    private bool vidaAtualRealInicializada;
    private bool playerMorto;
    private CarteiraReinPlayer carteiraReinInscrita;
    private Coroutine rotinaRegeneracaoMana;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (playerRoot == null)
            playerRoot = transform.root;

        GarantirCarteiraRein();
        GarantirAtributosIniciais();
    }

    private void OnEnable()
    {
        IniciarRegeneracaoMana();
    }

    private void Start()
    {
        GarantirCarteiraRein();
        SincronizarVidaAtualReal();
    }

    private void OnDisable()
    {
        PararRegeneracaoMana();
    }

    private void OnDestroy()
    {
        PararRegeneracaoMana();

        if (carteiraReinInscrita != null)
            carteiraReinInscrita.AoSaldoReinAlterado -= AoSaldoReinAlterado;
    }

    private void OnValidate()
    {
        experienciaAtual = Mathf.Max(0, experienciaAtual);
        experienciaParaProximoNivel = Mathf.Max(1, experienciaParaProximoNivel);
        multiplicadorExperienciaPorNivel = Mathf.Max(1.01f, multiplicadorExperienciaPorNivel);
        pontosStatusDisponiveis = Mathf.Max(0, pontosStatusDisponiveis);
        pontosStatusPorNivel = Mathf.Max(0, pontosStatusPorNivel);
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual, 0, GetVidaMaxima());
        manaMaxima = Mathf.Max(1, manaMaxima);
        manaAtual = Mathf.Clamp(manaAtual, 0, GetManaMaxima());
        quantidadeManaRecuperada = Mathf.Max(0, quantidadeManaRecuperada);
        intervaloRecuperacaoMana = Mathf.Max(0.1f, intervaloRecuperacaoMana);
        danoFallback = Mathf.Max(0f, danoFallback);
        intervaloEntreDanos = Mathf.Max(0f, intervaloEntreDanos);
        tempoAntesRespawn = Mathf.Max(0f, tempoAntesRespawn);
        GarantirAtributosIniciais();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
        {
            LogDano("OnCollisionEnter ignorado: collision nula.");
            return;
        }

        ProcessarContatoDeDano(collision.collider, "OnCollisionEnter");
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessarContatoDeDano(other, "OnTriggerEnter");
    }

    public void ReceberDano(float ataqueBruto, GameObject origemDoDano)
    {
        TentarReceberDano(ataqueBruto, origemDoDano);
    }

    internal bool TentarReceberDano(float ataqueBruto, GameObject origemDoDano)
    {
        if (playerMorto)
            return false;

        SincronizarVidaAtualRealSeNecessario();

        IDano fonteDano = EncontrarFonteDano(origemDoDano);
        string nomeOrigem = origemDoDano != null ? origemDoDano.name : "<sem origem>";

        if (ataqueBruto <= 0f)
        {
            LogDano($"Dano de '{nomeOrigem}' ignorado: ataque bruto {ataqueBruto:F3}.");
            return false;
        }

        if (OrigemPertenceAoPlayer(origemDoDano, fonteDano))
        {
            LogDano($"Dano de '{nomeOrigem}' ignorado: origem pertence ao proprio Player.");
            return false;
        }

        if (Time.time < proximoTempoPodeReceberDano)
        {
            LogDano($"Dano de '{nomeOrigem}' ignorado: cooldown ate {proximoTempoPodeReceberDano:F3}.");
            return false;
        }

        float defesaCalculada = Mathf.Max(0f, (float)GetConstituicaoTotal() + GetResistenciaTotal());
        float danoFinal = ataqueBruto * (ataqueBruto / (ataqueBruto + defesaCalculada));

        if (danoFinal < 0.1f)
        {
            LogDano(
                $"Dano de '{nomeOrigem}' zerado: IDano={(fonteDano != null ? "sim" : "nao")}, " +
                $"bruto={ataqueBruto:F3}, defesa={defesaCalculada:F3}, final={danoFinal:F3}.");
            return false;
        }

        float vidaAntes = vidaAtualReal;
        vidaAtualReal = Mathf.Max(0f, vidaAtualReal - danoFinal);
        vidaAtual = Mathf.Clamp(Mathf.CeilToInt(vidaAtualReal), 0, GetVidaMaxima());
        proximoTempoPodeReceberDano = Time.time + intervaloEntreDanos;

        if (vidaAtualReal <= 0f)
        {
            vidaAtualReal = 0f;
            vidaAtual = 0;
        }

        NotificarStatusAlterado();

        string tagOrigem = origemDoDano != null ? origemDoDano.tag : "<sem tag>";
        LogDano(
            $"origem={nomeOrigem}, tag={tagOrigem}, IDano={(fonteDano != null ? "sim" : "nao")}, " +
            $"ataqueBruto={ataqueBruto:0.###}, defesa={defesaCalculada:0.###}, " +
            $"danoFinal={danoFinal:0.###}, vidaAntes={vidaAntes:0.###}, " +
            $"vidaDepois={vidaAtualReal:0.###} (compatibilidade={vidaAtual}/{vidaMaxima}).");

        if (vidaAtualReal <= 0f)
            Morrer();
        return true;
    }

    // Mantem compatibilidade com inimigos atuais que procuram ReceberDano(int).
    public void ReceberDano(int ataqueBruto)
    {
        ReceberDano((float)ataqueBruto, null);
    }

    private void ProcessarContatoDeDano(Collider outro, string evento)
    {
        if (Machado.ControlaImpacto(outro) || Picareta.ControlaImpacto(outro)) return;
        if (outro != null && (outro.GetComponentInParent<Flecha>() != null ||
            (outro.attachedRigidbody != null && outro.attachedRigidbody.GetComponent<Flecha>() != null)))
            return; // O impacto lancado e processado exclusivamente pela Flecha.

        if (outro == null)
        {
            LogDano($"{evento} ignorado: collider nulo.");
            return;
        }

        if (outro.transform == transform || outro.transform.IsChildOf(transform))
        {
            LogDano($"{evento} de '{outro.name}' ignorado: collider pertence ao proprio Player.");
            return;
        }

        IDano fonteDano = EncontrarFonteDano(outro, out GameObject objetoFonteDano);
        if (fonteDano is SlimeIA || fonteDano is GoblinIA || fonteDano is TopeiraIA || fonteDano is RaposaIA) return; // A IA resolve o mesmo golpe com o escudo.
        string tagCollider = outro.gameObject.tag;
        LogDano(
            $"{evento}: objeto='{outro.name}', tag='{tagCollider}', " +
            $"IDano={(fonteDano != null ? "encontrado" : "nao encontrado")}.");

        if (!TagPodeCausarDano(outro.transform, fonteDano))
        {
            LogDano($"{evento} de '{outro.name}' ignorado: nenhuma tag autorizada no objeto, pai, root ou IDano.");
            return;
        }

        float dano = fonteDano != null
            ? Mathf.Max(0f, fonteDano.ObterDano())
            : Mathf.Max(0f, danoFallback);
        GameObject origem = objetoFonteDano != null ? objetoFonteDano : outro.gameObject;

        LogDano(
            $"{evento} autorizado: origem='{origem.name}', tag='{origem.tag}', " +
            $"IDano={(fonteDano != null ? "sim" : "nao, usando fallback")}, dano bruto={dano:F3}.");
        ReceberDano(dano, origem);
    }

    private bool TagPodeCausarDano(Transform origem, IDano fonteDano)
    {
        if (origem == null || tagsQueCausamDanoNoPlayer == null || tagsQueCausamDanoNoPlayer.Length == 0)
            return false;

        if (TransformTemTagAutorizada(origem))
            return true;

        if (origem.parent != null && TransformTemTagAutorizada(origem.parent))
            return true;

        if (origem.root != null && TransformTemTagAutorizada(origem.root))
            return true;

        if (fonteDano is Component componenteFonte &&
            TransformTemTagAutorizada(componenteFonte.transform))
        {
            return true;
        }

        return false;
    }

    private bool TransformTemTagAutorizada(Transform origem)
    {
        if (origem == null)
            return false;

        for (Transform atual = origem; atual != null; atual = atual.parent)
        {
            string tagAtual = atual.gameObject.tag;
            for (int i = 0; i < tagsQueCausamDanoNoPlayer.Length; i++)
            {
                string tagAutorizada = tagsQueCausamDanoNoPlayer[i];
                if (!string.IsNullOrWhiteSpace(tagAutorizada) &&
                    string.Equals(tagAtual, tagAutorizada.Trim(), StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IDano EncontrarFonteDano(GameObject origemDoDano)
    {
        if (origemDoDano == null)
            return null;

        IDano fonteDano = origemDoDano.GetComponent<IDano>();
        if (fonteDano != null)
            return fonteDano;

        fonteDano = origemDoDano.GetComponentInParent<IDano>();
        if (fonteDano != null)
            return fonteDano;

        Transform raiz = origemDoDano.transform.root;
        return raiz != null ? raiz.GetComponentInChildren<IDano>(true) : null;
    }

    private static IDano EncontrarFonteDano(Collider collider, out GameObject objetoFonteDano)
    {
        objetoFonteDano = null;
        if (collider == null)
            return null;

        IDano fonteDano = collider.GetComponent<IDano>();
        if (fonteDano == null)
            fonteDano = collider.GetComponentInParent<IDano>();

        Rigidbody rigidbody = collider.attachedRigidbody;
        if (fonteDano == null && rigidbody != null)
            fonteDano = rigidbody.GetComponent<IDano>();
        if (fonteDano == null && rigidbody != null)
            fonteDano = rigidbody.GetComponentInParent<IDano>();

        Transform raiz = collider.transform.root;
        if (fonteDano == null && raiz != null)
            fonteDano = raiz.GetComponentInChildren<IDano>(true);

        if (fonteDano is Component componenteFonte)
            objetoFonteDano = componenteFonte.gameObject;

        return fonteDano;
    }

    private bool OrigemPertenceAoPlayer(GameObject origemDoDano, IDano fonteDano)
    {
        if (origemDoDano == null)
            return false;

        Transform origem = origemDoDano.transform;
        if (origemDoDano == gameObject || origem.IsChildOf(transform))
            return true;

        if (fonteDano == null)
            return false;

        GameObject dono = fonteDano.ObterDono();
        if (dono == gameObject)
            return true;

        Transform raizPlayer = transform.root;
        return raizPlayer != null && dono == raizPlayer.gameObject;
    }

    private void SincronizarVidaAtualReal()
    {
        vidaAtualReal = Mathf.Clamp(vidaAtual, 0, GetVidaMaxima());
        vidaAtualRealInicializada = true;
    }

    private void SincronizarVidaAtualRealSeNecessario()
    {
        vidaAtual = Mathf.Clamp(vidaAtual, 0, GetVidaMaxima());
        if (!vidaAtualRealInicializada || Mathf.CeilToInt(vidaAtualReal) != vidaAtual)
            SincronizarVidaAtualReal();
    }

    private void LogDano(string mensagem)
    {
        if (debugDanoRecebido)
            { }
    }

    public float GetVidaAtualReal()
    {
        SincronizarVidaAtualRealSeNecessario();
        return vidaAtualReal;
    }

    private void Morrer()
    {
        if (playerMorto)
            return;

        playerMorto = true;

        if (playerRoot == null)
            playerRoot = transform.root;

        if (debugMortePlayer)
            { }
        if (efeitoMortePrefab != null)
            Instantiate(efeitoMortePrefab, playerRoot.position, Quaternion.identity);

        AplicarPenalidadeMorte();
        StartCoroutine(RespawnPlayer());
    }

    private void AplicarPenalidadeMorte()
    {
        nivel -= 1;
        experienciaAtual = 0;
        NotificarStatusAlterado();

        if (debugMortePlayer)
        {
            { }
        }
    }

    private IEnumerator RespawnPlayer()
    {
        yield return new WaitForSeconds(tempoAntesRespawn);

        if (playerRoot == null)
            playerRoot = transform.root;

        CharacterController cc = playerRoot.GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;

        Rigidbody rb = playerRoot.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        playerRoot.position = posicaoRespawn;
        playerRoot.rotation = Quaternion.Euler(rotacaoRespawnEuler);

        if (cc != null)
            cc.enabled = true;

        RestaurarVidaAposRespawn();
        playerMorto = false;
        NotificarStatusAlterado();

        if (debugMortePlayer)
            { }
    }

    private void RestaurarVidaAposRespawn()
    {
        vidaAtualReal = GetVidaMaxima();
        vidaAtual = Mathf.CeilToInt(vidaAtualReal);
        vidaAtualRealInicializada = true;
    }

    public void ReceberExperiencia(int quantidade)
    {
        if (quantidade <= 0)
            return;

        experienciaAtual += quantidade;
        VerificarLevelUp();
        NotificarStatusAlterado();
    }

    private void VerificarLevelUp()
    {
        experienciaParaProximoNivel = Mathf.Max(1, experienciaParaProximoNivel);

        while (experienciaAtual >= experienciaParaProximoNivel)
        {
            experienciaAtual -= experienciaParaProximoNivel;
            SubirNivel();
        }
    }

    private void SubirNivel()
    {
        nivel += 1;
        pontosStatusDisponiveis += pontosStatusPorNivel;
        experienciaParaProximoNivel = Mathf.Max(
            1,
            Mathf.CeilToInt(experienciaParaProximoNivel * Mathf.Max(1.01f, multiplicadorExperienciaPorNivel)));
        vidaMaxima = Mathf.Max(1, Mathf.CeilToInt(vidaMaxima * MultiplicadorVidaManaPorNivel));
        manaMaxima = Mathf.Max(1, Mathf.CeilToInt(manaMaxima * MultiplicadorVidaManaPorNivel));
        vidaAtual = GetVidaMaxima();
        manaAtual = GetManaMaxima();
        TocarFeedbackLevelUp();
    }

    public bool GastarPontoEmAtributo(string nomeAtributo)
    {
        if (pontosStatusDisponiveis <= 0 || string.IsNullOrWhiteSpace(nomeAtributo))
            return false;

        string atributo = NormalizarNomeAtributo(nomeAtributo);

        switch (atributo)
        {
            case "forca":
            case "strength":
                return AdicionarPontoForca();
            case "constituicao":
            case "constitution":
                return AdicionarPontoConstituicao();
            case "agilidade":
            case "agility":
                return AdicionarPontoAgilidade();
            case "velocidade":
            case "speed":
                return AdicionarPontoVelocidade();
            case "inteligencia":
            case "intelligence":
                return AdicionarPontoInteligencia();
            case "espirito":
            case "spirit":
                return AdicionarPontoEspirito();
            case "sorte":
            case "luck":
                return AdicionarPontoSorte();
            case "destreza":
            case "dexterity":
                return AdicionarPontoDestreza();
            case "force":
            case "vigor":
                return AdicionarPontoForcaExtra();
            case "percepcao":
            case "perception":
                return AdicionarPontoPercepcao();
            case "resistencia":
            case "resistance":
                return AdicionarPontoResistencia();
            case "mana":
                return AdicionarPontoMana();
            case "carisma":
            case "charisma":
                return AdicionarPontoCarisma();
            case "critico":
                return AdicionarPontoCritico();
            case "defesa":
                return AdicionarPontoDefesa();
            default:
                return false;
        }
    }

    public bool AdicionarPontoForca()
    {
        return ConsumirPontoStatus(() => forca += 1);
    }

    public bool AdicionarPontoConstituicao()
    {
        return ConsumirPontoStatus(() => constituicao += 1);
    }

    public bool AdicionarPontoAgilidade()
    {
        return ConsumirPontoStatus(() => agilidade += 1);
    }

    public bool AdicionarPontoVelocidade()
    {
        return ConsumirPontoStatus(() => velocidade += 1);
    }

    public bool AdicionarPontoInteligencia()
    {
        return ConsumirPontoStatus(() => inteligencia += 1);
    }

    public bool AdicionarPontoEspirito()
    {
        return ConsumirPontoStatus(() => espirito += 1);
    }

    public bool AdicionarPontoDestreza()
    {
        return ConsumirPontoStatus(() => destreza += 1);
    }

    public bool AdicionarPontoForcaExtra()
    {
        return ConsumirPontoStatus(() => vigor += 1);
    }

    public bool AdicionarPontoVigor()
    {
        return AdicionarPontoForcaExtra();
    }

    public bool AdicionarPontoPercepcao()
    {
        return ConsumirPontoStatus(() => percepcao += 1);
    }

    public bool AdicionarPontoResistencia()
    {
        GarantirResistenciaInicial();
        return ConsumirPontoStatus(() => resistencia += 1);
    }

    public bool AdicionarPontoCarisma()
    {
        return ConsumirPontoStatus(() => carisma += 1);
    }

    public bool AdicionarPontoSorte()
    {
        return ConsumirPontoStatus(() => sorte += 1);
    }

    public bool AdicionarPontoMana()
    {
        return ConsumirPontoStatus(() => mana += 1);
    }

    public bool AdicionarPontoCritico()
    {
        return ConsumirPontoStatus(() => critico += 1);
    }

    public bool AdicionarPontoDefesa()
    {
        return ConsumirPontoStatus(() => defesa += 1);
    }

    private bool ConsumirPontoStatus(Action adicionarAtributo)
    {
        if (pontosStatusDisponiveis <= 0 || adicionarAtributo == null)
            return false;

        adicionarAtributo();
        pontosStatusDisponiveis -= 1;
        NotificarStatusAlterado();
        return true;
    }

    private void GarantirAtributosIniciais()
    {
        forca = Mathf.Max(ValorInicialAtributo, forca);
        constituicao = Mathf.Max(ValorInicialAtributo, constituicao);
        agilidade = Mathf.Max(ValorInicialAtributo, agilidade);
        velocidade = Mathf.Max(ValorInicialAtributo, velocidade);
        inteligencia = Mathf.Max(ValorInicialAtributo, inteligencia);
        espirito = Mathf.Max(ValorInicialAtributo, espirito);
        sorte = Mathf.Max(ValorInicialAtributo, sorte);
        destreza = Mathf.Max(ValorInicialAtributo, destreza);
        vigor = Mathf.Max(ValorInicialAtributo, vigor);
        percepcao = Mathf.Max(ValorInicialAtributo, percepcao);
        GarantirResistenciaInicial();
        carisma = Mathf.Max(ValorInicialAtributo, carisma);
    }

    private void GarantirResistenciaInicial()
    {
        resistencia = Mathf.Max(ValorInicialAtributo, resistencia);
    }

    private void TocarFeedbackLevelUp()
    {
        if (audioSource != null && somLevelUp != null)
            audioSource.PlayOneShot(somLevelUp);

        if (efeitoLevelUp != null)
            efeitoLevelUp.Play();
    }

    private void NotificarStatusAlterado()
    {
        StatusAlterado?.Invoke();
    }

    private void GarantirCarteiraRein()
    {
        CarteiraReinPlayer encontrada = carteiraRein;
        if (encontrada == null)
            encontrada = GetComponent<CarteiraReinPlayer>();
        if (encontrada == null)
            encontrada = GetComponentInParent<CarteiraReinPlayer>();
        if (encontrada == null)
            encontrada = GetComponentInChildren<CarteiraReinPlayer>(true);
        if (encontrada == null)
            encontrada = gameObject.AddComponent<CarteiraReinPlayer>();

        if (carteiraRein == encontrada && carteiraReinInscrita == encontrada)
            return;

        if (carteiraReinInscrita != null)
            carteiraReinInscrita.AoSaldoReinAlterado -= AoSaldoReinAlterado;

        carteiraRein = encontrada;
        carteiraRein.AoSaldoReinAlterado -= AoSaldoReinAlterado;
        carteiraRein.AoSaldoReinAlterado += AoSaldoReinAlterado;
        carteiraReinInscrita = carteiraRein;
    }

    private void AoSaldoReinAlterado(long reinUnidades)
    {
        NotificarStatusAlterado();
    }

    public CarteiraReinPlayer ObterCarteiraRein()
    {
        GarantirCarteiraRein();
        return carteiraRein;
    }

    public long GetReinUnidades()
    {
        return ObterCarteiraRein() != null ? carteiraRein.ReinUnidades : 0L;
    }

    public string ObterReinFormatado()
    {
        CarteiraReinPlayer carteira = ObterCarteiraRein();
        return carteira != null ? carteira.ObterSaldoFormatado() : "0";
    }

    private static string NormalizarNomeAtributo(string nomeAtributo)
    {
        string texto = nomeAtributo.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder resultado = new StringBuilder(texto.Length);

        for (int i = 0; i < texto.Length; i++)
        {
            UnicodeCategory categoria = CharUnicodeInfo.GetUnicodeCategory(texto[i]);
            if (categoria != UnicodeCategory.NonSpacingMark)
                resultado.Append(texto[i]);
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }

    public int GetExperienciaAtual()
    {
        return experienciaAtual;
    }

    public int GetExperienciaParaProximoNivel()
    {
        return experienciaParaProximoNivel;
    }

    public int GetNivel()
    {
        return nivel;
    }

    public int GetPontosStatusDisponiveis()
    {
        return pontosStatusDisponiveis;
    }

    public string GetNome()
    {
        return nome;
    }

    public string GetRaca()
    {
        return raca;
    }

    public string GetClasse()
    {
        return classe;
    }

    public string GetTitulo()
    {
        return titulo;
    }

    public int GetVidaAtual()
    {
        return Mathf.Clamp(vidaAtual, 0, GetVidaMaxima());
    }

    public int GetVidaMaxima()
    {
        return SomarLimiteRecurso(vidaMaxima, GetBonusHP());
    }

    public int GetVidaMaximaBase() => Mathf.Max(1, vidaMaxima);
    public int GetManaMaximaBase() => Mathf.Max(1, manaMaxima);

    public int GetManaAtual()
    {
        return Mathf.Clamp(manaAtual, 0, GetManaMaxima());
    }

    public int GetManaMaxima()
    {
        return SomarLimiteRecurso(manaMaxima, GetManaTotal());
    }

    internal static int SaturarAtributo(long valor)
    {
        return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, valor));
    }

    internal static int SomarLimiteRecurso(int limiteBase, int adicional)
    {
        return (int)Math.Min(int.MaxValue, Math.Max(1L, (long)limiteBase + adicional));
    }

    private long ObterBonusArmas(TipoAtributoManopla atributo)
    {
        return (long)Espada.ObterBonusEmpunhado(this, atributo) + Flecha.ObterBonusEmUso(this, atributo) +
            Machado.ObterBonusEmpunhado(this, atributo) + Picareta.ObterBonusEmpunhado(this, atributo) +
            Escudo.ObterBonusEmpunhado(this, atributo);
    }

    public int GetBonusHP()
    {
        return SaturarAtributo((long)ObterBonusManoplas(TipoAtributoManopla.HP) + ObterBonusPeitoral(TipoAtributoManopla.HP) + ObterBonusElmo(TipoAtributoManopla.HP) + ObterBonusArmas(TipoAtributoManopla.HP));
    }

    private int ultimoLimiteHP = -1, ultimoLimiteMP = -1;

    private void LateUpdate()
    {
        AtualizarLimitesRecursos();
    }

    public void AtualizarAtributosEquipamentos()
    {
        AtualizarLimitesRecursos();
        NotificarStatusAlterado();
    }

    public void AtualizarLimitesRecursos()
    {
        int hp = GetVidaMaxima(), mp = GetManaMaxima();
        int anteriorHP = vidaAtual, anteriorMP = manaAtual;
        vidaAtual = Mathf.Clamp(vidaAtual, 0, hp);
        manaAtual = Mathf.Clamp(manaAtual, 0, mp);
        if (vidaAtualRealInicializada) vidaAtualReal = Mathf.Clamp(vidaAtualReal, 0f, hp);
        bool mudou = hp != ultimoLimiteHP || mp != ultimoLimiteMP || anteriorHP != vidaAtual || anteriorMP != manaAtual;
        ultimoLimiteHP = hp; ultimoLimiteMP = mp;
        if (mudou) NotificarStatusAlterado();
    }

    public bool TentarConsumirMana(int quantidade)
    {
        if (quantidade <= 0)
            return true;

        manaMaxima = Mathf.Max(1, manaMaxima);
        manaAtual = Mathf.Clamp(manaAtual, 0, GetManaMaxima());

        if (manaAtual < quantidade)
            return false;

        manaAtual = Mathf.Max(0, manaAtual - quantidade);
        NotificarStatusAlterado();
        return true;
    }

    public void RecuperarMana(int quantidade)
    {
        if (quantidade <= 0)
            return;

        manaMaxima = Mathf.Max(1, manaMaxima);
        int manaAnterior = Mathf.Clamp(manaAtual, 0, GetManaMaxima());
        manaAtual = (int)Math.Min(GetManaMaxima(), (long)manaAnterior + quantidade);

        if (manaAtual != manaAnterior)
            NotificarStatusAlterado();
    }

    private void IniciarRegeneracaoMana()
    {
        if (rotinaRegeneracaoMana != null || !isActiveAndEnabled)
            return;

        rotinaRegeneracaoMana = StartCoroutine(RotinaRegeneracaoMana());
    }

    private void PararRegeneracaoMana()
    {
        if (rotinaRegeneracaoMana == null)
            return;

        StopCoroutine(rotinaRegeneracaoMana);
        rotinaRegeneracaoMana = null;
    }

    private IEnumerator RotinaRegeneracaoMana()
    {
        WaitForSeconds espera = new WaitForSeconds(Mathf.Max(0.1f, intervaloRecuperacaoMana));

        while (true)
        {
            yield return espera;

            if (!regenerarManaAutomaticamente)
                continue;

            if (!regenerarManaEnquantoMorto && playerMorto)
                continue;

            if (quantidadeManaRecuperada <= 0)
                continue;

            manaMaxima = Mathf.Max(1, manaMaxima);
            manaAtual = Mathf.Clamp(manaAtual, 0, GetManaMaxima());

            if (manaAtual >= GetManaMaxima())
                continue;

            RecuperarMana(quantidadeManaRecuperada);
        }
    }

    public int GetForca()
    {
        return forca;
    }

    public int GetConstituicao()
    {
        return constituicao;
    }

    public int GetAgilidade()
    {
        return agilidade;
    }

    public int GetVelocidade()
    {
        return velocidade;
    }

    public int GetInteligencia()
    {
        return inteligencia;
    }

    public int GetEspirito()
    {
        return espirito;
    }

    public int GetSorte()
    {
        return sorte;
    }

    public int GetDestreza()
    {
        return destreza;
    }

    public int GetVigor()
    {
        return vigor;
    }

    public int GetPercepcao()
    {
        return percepcao;
    }

    public int GetResistencia()
    {
        GarantirResistenciaInicial();
        return resistencia;
    }

    public int GetMana()
    {
        return mana;
    }

    public int GetCarisma()
    {
        return carisma;
    }

    public int GetCritico()
    {
        return critico;
    }

    public int GetDefesa()
    {
        return defesa;
    }

    private int ObterBonusElmo(TipoAtributoManopla atributo)
    {
        var equipamentos = GetComponent<GerenciadorEquipamentosPlayer>();
        var elmo = equipamentos != null ? equipamentos.ElmoAtual : null;
        if (elmo == null || !elmo.JaEquipado || !elmo.gameObject.activeInHierarchy) return 0;
        return elmo.ObterValor(atributo);
    }

    private int ObterBonusPeitoral(TipoAtributoManopla atributo)
    {
        var equipamentos = GetComponent<GerenciadorEquipamentosPlayer>();
        var peitoral = equipamentos != null ? equipamentos.PeitoralAtual : null;
        if (peitoral == null || !peitoral.JaEquipado || !peitoral.gameObject.activeInHierarchy) return 0;
        return peitoral.ObterValor(atributo);
    }

    private int ObterBonusManoplas(TipoAtributoManopla atributo)
    {
        var equipamentos = GetComponent<GerenciadorEquipamentosPlayer>();
        if (equipamentos == null) return 0;
        return SaturarAtributo((long)ObterBonusManopla(equipamentos.LuvaDireita, atributo) +
            (equipamentos.LuvaEsquerda != equipamentos.LuvaDireita ?
                ObterBonusManopla(equipamentos.LuvaEsquerda, atributo) : 0));
    }

    private static int ObterBonusManopla(EquiparManoplas luva, TipoAtributoManopla atributo)
    {
        if (luva == null || !luva.JaEquipado || !luva.gameObject.activeInHierarchy) return 0;
        var dados = luva.GetComponent<AtributosManoplas>();
        return dados != null ? dados.ObterValor(atributo) : 0;
    }

    public int GetBonusForca()
    {
        return SaturarAtributo((long)bonusForca + ObterBonusManoplas(TipoAtributoManopla.Forca) + ObterBonusPeitoral(TipoAtributoManopla.Forca) + ObterBonusElmo(TipoAtributoManopla.Forca) + ObterBonusArmas(TipoAtributoManopla.Forca));
    }

    public int GetBonusConstituicao()
    {
        return SaturarAtributo((long)bonusConstituicao + ObterBonusManoplas(TipoAtributoManopla.Constituicao) + ObterBonusPeitoral(TipoAtributoManopla.Constituicao) + ObterBonusElmo(TipoAtributoManopla.Constituicao) + ObterBonusArmas(TipoAtributoManopla.Constituicao));
    }

    public int GetBonusAgilidade()
    {
        return SaturarAtributo((long)bonusAgilidade + ObterBonusManoplas(TipoAtributoManopla.Agilidade) + ObterBonusPeitoral(TipoAtributoManopla.Agilidade) + ObterBonusElmo(TipoAtributoManopla.Agilidade) + ObterBonusArmas(TipoAtributoManopla.Agilidade));
    }

    public int GetBonusVelocidade()
    {
        return SaturarAtributo((long)bonusVelocidade + ObterBonusManoplas(TipoAtributoManopla.Velocidade) + ObterBonusPeitoral(TipoAtributoManopla.Velocidade) + ObterBonusElmo(TipoAtributoManopla.Velocidade) + ObterBonusArmas(TipoAtributoManopla.Velocidade));
    }

    public int GetBonusInteligencia()
    {
        return SaturarAtributo((long)bonusInteligencia + ObterBonusManoplas(TipoAtributoManopla.Inteligencia) + ObterBonusPeitoral(TipoAtributoManopla.Inteligencia) + ObterBonusElmo(TipoAtributoManopla.Inteligencia) + ObterBonusArmas(TipoAtributoManopla.Inteligencia));
    }

    public int GetBonusEspirito()
    {
        return SaturarAtributo((long)bonusEspirito + ObterBonusManoplas(TipoAtributoManopla.Espirito) + ObterBonusPeitoral(TipoAtributoManopla.Espirito) + ObterBonusElmo(TipoAtributoManopla.Espirito) + ObterBonusArmas(TipoAtributoManopla.Espirito));
    }

    public int GetBonusSorte()
    {
        return SaturarAtributo((long)bonusSorte + ObterBonusManoplas(TipoAtributoManopla.Sorte) + ObterBonusPeitoral(TipoAtributoManopla.Sorte) + ObterBonusElmo(TipoAtributoManopla.Sorte) + ObterBonusArmas(TipoAtributoManopla.Sorte));
    }

    public int GetBonusDestreza()
    {
        return SaturarAtributo((long)bonusDestreza + ObterBonusManoplas(TipoAtributoManopla.Destreza) + ObterBonusPeitoral(TipoAtributoManopla.Destreza) + ObterBonusElmo(TipoAtributoManopla.Destreza) + ObterBonusArmas(TipoAtributoManopla.Destreza));
    }

    public int GetBonusVigor()
    {
        return SaturarAtributo((long)bonusVigor + ObterBonusManoplas(TipoAtributoManopla.Vigor) + ObterBonusPeitoral(TipoAtributoManopla.Vigor) + ObterBonusElmo(TipoAtributoManopla.Vigor) + ObterBonusArmas(TipoAtributoManopla.Vigor));
    }

    public int GetBonusPercepcao()
    {
        return SaturarAtributo((long)bonusPercepcao + ObterBonusManoplas(TipoAtributoManopla.Percepcao) + ObterBonusPeitoral(TipoAtributoManopla.Percepcao) + ObterBonusElmo(TipoAtributoManopla.Percepcao) + ObterBonusArmas(TipoAtributoManopla.Percepcao));
    }

    public int GetBonusResistencia()
    {
        return SaturarAtributo((long)bonusResistencia + ObterBonusManoplas(TipoAtributoManopla.Resistencia) + ObterBonusPeitoral(TipoAtributoManopla.Resistencia) + ObterBonusElmo(TipoAtributoManopla.Resistencia) + ObterBonusArmas(TipoAtributoManopla.Resistencia));
    }

    public int GetBonusMana()
    {
        return SaturarAtributo((long)bonusMana + ObterBonusManoplas(TipoAtributoManopla.Mana) + ObterBonusPeitoral(TipoAtributoManopla.Mana) + ObterBonusElmo(TipoAtributoManopla.Mana) + ObterBonusArmas(TipoAtributoManopla.Mana));
    }

    public int GetBonusCarisma()
    {
        return SaturarAtributo((long)bonusCarisma + ObterBonusManoplas(TipoAtributoManopla.Carisma) + ObterBonusPeitoral(TipoAtributoManopla.Carisma) + ObterBonusElmo(TipoAtributoManopla.Carisma) + ObterBonusArmas(TipoAtributoManopla.Carisma));
    }

    public int GetBonusCritico()
    {
        return SaturarAtributo((long)bonusCritico + ObterBonusManoplas(TipoAtributoManopla.Critico) + ObterBonusPeitoral(TipoAtributoManopla.Critico) + ObterBonusElmo(TipoAtributoManopla.Critico) + ObterBonusArmas(TipoAtributoManopla.Critico));
    }

    public int GetBonusDefesa()
    {
        return SaturarAtributo((long)bonusDefesa + ObterBonusManoplas(TipoAtributoManopla.Defesa) + ObterBonusPeitoral(TipoAtributoManopla.Defesa) + ObterBonusElmo(TipoAtributoManopla.Defesa) + ObterBonusArmas(TipoAtributoManopla.Defesa));
    }

    public int GetForcaTotal()
    {
        return SaturarAtributo((long)forca + GetBonusForca());
    }

    public int GetConstituicaoTotal()
    {
        return SaturarAtributo((long)constituicao + GetBonusConstituicao());
    }

    public int GetAgilidadeTotal()
    {
        return SaturarAtributo((long)agilidade + GetBonusAgilidade());
    }

    public int GetVelocidadeTotal()
    {
        return SaturarAtributo((long)velocidade + GetBonusVelocidade());
    }

    public int GetInteligenciaTotal()
    {
        return SaturarAtributo((long)inteligencia + GetBonusInteligencia());
    }

    public int GetEspiritoTotal()
    {
        return SaturarAtributo((long)espirito + GetBonusEspirito());
    }

    public int GetSorteTotal()
    {
        return SaturarAtributo((long)sorte + GetBonusSorte());
    }

    public int GetDestrezaTotal()
    {
        return SaturarAtributo((long)destreza + GetBonusDestreza());
    }

    public int GetVigorTotal()
    {
        return SaturarAtributo((long)vigor + GetBonusVigor());
    }

    public int GetPercepcaoTotal()
    {
        return SaturarAtributo((long)percepcao + GetBonusPercepcao());
    }

    public int GetResistenciaTotal()
    {
        GarantirResistenciaInicial();
        return SaturarAtributo((long)resistencia + GetBonusResistencia());
    }

    public int GetManaTotal()
    {
        return SaturarAtributo((long)mana + GetBonusMana());
    }

    public int GetCarismaTotal()
    {
        return SaturarAtributo((long)carisma + GetBonusCarisma());
    }

    public int GetCriticoTotal()
    {
        return SaturarAtributo((long)critico + GetBonusCritico());
    }

    public int GetDefesaTotal()
    {
        return SaturarAtributo((long)defesa + GetBonusDefesa());
    }
}
