using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class Cocumelo : MonoBehaviour
{
    [Header("Respawn do cogumelo (usa estes valores)")]
    [FormerlySerializedAs("idCocumelo")]
    [SerializeField] private string respawnId = "";
    [Min(0.1f)] public float distanciaParaRespawn = 1f;
    [Tooltip("Segundos de espera por reposicao neste ponto.")]
    [Min(0f)] public float tempoRespawn = 30f;
    [Tooltip("Elevacao sobre o ponto original; nao se acumula entre geracoes.")]
    [Min(0f)] public float alturaRespawn = 0.5f;

    private Vector3 posicaoOriginal;
    private Quaternion rotacaoOriginal;
    private bool baseOriginalCapturada;
    private bool desarmadoComoItemColetado;
    private bool reposicao;
    private bool hierarquiaOrganizada;
    private bool coletaJogadorConfirmada;
    private XRGrabInteractable grab;

    private void Awake() { CapturarBaseSeNecessario(); }
    private void OnEnable()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (grab != null) grab.selectEntered.AddListener(AoPegar);
    }
    private void OnDisable()
    {
        if (grab != null) grab.selectEntered.RemoveListener(AoPegar);
    }
    private void AoPegar(SelectEnterEventArgs args)
    {
        if (args.interactorObject != null && !(args.interactorObject is XRSocketInteractor) && RespawnNatureza.PodeProcessarRespawn())
            coletaJogadorConfirmada = true;
    }
    internal void NotificarEntradaInventario()
    {
        if (!RespawnNatureza.PodeProcessarRespawn() ||
            (SaveManager.Instancia != null && SaveManager.Instancia.EstadoCarregamento == SaveManager.EstadoCarga.EmAndamento)) return;
        coletaJogadorConfirmada = true;
        if (!desarmadoComoItemColetado && RespawnNatureza.Instancia != null)
            RespawnNatureza.Instancia.AgendarRespawnCogumelo(this);
    }
    private void Start() { RegistrarPonto(); }
    private void Update()
    {
        if (!RespawnNatureza.PodeProcessarRespawn()) return;
        OrganizarClone();
        RegistrarPonto();
        if (!coletaJogadorConfirmada || desarmadoComoItemColetado || EstaNoPontoOriginal()) return;
        var manager = RespawnNatureza.Instancia;
        if (manager != null && manager.AgendarRespawnCogumelo(this))
            desarmadoComoItemColetado = true;
    }

    private void OnValidate()
    {
        distanciaParaRespawn = Mathf.Max(0.1f, distanciaParaRespawn);
        tempoRespawn = Mathf.Max(0f, tempoRespawn);
        alturaRespawn = Mathf.Max(0f, alturaRespawn);
    }

    internal void CapturarBaseSeNecessario()
    {
        if (baseOriginalCapturada) return;
        posicaoOriginal = transform.position;
        rotacaoOriginal = transform.rotation;
        baseOriginalCapturada = true;
    }
    private void RegistrarPonto()
    {
        if (!desarmadoComoItemColetado && RespawnNatureza.Instancia != null)
            RespawnNatureza.Instancia.RegistrarPontoCogumelo(this);
    }
    public string ObterRespawnId()
    {
        if (string.IsNullOrWhiteSpace(respawnId))
        {
            var dados = GetComponent<ItemInventarioDados>();
            respawnId = dados != null ? dados.NomeItem : gameObject.name.Replace("(Clone)", "").Trim();
        }
        // IDs legados destes prefabs usavam espacos, mas a tabela usa underscores.
        respawnId = (respawnId ?? string.Empty).Trim();
        switch (respawnId)
        {
            case "Mushroom Orange": respawnId = "Mushroom_Orange"; break;
            case "Mushroom 01": respawnId = "Mushroom_01"; break;
            case "Mushroom 02": respawnId = "Mushroom_02"; break;
        }
        return respawnId;
    }
    public string ObterIdCocumelo() => ObterRespawnId();
    public bool EstaDesarmadoComoItemColetado() => desarmadoComoItemColetado;
    public void DesarmarComoItemColetado() { desarmadoComoItemColetado = true; }
    internal bool ColetaConfirmada => coletaJogadorConfirmada;
    internal void RearmarAposRetorno()
    {
        desarmadoComoItemColetado = false;
        coletaJogadorConfirmada = false;
        if (grab != null)
            foreach (var seletor in grab.interactorsSelecting)
                if (!(seletor is XRSocketInteractor)) { coletaJogadorConfirmada = true; break; }
    }
    internal Vector3 PontoOriginal { get { CapturarBaseSeNecessario(); return posicaoOriginal; } }
    internal Quaternion RotacaoOriginal { get { CapturarBaseSeNecessario(); return rotacaoOriginal; } }

    internal bool EstaNoPontoOriginal()
    {
        CapturarBaseSeNecessario();
        var item = GetComponent<ItemPersistente>();
        if (item != null && item.EstaNoInventario()) return false;
        Vector3 atual = transform.position;
        // A queda dos 0.5m de nascimento ate o solo nao e uma nova coleta.
        float minimoY = posicaoOriginal.y;
        float maximoY = minimoY + (reposicao ? alturaRespawn : 0f);
        float deltaY = atual.y < minimoY ? minimoY - atual.y : Mathf.Max(0f, atual.y - maximoY);
        Vector3 deslocamento = new Vector3(atual.x - posicaoOriginal.x, deltaY, atual.z - posicaoOriginal.z);
        return Vector3.Distance(deslocamento, Vector3.zero) < Mathf.Max(0.1f, distanciaParaRespawn);
    }

    internal bool EhReposicao => reposicao;
    internal void OrganizarClone()
    {
        var persistente = GetComponent<ItemPersistente>();
        if (hierarquiaOrganizada || (persistente != null && persistente.EstaNoInventario())) return;
        var manager = RespawnNatureza.Instancia;
        if (manager == null) return;
        var pai = reposicao ? manager.ObterPastaCogumelos() : manager.ObterGrupoCogumelos();
        if (pai == null) return;
        transform.SetParent(pai, true);
        hierarquiaOrganizada = true;
    }

    internal void PrepararNoPonto(Vector3 ponto, Quaternion rotacao, float distancia, float espera, float altura, bool ehReposicao = true)
    {
        posicaoOriginal = ponto; rotacaoOriginal = rotacao; baseOriginalCapturada = true;
        distanciaParaRespawn = Mathf.Max(0.1f, distancia); tempoRespawn = Mathf.Max(0f, espera);
        alturaRespawn = Mathf.Max(0f, altura); desarmadoComoItemColetado = false;
        reposicao = ehReposicao; hierarquiaOrganizada = false; coletaJogadorConfirmada = false;
    }

    internal System.Action CapturarRecuperacaoRespawn()
    {
        var ponto = posicaoOriginal; var rotacao = rotacaoOriginal;
        bool capturada = baseOriginalCapturada, coletado = desarmadoComoItemColetado;
        bool clone = reposicao, organizado = hierarquiaOrganizada, coleta = coletaJogadorConfirmada;
        return () =>
        {
            posicaoOriginal = ponto; rotacaoOriginal = rotacao; baseOriginalCapturada = capturada;
            desarmadoComoItemColetado = coletado; reposicao = clone; hierarquiaOrganizada = organizado; coletaJogadorConfirmada = coleta;
        };
    }

    internal void CapturarEstadoRespawn(ItemPersistente.EstadoCenaItem estado)
    {
        CapturarBaseSeNecessario();
        estado.possuiPontoCogumelo = true;
        estado.pontoCogumelo = posicaoOriginal;
        estado.rotacaoPontoCogumelo = rotacaoOriginal.eulerAngles;
        estado.cogumeloColetado = desarmadoComoItemColetado;
        estado.cogumeloReposicao = reposicao;
        estado.coletaJogadorConfirmada = coletaJogadorConfirmada;
        estado.distanciaPontoCogumelo = distanciaParaRespawn;
        estado.tempoPontoCogumelo = tempoRespawn;
        estado.alturaPontoCogumelo = alturaRespawn;
    }
    internal void RestaurarEstadoRespawn(ItemPersistente.EstadoCenaItem estado, bool originalDeCena)
    {
        if (estado.possuiPontoCogumelo)
        {
            posicaoOriginal = estado.pontoCogumelo;
            rotacaoOriginal = Quaternion.Euler(estado.rotacaoPontoCogumelo);
            baseOriginalCapturada = true;
            desarmadoComoItemColetado = estado.cogumeloColetado;
            reposicao = estado.cogumeloReposicao;
            coletaJogadorConfirmada = estado.coletaJogadorConfirmada;
            distanciaParaRespawn = estado.distanciaPontoCogumelo;
            tempoRespawn = estado.tempoPontoCogumelo;
            alturaRespawn = estado.alturaPontoCogumelo;
        }
        else if (!originalDeCena)
        {
            // Copias antigas nao tem ponto confiavel; continuam como itens utilizaveis,
            // mas nao criam novos pontos na posicao temporaria de Instantiate/save.
            DesarmarComoItemColetado();
            reposicao = true;
        }
        hierarquiaOrganizada = false;
    }
}
