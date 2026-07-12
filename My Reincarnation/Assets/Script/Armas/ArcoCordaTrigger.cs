using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public class ArcoCordaTrigger : MonoBehaviour
{
    [SerializeField] private Arco arco;

    private Collider triggerCollider;

    private void Reset()
    {
        ConfigurarColliderComoTrigger();
    }

    private void Awake()
    {
        ConfigurarColliderComoTrigger();

        if (arco == null)
            arco = GetComponentInParent<Arco>();
    }

    private void OnValidate()
    {
        ConfigurarColliderComoTrigger();
    }

    public void DefinirArco(Arco novoArco)
    {
        arco = novoArco;
        ConfigurarColliderComoTrigger();
    }

    private void ConfigurarColliderComoTrigger()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider == null)
            triggerCollider = GetComponent<SphereCollider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (arco != null)
            arco.AoMaoEntrouNaAreaDaCorda(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (arco != null)
            arco.AoMaoSaiuDaAreaDaCorda(other);
    }
}
