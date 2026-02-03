using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class VidaArvore : MonoBehaviour
{
    // =========================================================
    // VIDA
    // =========================================================
    [Header("Vida")]
    [SerializeField] private int vidaMax = 5;
    [SerializeField] private int vidaAtual = 5;

    // =========================================================
    // DANO / TAGS
    // =========================================================
    [Header("Dano (Tags que causam dano)")]
    [SerializeField] private string[] tagsQueDano = new string[] { "machado", "Espada" };

    [Tooltip("Aceita a tag também no ROOT do objeto que bateu (muito comum no XR).")]
    [SerializeField] private bool aceitarTagNoRoot = true;

    [Tooltip("Aceita a tag também no PARENT do collider (às vezes o collider é filho).")]
    [SerializeField] private bool aceitarTagNoParent = true;

    [SerializeField] private int danoPorHit = 1;
    [SerializeField] private float cooldownHit = 0.25f;

    // =========================================================
    // IMPACTO (VR/XR)
    // =========================================================
    [Header("Impacto (VR/XR)")]
    [Tooltip("Se true, só aplica dano se a velocidade do objeto que bateu for >= velocidadeMinimaParaDano.")]
    [SerializeField] private bool exigirImpacto = true;

    [Tooltip("Velocidade mínima para considerar golpe. Ajuste fino (1.0 ~ 2.5 costuma funcionar).")]
    [SerializeField] private float velocidadeMinimaParaDano = 1.2f;

    [Tooltip("Em Collision usa relativeVelocity (geralmente melhor).")]
    [SerializeField] private bool usarRelativeVelocityEmCollision = true;

    // =========================================================
    // SPAWN AO DESTRUIR
    // =========================================================
    [Header("Spawn ao destruir")]
    [SerializeField] private GameObject prefabAoDestruir;
    [SerializeField] private Vector3 offsetSpawn = Vector3.zero;

    // =========================================================
    // SOM
    // =========================================================
    [Header("Som (opcional)")]
    [SerializeField] private AudioSource audioSourceHit;

    // =========================================================
    // DEBUG
    // =========================================================
    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private bool emCooldown = false;

    private void Reset()
    {
        vidaMax = 5;
        vidaAtual = 5;

        tagsQueDano = new string[] { "machado", "Espada", "machado_ferro", "machado_pedra" };
        aceitarTagNoRoot = true;
        aceitarTagNoParent = true;

        danoPorHit = 1;
        cooldownHit = 0.25f;

        exigirImpacto = true;
        velocidadeMinimaParaDano = 1.2f;
        usarRelativeVelocityEmCollision = true;

        audioSourceHit = GetComponent<AudioSource>();
        debugLogs = true;
    }

    private void Awake()
    {
        if (!audioSourceHit) audioSourceHit = GetComponent<AudioSource>();

        vidaMax = Mathf.Max(1, vidaMax);
        vidaAtual = Mathf.Clamp(vidaAtual, 1, vidaMax);

        // Aviso útil se estiver sem collider
        if (GetComponent<Collider>() == null)
        {
            Debug.LogWarning($"[VidaArvore] '{name}' está sem Collider. Sem collider não existe hit.", this);
        }
    }

    private void OnValidate()
    {
        if (vidaMax < 1) vidaMax = 1;
        if (vidaAtual < 1) vidaAtual = 1;
        if (vidaAtual > vidaMax) vidaAtual = vidaMax;

        if (danoPorHit < 1) danoPorHit = 1;
        if (cooldownHit < 0f) cooldownHit = 0f;

        if (velocidadeMinimaParaDano < 0f) velocidadeMinimaParaDano = 0f;

        if (tagsQueDano == null || tagsQueDano.Length == 0)
            tagsQueDano = new string[] { "machado" };
    }

    // =========================================================
    // COLLISION
    // =========================================================
    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null) return;

        if (debugLogs)
        {
            Debug.Log(
                $"[VidaArvore] COLLISION: Tree='{name}' BateuCom='{collision.gameObject.name}' Tag='{collision.gameObject.tag}'",
                this
            );
        }

        if (emCooldown) return;

        var t = collision.transform;

        if (!TemTagDeDano(t))
        {
            if (debugLogs) Debug.Log("[VidaArvore] Ignorado: tag não está na lista.", this);
            return;
        }

        if (!PassouDoImpactoMinimo(t, collision))
        {
            if (debugLogs) Debug.Log("[VidaArvore] Ignorado: impacto fraco (velocidade baixa).", this);
            return;
        }

        TomarDano(danoPorHit);
    }

    // =========================================================
    // TRIGGER (recomendado para XR)
    // =========================================================
    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        if (debugLogs)
        {
            Debug.Log(
                $"[VidaArvore] TRIGGER: Tree='{name}' EncostouEm='{other.name}' Tag='{other.tag}'",
                this
            );
        }

        if (emCooldown) return;

        var t = other.transform;

        if (!TemTagDeDano(t))
        {
            if (debugLogs) Debug.Log("[VidaArvore] Ignorado: tag não está na lista.", this);
            return;
        }

        if (!PassouDoImpactoMinimo(t, null))
        {
            if (debugLogs) Debug.Log("[VidaArvore] Ignorado: impacto fraco (velocidade baixa).", this);
            return;
        }

        TomarDano(danoPorHit);
    }

    // =========================================================
    // TAG CHECK
    // =========================================================
    private bool TemTagDeDano(Transform t)
    {
        if (!t) return false;

        // 1) no próprio objeto do collider que bateu
        if (TagEstaNaLista(t.gameObject.tag)) return true;

        // 2) no parent (muito comum: collider é filho)
        if (aceitarTagNoParent && t.parent != null && TagEstaNaLista(t.parent.gameObject.tag))
            return true;

        // 3) no root (muito comum no XR)
        if (aceitarTagNoRoot && t.root != null && TagEstaNaLista(t.root.gameObject.tag))
            return true;

        return false;
    }

    private bool TagEstaNaLista(string tagAtual)
    {
        if (string.IsNullOrEmpty(tagAtual) || tagsQueDano == null) return false;

        for (int i = 0; i < tagsQueDano.Length; i++)
        {
            var tg = tagsQueDano[i];
            if (string.IsNullOrEmpty(tg)) continue;
            if (tagAtual == tg) return true; // tag é case-sensitive
        }
        return false;
    }

    // =========================================================
    // IMPACTO (VELOCIDADE)
    // =========================================================
    private bool PassouDoImpactoMinimo(Transform t, Collision collision)
    {
        if (!exigirImpacto) return true;
        if (velocidadeMinimaParaDano <= 0f) return true;

        // Collision: relativeVelocity
        if (collision != null && usarRelativeVelocityEmCollision)
        {
            float v = collision.relativeVelocity.magnitude;
            if (debugLogs) Debug.Log($"[VidaArvore] Impacto(Collision.relativeVelocity) v={v:0.00}", this);
            return v >= velocidadeMinimaParaDano;
        }

        // Trigger ou fallback: Rigidbody do objeto que bateu
        var rb = t.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            float v = rb.velocity.magnitude; // Unity 2022
            if (debugLogs) Debug.Log($"[VidaArvore] Impacto(Rigidbody.velocity) v={v:0.00}", this);
            return v >= velocidadeMinimaParaDano;
        }

        if (debugLogs)
            Debug.LogWarning("[VidaArvore] Sem Rigidbody no objeto que bateu para medir impacto. Não aplicando dano.", this);

        return false;
    }

    // =========================================================
    // DANO / MORTE
    // =========================================================
    private void TomarDano(int dano)
    {
        vidaAtual -= dano;

        if (debugLogs)
            Debug.Log($"[VidaArvore] DANO! Vida agora: {vidaAtual}/{vidaMax}", this);

        if (audioSourceHit) audioSourceHit.Play();

        if (vidaAtual <= 0) Morrer();
        else StartCoroutine(Cooldown());
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
            Debug.Log($"[VidaArvore] MORREU! Spawn='{(prefabAoDestruir ? prefabAoDestruir.name : "null")}'", this);

        if (prefabAoDestruir)
            Instantiate(prefabAoDestruir, transform.position + offsetSpawn, transform.rotation);

        Destroy(gameObject);
    }
}
