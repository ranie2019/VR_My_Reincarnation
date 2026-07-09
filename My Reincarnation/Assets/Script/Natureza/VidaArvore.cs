using UnityEngine;
using System.Collections;
using System;

[DisallowMultipleComponent]
public class VidaArvore : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private int vidaMax = 5;
    [SerializeField] private int vidaAtual = 5;

    [Header("Dano")]
    [SerializeField] private string tagMachado = "Machado";
    [SerializeField] private int danoPorHit = 1;
    [SerializeField] private float cooldownHit = 0.25f;

    [Header("Spawn ao destruir")]
    [SerializeField] private GameObject prefabAoDestruir;
    [SerializeField] private Vector3 offsetSpawn = Vector3.zero;

    [Header("Respawn")]
    [SerializeField] private string respawnId = "";

    private bool emCooldown;
    private bool morreu;

    private class RepassadorFisica : MonoBehaviour
    {
        private VidaArvore pai;

        public void Init(VidaArvore p)
        {
            pai = p;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (pai != null)
                pai.ReceberHitPorCollision(collision, "RepassadorFisica.OnCollisionEnter");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (pai != null)
                pai.ReceberHitPorTrigger(other, "RepassadorFisica.OnTriggerEnter");
        }

        private void OnTriggerStay(Collider other)
        {
            // TriggerStay nao aplica dano continuo. O machado libera novo hit apenas apos TriggerExit.
        }
    }

    private void Reset()
    {
        vidaMax = 5;
        vidaAtual = 5;
        tagMachado = "Machado";
        danoPorHit = 1;
        cooldownHit = 0.25f;
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

            RepassadorFisica repassador = col.GetComponent<RepassadorFisica>();

            if (repassador == null)
                repassador = col.gameObject.AddComponent<RepassadorFisica>();

            repassador.Init(this);
        }
    }

    private void ReceberHitPorCollision(Collision collision, string origemEvento)
    {
        // A arvore nao aplica dano diretamente por evento fisico.
        // O Machado e o dono do controle "1 colisao real = 1 dano", incluindo bloqueio ate sair da arvore.
    }

    private void ReceberHitPorTrigger(Collider other, string origemEvento)
    {
        if (other == null)
            return;

    }

    private bool ColliderEhMachado(Collider colliderContato)
    {
        if (colliderContato == null)
            return false;

        if (TransformEhMachado(colliderContato.transform))
            return true;

        Rigidbody rb = colliderContato.attachedRigidbody;
        return rb != null && TransformEhMachado(rb.transform);
    }

    private bool TransformEhMachado(Transform alvo)
    {
        Transform atual = alvo;

        while (atual != null)
        {
            if (TagEhMachado(atual) || atual.GetComponent<Machado>() != null)
                return true;

            atual = atual.parent;
        }

        return false;
    }

    private bool TagEhMachado(Transform alvo)
    {
        if (alvo == null)
            return false;

        string tagAtual = alvo.tag;
        return TagIgual(tagAtual, tagMachado) || TagIgual(tagAtual, "Machado");
    }

    private static bool TagIgual(string tagAtual, string tagEsperada)
    {
        return !string.IsNullOrWhiteSpace(tagAtual) &&
               !string.IsNullOrWhiteSpace(tagEsperada) &&
               string.Equals(tagAtual, tagEsperada, StringComparison.OrdinalIgnoreCase);
    }

    public bool ReceberDanoDeMachado(GameObject origem)
    {
        return ReceberDanoDeMachado(danoPorHit, origem);
    }

    public bool ReceberDanoDeMachado(int dano, GameObject origem)
    {
        if (morreu)
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
