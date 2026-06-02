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

    [Header("Áudio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somHit;
    [SerializeField] private float volumeSom = 1f;
    [SerializeField] private AudioClip somMorte;
    [SerializeField] private float volumeSomMorte = 1f;

    private bool emCooldown;
    private bool morreu;
    private int ultimoFrameSomHit = -1;

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

            RepassadorFisica repassador = col.GetComponent<RepassadorFisica>();

            if (repassador == null)
                repassador = col.gameObject.AddComponent<RepassadorFisica>();

            repassador.Init(this);
        }
    }

    private void ReceberHitPorCollision(Collision collision, string origemEvento)
    {
        if (collision == null)
            return;

        TocarSomHit();

        bool colliderEhMachado = ColliderEhMachado(collision.collider);
        bool transformEhMachado = TransformEhMachado(collision.transform);

        if (morreu)
            return;

        if (emCooldown)
            return;

        if (colliderEhMachado || transformEhMachado)
        {
            TomarDano(danoPorHit);
            return;
        }
    }

    private void ReceberHitPorTrigger(Collider other, string origemEvento)
    {
        if (other == null)
            return;

        TocarSomHit();
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
