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

    [Header("Movimento")]
    public float sensibilidadeEmpurrao = 1f;
    public float velocidadeAbertura = 120f;
    public float amortecimentoMovimento = 8f;
    public float tempoParaFechar = 3f;
    public float velocidadeRetorno = 90f;

    [Header("Estado")]
    public float anguloAtual;

    const float DistanciaMinimaDobradicas = 0.05f;
    const float SnapAnguloFechado = 0.15f;

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

    float anguloAlvo;
    float velocidadeAngularSuave;
    float tempoSemColisao;

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
        anguloAtual = Mathf.SmoothDampAngle(
            anguloAtual,
            anguloAlvo,
            ref velocidadeAngularSuave,
            tempoSuavizacao,
            Mathf.Infinity,
            Time.fixedDeltaTime
        );

        anguloAtual = Mathf.Clamp(anguloAtual, anguloMinimo, anguloMaximo);

        if (!recebendoEmpurrao && tempoSemColisao >= tempoParaFechar && Mathf.Abs(anguloAtual) <= SnapAnguloFechado)
        {
            anguloAtual = 0f;
            anguloAlvo = 0f;
            velocidadeAngularSuave = 0f;
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
        RegistrarColliderAtivo(collision.collider);
        ProcessarColisao(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        RegistrarColliderAtivo(collision.collider);
        ProcessarColisao(collision);
    }

    void OnCollisionExit(Collision collision)
    {
        RemoverColliderAtivo(collision.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        RegistrarColliderAtivo(other);
        ProcessarTrigger(other);
    }

    void OnTriggerStay(Collider other)
    {
        RegistrarColliderAtivo(other);
        ProcessarTrigger(other);
    }

    void OnTriggerExit(Collider other)
    {
        RemoverColliderAtivo(other);
    }

    void RegistrarColliderAtivo(Collider other)
    {
        if (other == null)
        {
            return;
        }

        collidersEmpurrando.Add(other);
        tempoSemColisao = 0f;
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
            !colliderAtivo.gameObject.activeInHierarchy
        );
    }

    void ProcessarColisao(Collision collision)
    {
        if (!dobradicasValidas || collision == null)
        {
            return;
        }

        if (collision.contactCount == 0)
        {
            return;
        }

        ContactPoint contato = collision.GetContact(0);
        Vector3 pontoContato = contato.point;
        Vector3 direcaoEmpurrao = collision.relativeVelocity;

        if (direcaoEmpurrao.sqrMagnitude < 0.0001f)
        {
            direcaoEmpurrao = -contato.normal;
        }

        float intensidadeExtra = 0f;

        if (collision.rigidbody != null)
        {
            intensidadeExtra = collision.rigidbody.mass * 0.08f;
        }

        AplicarEmpurrao(pontoContato, direcaoEmpurrao, intensidadeExtra);
    }

    void ProcessarTrigger(Collider other)
    {
        if (!dobradicasValidas || other == null)
        {
            return;
        }

        Vector3 pontoContato = other.ClosestPoint(transform.position);
        Vector3 direcaoEmpurrao = Vector3.zero;
        Rigidbody outroRigidbody = other.attachedRigidbody;

        if (outroRigidbody != null)
        {
            direcaoEmpurrao = outroRigidbody.GetPointVelocity(pontoContato);
        }

        if (direcaoEmpurrao.sqrMagnitude < 0.0001f)
        {
            direcaoEmpurrao = pontoContato - pivoInicial;
        }

        AplicarEmpurrao(pontoContato, direcaoEmpurrao, 0f);
    }

    void AplicarEmpurrao(Vector3 pontoContato, Vector3 direcaoEmpurrao, float intensidadeExtra)
    {
        Vector3 radial = Vector3.ProjectOnPlane(pontoContato - pivoInicial, eixoInicial);

        if (radial.sqrMagnitude < 0.0001f)
        {
            radial = direcaoReferenciaPorta;
        }

        Vector3 tangentePositiva = Vector3.Cross(eixoInicial, radial.normalized).normalized;

        if (inverterDirecao)
        {
            tangentePositiva = -tangentePositiva;
        }

        float sinal = Mathf.Sign(Vector3.Dot(direcaoEmpurrao, tangentePositiva));

        if (Mathf.Approximately(sinal, 0f))
        {
            sinal = Mathf.Sign(Vector3.Dot(direcaoEmpurrao, direcaoReferenciaPorta));
        }

        if (Mathf.Approximately(sinal, 0f))
        {
            return;
        }

        float intensidade = Mathf.Max(0.15f, direcaoEmpurrao.magnitude + intensidadeExtra);
        float deltaAngulo = sinal * intensidade * sensibilidadeEmpurrao * velocidadeAbertura * Time.fixedDeltaTime;

        anguloAlvo = Mathf.Clamp(anguloAlvo + deltaAngulo, anguloMinimo, anguloMaximo);
        tempoSemColisao = 0f;
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
        dobradicasValidas = true;
    }

    void MostrarAvisoDobradica(string mensagem)
    {
        if (avisoDobradicasMostrado)
        {
            return;
        }

        avisoDobradicasMostrado = true;
        Debug.LogWarning($"[Porta] {mensagem} Objeto: {name}", this);
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
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

}
