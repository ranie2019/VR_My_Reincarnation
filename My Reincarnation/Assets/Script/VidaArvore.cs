using UnityEngine;
using System.Collections;

public class VidaArvore : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private int vidaMax = 5;
    [SerializeField] private int vidaAtual = 5;

    [Header("Dano")]
    [SerializeField] private string tagMachado = "machado";
    [SerializeField] private int danoPorHit = 1;
    [SerializeField] private float cooldownHit = 0.25f;

    [Header("Spawn ao destruir")]
    [SerializeField] private GameObject prefabAoDestruir;
    [SerializeField] private Vector3 offsetSpawn = Vector3.zero;

    [Header("Som (opcional)")]
    [SerializeField] private AudioSource audioSourceHit;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private bool emCooldown;

    private void Reset()
    {
        vidaMax = 5;
        vidaAtual = 5;
        tagMachado = "machado";
        danoPorHit = 1;
        cooldownHit = 0.25f;
        offsetSpawn = Vector3.zero;
        audioSourceHit = GetComponent<AudioSource>();
        debugLogs = true;
    }

    private void Awake()
    {
        if (audioSourceHit == null)
            audioSourceHit = GetComponent<AudioSource>();

        vidaAtual = Mathf.Clamp(vidaAtual, 1, vidaMax);
    }

    // ========= COLISÃO NORMAL =========
    private void OnCollisionEnter(Collision collision)
    {
        if (debugLogs)
        {
            Debug.Log($"[VidaArvore] OnCollisionEnter com: {collision.gameObject.name} | tag: {collision.gameObject.tag}", this);
        }

        if (emCooldown) return;

        // Pode bater com collider filho do machado, então checa a hierarquia
        if (TemTagNaHierarquia(collision.transform, tagMachado))
        {
            AplicarDano(danoPorHit, collision.gameObject);
        }
    }

    // ========= TRIGGER (mais confiável em alguns setups XR) =========
    private void OnTriggerEnter(Collider other)
    {
        if (debugLogs)
        {
            Debug.Log($"[VidaArvore] OnTriggerEnter com: {other.gameObject.name} | tag: {other.gameObject.tag}", this);
        }

        if (emCooldown) return;

        if (TemTagNaHierarquia(other.transform, tagMachado))
        {
            AplicarDano(danoPorHit, other.gameObject);
        }
    }

    private void AplicarDano(int dano, GameObject quemBateu)
    {
        vidaAtual -= dano;

        if (audioSourceHit != null)
            audioSourceHit.Play();

        if (debugLogs)
        {
            Debug.Log($"[VidaArvore] DANO por '{quemBateu.name}'. Vida agora: {vidaAtual}/{vidaMax}", this);
        }

        if (vidaAtual <= 0)
        {
            Morrer();
        }
        else
        {
            StartCoroutine(Cooldown());
        }
    }

    private IEnumerator Cooldown()
    {
        emCooldown = true;
        yield return new WaitForSeconds(cooldownHit);
        emCooldown = false;
    }

    private void Morrer()
    {
        if (debugLogs)
            Debug.Log("[VidaArvore] MORREU! Spawnando e destruindo...", this);

        if (prefabAoDestruir != null)
        {
            Instantiate(prefabAoDestruir, transform.position + offsetSpawn, transform.rotation);
        }

        Destroy(gameObject);
    }

    // Procura a tag no objeto que colidiu OU em qualquer pai dele
    private bool TemTagNaHierarquia(Transform t, string tagProcurada)
    {
        while (t != null)
        {
            if (t.CompareTag(tagProcurada))
                return true;
            t = t.parent;
        }
        return false;
    }
}
