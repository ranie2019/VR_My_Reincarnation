using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
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

    private bool emCooldown = false;

    // ===== Repassador para colisão/trigger em colliders dos FILHOS =====
    private class RepassadorFisica : MonoBehaviour
    {
        private VidaArvore pai;
        public void Init(VidaArvore p) => pai = p;

        private void OnCollisionEnter(Collision c)
        {
            if (pai != null) pai.ReceberHitPorCollision(c, gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (pai != null) pai.ReceberHitPorTrigger(other, gameObject);
        }
    }

    private void Reset()
    {
        vidaMax = 5;
        vidaAtual = 5;
        tagMachado = "machado";
        danoPorHit = 1;
        cooldownHit = 0.25f;
        audioSourceHit = GetComponent<AudioSource>();
        debugLogs = true;
    }

    private void Awake()
    {
        if (!audioSourceHit) audioSourceHit = GetComponent<AudioSource>();
        vidaAtual = Mathf.Clamp(vidaAtual, 1, Mathf.Max(1, vidaMax));

        InstalarRepassadoresNosFilhos();
    }

    private void OnValidate()
    {
        if (vidaMax < 1) vidaMax = 1;
        if (vidaAtual < 1) vidaAtual = 1;
        if (vidaAtual > vidaMax) vidaAtual = vidaMax;
        if (cooldownHit < 0f) cooldownHit = 0f;
        if (danoPorHit < 1) danoPorHit = 1;
    }

    private void InstalarRepassadoresNosFilhos()
    {
        var cols = GetComponentsInChildren<Collider>(true);
        if (cols == null || cols.Length == 0)
        {
            if (debugLogs) Debug.LogWarning($"[VidaArvore] Sem Collider em '{name}'. Sem collider não tem hit.", this);
            return;
        }

        foreach (var col in cols)
        {
            if (!col) continue;
            var go = col.gameObject;

            var rep = go.GetComponent<RepassadorFisica>();
            if (rep == null) rep = go.AddComponent<RepassadorFisica>();
            rep.Init(this);
        }

        if (debugLogs)
            Debug.Log($"[VidaArvore] Repassadores instalados em {cols.Length} collider(s) (pai/filhos).", this);
    }

    // ===================== HIT via COLLISION =====================
    private void ReceberHitPorCollision(Collision collision, GameObject donoCollider)
    {
        if (collision == null) return;

        if (debugLogs)
        {
            Debug.Log($"[VidaArvore] COLLISION: Tree='{name}' Filho='{donoCollider.name}' BateuCom='{collision.gameObject.name}' Tag='{collision.gameObject.tag}'", this);
        }

        if (emCooldown) return;

        // pega a TAG do objeto que bateu (ou do pai dele)
        if (TemTagMachado(collision.transform))
            TomarDano(danoPorHit);
    }

    // ===================== HIT via TRIGGER (RECOMENDADO) =====================
    private void ReceberHitPorTrigger(Collider other, GameObject donoCollider)
    {
        if (other == null) return;

        if (debugLogs)
        {
            Debug.Log($"[VidaArvore] TRIGGER: Tree='{name}' Filho='{donoCollider.name}' EncostouEm='{other.name}' Tag='{other.tag}'", this);
        }

        if (emCooldown) return;

        // aqui funciona MUITO melhor com machado segurado no XR
        if (TemTagMachado(other.transform))
            TomarDano(danoPorHit);
    }

    // Aceita tag no próprio collider OU no pai/root do machado
    private bool TemTagMachado(Transform t)
    {
        if (!t) return false;
        if (t.CompareTag(tagMachado)) return true;
        if (t.root != null && t.root.CompareTag(tagMachado)) return true;
        return false;
    }

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
