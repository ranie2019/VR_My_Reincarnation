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

    [Header("Respawn")]
    [SerializeField] private string respawnId = "";

    private bool emCooldown;
    private bool morreu;

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
    }

    private void Awake()
    {
        vidaAtual = Mathf.Clamp(vidaAtual, 1, Mathf.Max(1, vidaMax));

        InstalarRepassadoresNosFilhos();
    }

    private void OnValidate()
    {
        vidaMax = Mathf.Max(1, vidaMax);
        vidaAtual = Mathf.Clamp(vidaAtual, 1, vidaMax);
        danoPorHit = Mathf.Max(1, danoPorHit);
        cooldownHit = Mathf.Max(0f, cooldownHit);
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
            return;
        }

        bool colliderEhPicareta = ColliderEhPicareta(collision.collider);
        bool transformEhPicareta = TransformEhPicareta(collision.transform);

        if (morreu)
        {
            return;
        }

        if (emCooldown)
        {
            return;
        }

        if (colliderEhPicareta || transformEhPicareta)
        {
            TomarDano(danoPorHit);
            return;
        }

    }

    private void ReceberHitPorTrigger(Collider other, string origemEvento)
    {
        if (other == null)
        {
            return;
        }

        bool colliderEhPicareta = ColliderEhPicareta(other);

        if (morreu)
        {
            return;
        }

        if (emCooldown)
        {
            return;
        }

        if (colliderEhPicareta)
        {
            if (aplicarDanoPorTriggerDireto)
            {
                TomarDano(danoPorHit);
                return;
            }

            return;
        }

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

        if (prefabAoDestruir != null)
            Instantiate(prefabAoDestruir, transform.position + offsetSpawn, transform.rotation);

        if (RespawnNatureza.Instancia != null && !string.IsNullOrWhiteSpace(respawnId))
        {
            RespawnNatureza.Instancia.AgendarRespawn(
                respawnId,
                transform.position,
                transform.rotation);
        }
        else if (RespawnNatureza.Instancia == null)
        {
            { }
        }
        else if (string.IsNullOrWhiteSpace(respawnId))
        {
            { }
        }

        Destroy(gameObject);
    }
}
