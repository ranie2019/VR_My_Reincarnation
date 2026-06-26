using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Porta : MonoBehaviour
{
    [Header("Dobradicas")]
    public Transform dobradicaSuperior;
    public Transform dobradicaInferior;

    [Header("Angulo")]
    public float anguloMinimo = -95f;
    public float anguloMaximo = 95f;
    public bool inverterDirecao = false;

    [Header("Direcao")]
    [SerializeField] private Transform referenciaLadoExterno;

    [Header("Movimento")]
    public float sensibilidadeEmpurrao = 1f;
    public float velocidadeAbertura = 120f;
    public float amortecimentoMovimento = 8f;
    public float tempoParaFechar = 3f;
    public float velocidadeRetorno = 90f;

    [Header("Estado")]
    public float anguloAtual;

    [Header("Debug")]
    [SerializeField] private bool debugPorta = false;

    const float DistanciaMinimaDobradicas = 0.05f;
    const float DistanciaMinimaDeteccaoLado = 0.001f;
    const float SnapAnguloFechado = 0.15f;
    const float AnguloQuaseFechado = 2f;
    const float TempoLiberacaoDirecao = 0.15f;

    readonly HashSet<Collider> collidersEmpurrando = new HashSet<Collider>();

    Rigidbody rb;
    bool dobradicasValidas;
    bool avisoDobradicasMostrado;

    Vector3 posicaoInicial;
    Quaternion rotacaoInicial;
    Vector3 pivoInicial;
    Vector3 eixoInicial;
    Vector3 vetorInicialAtePorta;
    Vector3 direcaoReferenciaPorta;
    Vector3 normalPortaFechada;

    float sinalLadoExterno = 1f;

    float anguloAlvo;
    float velocidadeAngularSuave;
    float tempoSemColisao;
    bool direcaoTravada;

    void Awake()
    {
        posicaoInicial = transform.position;
        rotacaoInicial = transform.rotation;

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        AtualizarDadosDasDobradicas();

        if (dobradicasValidas)
        {
            AplicarPoseDaPorta(0f);
        }
        else
        {
            CorrigirPosicaoFisica();
        }
    }

    void OnValidate()
    {
        if (anguloMinimo > anguloMaximo)
        {
            float temporario = anguloMinimo;
            anguloMinimo = anguloMaximo;
            anguloMaximo = temporario;
        }

        sensibilidadeEmpurrao = Mathf.Max(0f, sensibilidadeEmpurrao);
        velocidadeAbertura = Mathf.Max(0f, velocidadeAbertura);
        amortecimentoMovimento = Mathf.Max(0.01f, amortecimentoMovimento);
        tempoParaFechar = Mathf.Max(0f, tempoParaFechar);
        velocidadeRetorno = Mathf.Max(0f, velocidadeRetorno);

        anguloAtual = Mathf.Clamp(anguloAtual, anguloMinimo, anguloMaximo);
    }

    void FixedUpdate()
    {
        if (!dobradicasValidas)
        {
            CorrigirPosicaoFisica();
            return;
        }

        LimparCollidersInativos();

        bool recebendoEmpurrao = collidersEmpurrando.Count > 0;

        if (recebendoEmpurrao)
        {
            tempoSemColisao = 0f;
        }
        else
        {
            tempoSemColisao += Time.fixedDeltaTime;

            if (direcaoTravada && tempoSemColisao >= TempoLiberacaoDirecao)
            {
                direcaoTravada = false;
            }

            if (tempoSemColisao >= tempoParaFechar)
            {
                anguloAlvo = Mathf.MoveTowards(
                    anguloAlvo,
                    0f,
                    velocidadeRetorno * Time.fixedDeltaTime
                );
            }
        }

        float tempoSuavizacao = 1f / Mathf.Max(0.01f, amortecimentoMovimento);
        bool retornandoAutomaticamente = !recebendoEmpurrao && tempoSemColisao >= tempoParaFechar;
        float velocidadeMaximaMovimento = retornandoAutomaticamente
            ? Mathf.Infinity
            : velocidadeAbertura * sensibilidadeEmpurrao;
        anguloAtual = Mathf.SmoothDampAngle(
            anguloAtual,
            anguloAlvo,
            ref velocidadeAngularSuave,
            tempoSuavizacao,
            velocidadeMaximaMovimento,
            Time.fixedDeltaTime
        );

        anguloAtual = Mathf.Clamp(anguloAtual, anguloMinimo, anguloMaximo);

        if (!recebendoEmpurrao && tempoSemColisao >= tempoParaFechar && Mathf.Abs(anguloAtual) <= SnapAnguloFechado)
        {
            anguloAtual = 0f;
            anguloAlvo = 0f;
            velocidadeAngularSuave = 0f;
            direcaoTravada = false;
        }

        AplicarPoseDaPorta(anguloAtual);
    }

    void LateUpdate()
    {
        if (dobradicasValidas)
        {
            AplicarPoseDaPorta(anguloAtual);
        }
        else
        {
            CorrigirPosicaoFisica();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        ProcessarEntradaValida(collision.collider, "OnCollisionEnter");
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        ManterContatoValido(collision.collider);
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision != null)
        {
            RemoverColliderAtivo(collision.collider);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        ProcessarEntradaValida(other, "OnTriggerEnter");
    }

    void OnTriggerStay(Collider other)
    {
        ManterContatoValido(other);
    }

    void OnTriggerExit(Collider other)
    {
        RemoverColliderAtivo(other);
    }

    void ProcessarEntradaValida(Collider other, string origemEvento)
    {
        if (!EhEmpurradorValido(other, out string motivo))
        {
            LogDebug($"Collider ignorado em {origemEvento}: {NomeCollider(other)}. Motivo: {motivo}");
            return;
        }

        bool novoContato = collidersEmpurrando.Add(other);
        tempoSemColisao = 0f;

        if (novoContato)
        {
            LogDebug($"Collider aceito em {origemEvento}: {NomeCollider(other)}");
        }

        bool unicoEmpurrador = collidersEmpurrando.Count == 1;
        bool podeDefinirDirecao = !direcaoTravada || (PortaQuaseFechada() && unicoEmpurrador);

        if (!podeDefinirDirecao)
        {
            LogDebug($"Direcao mantida para {NomeCollider(other)}. Alvo atual: {anguloAlvo:F1}");
            return;
        }

        if (DefinirAlvoPeloLado(other))
        {
            direcaoTravada = true;
        }
    }

    void ManterContatoValido(Collider other)
    {
        if (!EhEmpurradorValido(other, out _))
        {
            collidersEmpurrando.Remove(other);
            return;
        }

        bool novoContato = collidersEmpurrando.Add(other);
        tempoSemColisao = 0f;

        if (novoContato)
        {
            LogDebug($"Collider aceito apenas para manter contato: {NomeCollider(other)}");
        }
    }

    void RemoverColliderAtivo(Collider other)
    {
        if (other == null)
        {
            return;
        }

        collidersEmpurrando.Remove(other);
    }

    void LimparCollidersInativos()
    {
        if (collidersEmpurrando.Count == 0)
        {
            return;
        }

        collidersEmpurrando.RemoveWhere(colliderAtivo =>
            colliderAtivo == null ||
            !colliderAtivo.enabled ||
            !colliderAtivo.gameObject.activeInHierarchy ||
            !EhEmpurradorValido(colliderAtivo, out _)
        );
    }

    bool EhEmpurradorValido(Collider outro, out string motivo)
    {
        if (outro == null)
        {
            motivo = "collider nulo";
            return false;
        }

        if (!outro.enabled || !outro.gameObject.activeInHierarchy)
        {
            motivo = "collider desativado";
            return false;
        }

        if (PertenceAEstruturaDaPorta(outro.transform))
        {
            motivo = "pertence a porta, moldura ou estrutura pai";
            return false;
        }

        Rigidbody outroRigidbody = outro.attachedRigidbody;
        if (outroRigidbody != null && outroRigidbody != rb && !outroRigidbody.isKinematic)
        {
            motivo = string.Empty;
            return true;
        }

        CharacterController characterController = outro.GetComponentInParent<CharacterController>();
        if (characterController != null && characterController.enabled && characterController.gameObject.activeInHierarchy)
        {
            motivo = string.Empty;
            return true;
        }

        if (PossuiIdentidadeDeJogador(outro.transform))
        {
            motivo = string.Empty;
            return true;
        }

        motivo = outroRigidbody == null
            ? "sem Rigidbody dinamico ou CharacterController"
            : "Rigidbody cinematico";
        return false;
    }

    bool PertenceAEstruturaDaPorta(Transform outroTransform)
    {
        if (outroTransform == null)
        {
            return false;
        }

        if (outroTransform == transform || outroTransform.IsChildOf(transform))
        {
            return true;
        }

        Transform estruturaPai = transform.parent;
        if (estruturaPai == null)
        {
            return false;
        }

        return outroTransform == estruturaPai ||
               outroTransform.IsChildOf(estruturaPai) ||
               estruturaPai.IsChildOf(outroTransform);
    }

    bool PossuiIdentidadeDeJogador(Transform origem)
    {
        for (Transform atual = origem; atual != null; atual = atual.parent)
        {
            string tagAtual = atual.gameObject.tag;
            string layerAtual = LayerMask.LayerToName(atual.gameObject.layer);

            if (EhNomeDeJogadorMaoOuControle(tagAtual) || EhNomeDeJogadorMaoOuControle(layerAtual))
            {
                return true;
            }
        }

        return false;
    }

    bool EhNomeDeJogadorMaoOuControle(string valor)
    {
        return string.Equals(valor, "Player", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(valor, "Hand", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(valor, "Controller", System.StringComparison.OrdinalIgnoreCase);
    }

    bool PortaQuaseFechada()
    {
        return Mathf.Abs(anguloAtual) <= AnguloQuaseFechado;
    }

    bool DefinirAlvoPeloLado(Collider outro)
    {
        if (!dobradicasValidas || outro == null)
        {
            return false;
        }

        Vector3 centroCollider = outro.bounds.center;
        float distanciaAoPlano = Vector3.Dot(
            centroCollider - posicaoInicial,
            normalPortaFechada
        );

        if (Mathf.Abs(distanciaAoPlano) < DistanciaMinimaDeteccaoLado)
        {
            distanciaAoPlano = Vector3.Dot(
                outro.transform.position - posicaoInicial,
                normalPortaFechada
            );
        }

        if (Mathf.Abs(distanciaAoPlano) < DistanciaMinimaDeteccaoLado)
        {
            LogDebug($"Nao foi possivel determinar o lado de {NomeCollider(outro)}.");
            return false;
        }

        float sinalLadoCollider = Mathf.Sign(distanciaAoPlano);
        bool colliderNoLadoExterno = Mathf.Approximately(
            sinalLadoCollider,
            sinalLadoExterno
        );

        float sinalAlvo = colliderNoLadoExterno
            ? -sinalLadoExterno
            : sinalLadoExterno;

        if (inverterDirecao)
        {
            sinalAlvo = -sinalAlvo;
        }

        anguloAlvo = sinalAlvo > 0f ? anguloMaximo : anguloMinimo;
        tempoSemColisao = 0f;

        string ladoDetectado = colliderNoLadoExterno ? "externo" : "interno";
        LogDebug($"Lado detectado: {ladoDetectado}. Collider: {NomeCollider(outro)}. Angulo alvo: {anguloAlvo:F1}");
        return true;
    }

    string NomeCollider(Collider outro)
    {
        return outro != null ? outro.name : "<nulo>";
    }

    void LogDebug(string mensagem)
    {
        if (debugPorta)
        {
            { }
        }
    }

    void AtualizarDadosDasDobradicas()
    {
        dobradicasValidas = false;

        if (dobradicaSuperior == null || dobradicaInferior == null)
        {
            MostrarAvisoDobradica("Configure dobradicaSuperior e dobradicaInferior no Inspector.");
            return;
        }

        Vector3 inferior = dobradicaInferior.position;
        Vector3 superior = dobradicaSuperior.position;
        Vector3 eixo = superior - inferior;

        if (eixo.magnitude < DistanciaMinimaDobradicas)
        {
            MostrarAvisoDobradica("A distancia entre as dobradicas e muito pequena.");
            return;
        }

        pivoInicial = inferior;
        eixoInicial = eixo.normalized;
        vetorInicialAtePorta = posicaoInicial - pivoInicial;
        direcaoReferenciaPorta = Vector3.ProjectOnPlane(vetorInicialAtePorta, eixoInicial);

        if (direcaoReferenciaPorta.sqrMagnitude < 0.0001f)
        {
            direcaoReferenciaPorta = Vector3.ProjectOnPlane(transform.forward, eixoInicial);
        }

        if (direcaoReferenciaPorta.sqrMagnitude < 0.0001f)
        {
            direcaoReferenciaPorta = Vector3.ProjectOnPlane(transform.right, eixoInicial);
        }

        direcaoReferenciaPorta.Normalize();
        normalPortaFechada = Vector3.Cross(
            eixoInicial,
            direcaoReferenciaPorta
        ).normalized;

        sinalLadoExterno = 1f;

        if (referenciaLadoExterno != null)
        {
            float distanciaReferenciaAoPlano = Vector3.Dot(
                referenciaLadoExterno.position - posicaoInicial,
                normalPortaFechada
            );

            if (Mathf.Abs(distanciaReferenciaAoPlano) >= DistanciaMinimaDeteccaoLado)
            {
                sinalLadoExterno = Mathf.Sign(distanciaReferenciaAoPlano);
            }
        }

        dobradicasValidas = true;
    }

    void MostrarAvisoDobradica(string mensagem)
    {
        if (avisoDobradicasMostrado)
        {
            return;
        }

        avisoDobradicasMostrado = true;
        { }
    }

    void AplicarPoseDaPorta(float angulo)
    {
        Quaternion rotacaoEmTornoDaDobradica = Quaternion.AngleAxis(angulo, eixoInicial);
        transform.position = pivoInicial + rotacaoEmTornoDaDobradica * vetorInicialAtePorta;
        transform.rotation = rotacaoEmTornoDaDobradica * rotacaoInicial;

        if (rb != null)
        {
            rb.position = transform.position;
            rb.rotation = transform.rotation;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    void CorrigirPosicaoFisica()
    {
        transform.position = posicaoInicial;
        transform.rotation = rotacaoInicial;

        if (rb != null)
        {
            rb.position = posicaoInicial;
            rb.rotation = rotacaoInicial;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

}
