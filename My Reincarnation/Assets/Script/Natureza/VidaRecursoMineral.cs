using UnityEngine;
using System.Collections;
using System;

[DisallowMultipleComponent]
public class VidaRecursoMineral : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private int vidaMax = 5;
    [SerializeField] private int vidaAtual = 5;

    [Header("Dano")]
    [SerializeField] private string tagPicareta = "Picareta";
    [SerializeField] private string nomeComponentePicareta = "Picareta";
    [SerializeField] private int danoPorHit = 1;
    [SerializeField] private float cooldownHit = 0.25f;
    [SerializeField] private bool aplicarDanoPorTriggerDireto = true;

    [Header("Spawn ao destruir")]
    [SerializeField] private GameObject prefabAoDestruir;
    [SerializeField] private Vector3 offsetSpawn = Vector3.zero;

    [Header("Áudio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somHit;
    [SerializeField] private float volumeSom = 1f;
    [SerializeField] private AudioClip somMorte;
    [SerializeField] private float volumeSomMorte = 1f;

    [Header("Debug temporário")]
    [SerializeField] private bool debugDiagnosticoContato = true;

    private bool emCooldown;
    private bool morreu;
    private int ultimoFrameSomHit = -1;

    private class RepassadorFisicaMineral : MonoBehaviour
    {
        private VidaRecursoMineral pai;

        public void Init(VidaRecursoMineral p)
        {
            pai = p;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (pai != null)
                pai.ReceberHitPorCollision(collision, "RepassadorFisicaMineral.OnCollisionEnter");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (pai != null)
                pai.ReceberHitPorTrigger(other, "RepassadorFisicaMineral.OnTriggerEnter");
        }

        private void OnTriggerStay(Collider other)
        {
            // TriggerStay nao aplica dano continuo.
            // O recurso mineral so libera novo hit apos sair do cooldown.
        }
    }

    private void Reset()
    {
        vidaMax = 5;
        vidaAtual = 5;
        tagPicareta = "Picareta";
        nomeComponentePicareta = "Picareta";
        danoPorHit = 1;
        cooldownHit = 0.25f;
        aplicarDanoPorTriggerDireto = true;
        volumeSom = 1f;
        volumeSomMorte = 1f;
        audioSource = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        vidaAtual = Mathf.Clamp(vidaAtual, 1, Mathf.Max(1, vidaMax));

        InstalarRepassadoresNosFilhos();
    }

    private void OnValidate()
    {
        vidaMax = Mathf.Max(1, vidaMax);
        vidaAtual = Mathf.Clamp(vidaAtual, 1, vidaMax);
        danoPorHit = Mathf.Max(1, danoPorHit);
        cooldownHit = Mathf.Max(0f, cooldownHit);
        volumeSom = Mathf.Clamp01(volumeSom);
        volumeSomMorte = Mathf.Clamp01(volumeSomMorte);
    }

    private void InstalarRepassadoresNosFilhos()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        foreach (Collider col in colliders)
        {
            if (col == null)
                continue;

            RepassadorFisicaMineral repassador = col.GetComponent<RepassadorFisicaMineral>();

            if (repassador == null)
                repassador = col.gameObject.AddComponent<RepassadorFisicaMineral>();

            repassador.Init(this);
        }
    }

    private void ReceberHitPorCollision(Collision collision, string origemEvento)
    {
        if (collision == null)
        {
            LogDiagnosticoContatoMineral(origemEvento, null, null, false, "Collision nula.");
            return;
        }

        TocarSomHit();

        bool colliderEhPicareta = ColliderEhPicareta(collision.collider);
        bool transformEhPicareta = TransformEhPicareta(collision.transform);

        if (morreu)
        {
            LogDiagnosticoContatoMineral(origemEvento, collision.collider, collision.transform, false, "Recurso mineral ja foi destruido.");
            return;
        }

        if (emCooldown)
        {
            LogDiagnosticoContatoMineral(origemEvento, collision.collider, collision.transform, false, "Cooldown ativo.");
            return;
        }

        if (colliderEhPicareta || transformEhPicareta)
        {
            LogDiagnosticoContatoMineral(origemEvento, collision.collider, collision.transform, true, "Picareta detectada por Collision.");
            TomarDano(danoPorHit);
            return;
        }

        LogDiagnosticoContatoMineral(origemEvento, collision.collider, collision.transform, false, "Nao encontrou tag/componente Picareta.");
    }

    private void ReceberHitPorTrigger(Collider other, string origemEvento)
    {
        if (other == null)
        {
            LogDiagnosticoContatoMineral(origemEvento, null, null, false, "Collider nulo.");
            return;
        }

        TocarSomHit();

        bool colliderEhPicareta = ColliderEhPicareta(other);

        if (morreu)
        {
            LogDiagnosticoContatoMineral(origemEvento, other, other.transform, false, "Recurso mineral ja foi destruido.");
            return;
        }

        if (emCooldown)
        {
            LogDiagnosticoContatoMineral(origemEvento, other, other.transform, false, "Cooldown ativo.");
            return;
        }

        if (colliderEhPicareta)
        {
            if (aplicarDanoPorTriggerDireto)
            {
                LogDiagnosticoContatoMineral(origemEvento, other, other.transform, true, "Picareta detectada por Trigger.");
                TomarDano(danoPorHit);
                return;
            }

            LogDiagnosticoContatoMineral(origemEvento, other, other.transform, false, "Picareta detectada por Trigger, mas dano direto por trigger esta desativado.");
            return;
        }

        LogDiagnosticoContatoMineral(origemEvento, other, other.transform, false, "Nao encontrou tag/componente Picareta.");
    }

    private bool ColliderEhPicareta(Collider colliderContato)
    {
        if (colliderContato == null)
            return false;

        if (TransformEhPicareta(colliderContato.transform))
            return true;

        Rigidbody rb = colliderContato.attachedRigidbody;
        return rb != null && TransformEhPicareta(rb.transform);
    }

    private bool TransformEhPicareta(Transform alvo)
    {
        Transform atual = alvo;

        while (atual != null)
        {
            if (TagEhPicareta(atual) || atual.GetComponent<Picareta>() != null || TemComponentePicareta(atual))
                return true;

            atual = atual.parent;
        }

        return false;
    }

    private bool TagEhPicareta(Transform alvo)
    {
        if (alvo == null)
            return false;

        string tagAtual = alvo.tag;
        return TagIgual(tagAtual, tagPicareta) ||
               TagIgual(tagAtual, "Picareta") ||
               TagIgual(tagAtual, "Pick");
    }

    private bool TemComponentePicareta(Transform alvo)
    {
        if (alvo == null || string.IsNullOrWhiteSpace(nomeComponentePicareta))
            return false;

        Component[] componentes = alvo.GetComponents<Component>();

        foreach (Component componente in componentes)
        {
            if (componente == null)
                continue;

            Type tipo = componente.GetType();

            if (string.Equals(tipo.Name, nomeComponentePicareta, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool TagIgual(string tagAtual, string tagEsperada)
    {
        return !string.IsNullOrWhiteSpace(tagAtual) &&
               !string.IsNullOrWhiteSpace(tagEsperada) &&
               string.Equals(tagAtual, tagEsperada, StringComparison.OrdinalIgnoreCase);
    }

    private void LogDiagnosticoContatoMineral(string evento, Collider colliderContato, Transform transformContato, bool aceito, string motivo)
    {
        if (!debugDiagnosticoContato)
            return;

        Transform contato = transformContato != null
            ? transformContato
            : colliderContato != null ? colliderContato.transform : null;

        Rigidbody rb = colliderContato != null ? colliderContato.attachedRigidbody : null;
        Transform root = contato != null ? contato.root : null;
        int layer = contato != null ? contato.gameObject.layer : -1;
        string layerNome = layer >= 0 ? LayerMask.LayerToName(layer) : "sem layer";
        bool encontrouPicaretaNoCollider = ColliderEhPicareta(colliderContato);
        bool encontrouPicaretaNoTransform = TransformEhPicareta(contato);

        string colliderInfo = colliderContato != null
            ? $"{colliderContato.GetType().Name} enabled={colliderContato.enabled} isTrigger={colliderContato.isTrigger}"
            : "sem collider";

        string rbInfo = rb != null
            ? $"{rb.name} isKinematic={rb.isKinematic} useGravity={rb.useGravity} detectCollisions={rb.detectCollisions}"
            : "sem attachedRigidbody";

        Debug.Log(
            $"[VidaRecursoMineral][{evento}] mineral={name} aceito={aceito} motivo=\"{motivo}\" " +
            $"contato={(contato != null ? contato.name : "null")} tag={ObterTagSegura(contato)} " +
            $"layer={layer}:{layerNome} root={(root != null ? root.name : "null")} collider={colliderInfo} " +
            $"attachedRigidbody={rbInfo} encontrouPicaretaCollider={encontrouPicaretaNoCollider} " +
            $"encontrouPicaretaTransform={encontrouPicaretaNoTransform} cooldown={emCooldown} morreu={morreu}",
            this);
    }

    private static string ObterTagSegura(Transform alvo)
    {
        return alvo != null ? alvo.tag : "null";
    }

    public bool ReceberDanoDePicareta(GameObject origem)
    {
        return ReceberDanoDePicareta(danoPorHit, origem);
    }

    public bool ReceberDanoDePicareta(int dano, GameObject origem)
    {
        if (morreu || emCooldown)
            return false;

        TomarDano(Mathf.Max(1, dano));
        return true;
    }

    private void TomarDano(int dano)
    {
        if (morreu)
            return;

        vidaAtual -= dano;

        TocarSomHit();

        if (vidaAtual <= 0)
        {
            Morrer();
            return;
        }

        StartCoroutine(Cooldown());
    }

    private IEnumerator Cooldown()
    {
        emCooldown = true;
        yield return new WaitForSeconds(cooldownHit);
        emCooldown = false;
    }

    private void Morrer()
    {
        if (morreu)
            return;

        morreu = true;
        TocarSomMorte();

        if (prefabAoDestruir != null)
            Instantiate(prefabAoDestruir, transform.position + offsetSpawn, transform.rotation);

        Destroy(gameObject);
    }

    private void TocarSom(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip, volumeSom);
    }

    private void TocarSomHit()
    {
        if (Time.frameCount == ultimoFrameSomHit)
            return;

        ultimoFrameSomHit = Time.frameCount;
        TocarSom(somHit);
    }

    private void TocarSomMorte()
    {
        if (somMorte != null)
            AudioSource.PlayClipAtPoint(somMorte, transform.position, volumeSomMorte);
    }
}
