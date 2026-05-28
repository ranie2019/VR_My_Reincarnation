using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class ItemInventarioEscalavel : MonoBehaviour
{
    [HideInInspector] public bool escalaOriginalSalva = false;
    [HideInInspector] public Vector3 escalaOriginalMundo;

    public bool AjustarParaSlot(BoxCollider slotCollider, float margemDeSeguranca, Transform pontoReferencia)
    {
        if (slotCollider == null) return false;

        GarantirEscalaOriginal();
        if (!escalaOriginalSalva) return false;

        float margem = Mathf.Clamp(margemDeSeguranca, 0.01f, 1f);
        Transform slotTransform = slotCollider.transform;
        Bounds boundsPermitido = new Bounds(slotCollider.center, slotCollider.size * margem);
        if (!TamanhoValido(boundsPermitido.size)) return false;

        Vector3 escalaLocalOriginal = ConverterEscalaMundoParaLocal(escalaOriginalMundo);
        AplicarEscalaLocal(escalaLocalOriginal);

        if (!TentarObterBoundsNoEspacoDoSlot(slotTransform, boundsPermitido.center, out Bounds boundsItem, out int qtdRenderers, out int qtdColliders))
        {
            Debug.Log($"[InventarioEscala] item={name} | slot={NomeDoSlot(slotCollider, pontoReferencia)} | margem={margem:F3} | renderers={qtdRenderers} | colliders={qtdColliders} | tamanhoInicial={Vector3.zero} | tamanhoPermitido={boundsPermitido.size} | fator=1.0000 | escalaFinal={transform.localScale} | coube=False");
            return false;
        }

        Vector3 tamanhoInicial = boundsItem.size;
        float fatorAplicado = Mathf.Min(1f, MenorFator(boundsPermitido.size, tamanhoInicial));
        AplicarEscalaLocal(escalaLocalOriginal * fatorAplicado);

        bool coube = false;
        Vector3 tamanhoFinal = tamanhoInicial;

        for (int i = 0; i < 12; i++)
        {
            if (!TentarObterBoundsNoEspacoDoSlot(slotTransform, boundsPermitido.center, out boundsItem, out qtdRenderers, out qtdColliders))
                break;

            CentralizarNoSlot(slotTransform, boundsItem, boundsPermitido);
            Physics.SyncTransforms();

            if (!TentarObterBoundsNoEspacoDoSlot(slotTransform, boundsPermitido.center, out boundsItem, out qtdRenderers, out qtdColliders))
                break;

            tamanhoFinal = boundsItem.size;
            coube = BoundsDentroDoPermitido(boundsItem, boundsPermitido);
            if (coube)
                break;

            float ajuste = Mathf.Min(1f, MenorFator(boundsPermitido.size, tamanhoFinal));
            if (ajuste >= 1f)
                break;

            fatorAplicado *= ajuste;
            AplicarEscalaLocal(escalaLocalOriginal * fatorAplicado);
        }

        if (!coube && TentarObterBoundsNoEspacoDoSlot(slotTransform, boundsPermitido.center, out boundsItem, out qtdRenderers, out qtdColliders))
        {
            CentralizarNoSlot(slotTransform, boundsItem, boundsPermitido);
            Physics.SyncTransforms();

            if (TentarObterBoundsNoEspacoDoSlot(slotTransform, boundsPermitido.center, out boundsItem, out qtdRenderers, out qtdColliders))
            {
                tamanhoFinal = boundsItem.size;
                coube = BoundsDentroDoPermitido(boundsItem, boundsPermitido);
            }
        }

        Debug.Log($"[InventarioEscala] item={name} | slot={NomeDoSlot(slotCollider, pontoReferencia)} | margem={margem:F3} | renderers={qtdRenderers} | colliders={qtdColliders} | tamanhoInicial={tamanhoInicial} | tamanhoPermitido={boundsPermitido.size} | fator={fatorAplicado:F4} | escalaFinal={transform.localScale} | coube={coube}");

        return coube;
    }

    private void GarantirEscalaOriginal()
    {
        var escalaComp = GetComponent<EscalaOriginalItem>();
        if (escalaComp != null && escalaComp.inicializado)
        {
            if (!escalaOriginalSalva)
            {
                escalaOriginalMundo = escalaComp.escalaOriginal;
                escalaOriginalSalva = true;
            }

            return;
        }

        if (escalaOriginalSalva)
            return;

        escalaOriginalMundo = transform.lossyScale;
        escalaOriginalSalva = true;

        if (escalaComp != null && !escalaComp.inicializado)
        {
            escalaComp.escalaOriginal = escalaOriginalMundo;
            escalaComp.inicializado = true;
        }
    }

    private Vector3 ConverterEscalaMundoParaLocal(Vector3 escalaMundo)
    {
        Vector3 escalaParent = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        return new Vector3(
            DividirSeguro(escalaMundo.x, escalaParent.x),
            DividirSeguro(escalaMundo.y, escalaParent.y),
            DividirSeguro(escalaMundo.z, escalaParent.z)
        );
    }

    private void AplicarEscalaLocal(Vector3 escalaLocal)
    {
        transform.localScale = escalaLocal;

        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.SetTargetLocalScale(escalaLocal);

        Physics.SyncTransforms();
    }

    private bool TentarObterBoundsNoEspacoDoSlot(Transform slotTransform, Vector3 centroInicial, out Bounds boundsLocal, out int quantidadeRenderers, out int quantidadeColliders)
    {
        boundsLocal = new Bounds(centroInicial, Vector3.zero);
        quantidadeRenderers = 0;
        quantidadeColliders = 0;
        bool achou = false;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (col == null || !col.enabled || !col.transform.IsChildOf(transform)) continue;

            quantidadeColliders++;
            EncapsularBoundsNoSlot(slotTransform, col.bounds, ref boundsLocal, ref achou);
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.transform.IsChildOf(transform)) continue;

            quantidadeRenderers++;
            EncapsularBoundsNoSlot(slotTransform, r.bounds, ref boundsLocal, ref achou);
        }

        return achou;
    }

    private static void EncapsularBoundsNoSlot(Transform slotTransform, Bounds boundsMundo, ref Bounds boundsLocal, ref bool achou)
    {
        Vector3 min = boundsMundo.min;
        Vector3 max = boundsMundo.max;

        EncapsularPontoNoSlot(slotTransform, new Vector3(min.x, min.y, min.z), ref boundsLocal, ref achou);
        EncapsularPontoNoSlot(slotTransform, new Vector3(min.x, min.y, max.z), ref boundsLocal, ref achou);
        EncapsularPontoNoSlot(slotTransform, new Vector3(min.x, max.y, min.z), ref boundsLocal, ref achou);
        EncapsularPontoNoSlot(slotTransform, new Vector3(min.x, max.y, max.z), ref boundsLocal, ref achou);
        EncapsularPontoNoSlot(slotTransform, new Vector3(max.x, min.y, min.z), ref boundsLocal, ref achou);
        EncapsularPontoNoSlot(slotTransform, new Vector3(max.x, min.y, max.z), ref boundsLocal, ref achou);
        EncapsularPontoNoSlot(slotTransform, new Vector3(max.x, max.y, min.z), ref boundsLocal, ref achou);
        EncapsularPontoNoSlot(slotTransform, new Vector3(max.x, max.y, max.z), ref boundsLocal, ref achou);
    }

    private static void EncapsularPontoNoSlot(Transform slotTransform, Vector3 pontoMundo, ref Bounds boundsLocal, ref bool achou)
    {
        Vector3 pontoLocal = slotTransform.InverseTransformPoint(pontoMundo);
        if (!achou)
        {
            boundsLocal = new Bounds(pontoLocal, Vector3.zero);
            achou = true;
        }
        else
        {
            boundsLocal.Encapsulate(pontoLocal);
        }
    }

    private void CentralizarNoSlot(Transform slotTransform, Bounds boundsItem, Bounds boundsPermitido)
    {
        Vector3 deslocamentoLocal = boundsPermitido.center - boundsItem.center;
        transform.position += slotTransform.TransformVector(deslocamentoLocal);
    }

    private static bool BoundsDentroDoPermitido(Bounds boundsItem, Bounds boundsPermitido)
    {
        const float tolerancia = 0.0001f;
        return boundsItem.min.x >= boundsPermitido.min.x - tolerancia &&
               boundsItem.max.x <= boundsPermitido.max.x + tolerancia &&
               boundsItem.min.y >= boundsPermitido.min.y - tolerancia &&
               boundsItem.max.y <= boundsPermitido.max.y + tolerancia &&
               boundsItem.min.z >= boundsPermitido.min.z - tolerancia &&
               boundsItem.max.z <= boundsPermitido.max.z + tolerancia;
    }

    private static float MenorFator(Vector3 tamanhoPermitido, Vector3 tamanhoItem)
    {
        if (!TamanhoValido(tamanhoPermitido) || !TamanhoValido(tamanhoItem)) return 1f;
        float fatorX = tamanhoPermitido.x / tamanhoItem.x;
        float fatorY = tamanhoPermitido.y / tamanhoItem.y;
        float fatorZ = tamanhoPermitido.z / tamanhoItem.z;
        return Mathf.Min(fatorX, fatorY, fatorZ);
    }

    private static bool TamanhoValido(Vector3 tamanho)
    {
        return tamanho.x > 0f && tamanho.y > 0f && tamanho.z > 0f;
    }

    private static float DividirSeguro(float valor, float divisor)
    {
        return Mathf.Approximately(divisor, 0f) ? valor : valor / divisor;
    }

    private static string NomeDoSlot(BoxCollider slotCollider, Transform pontoReferencia)
    {
        if (pontoReferencia != null)
            return pontoReferencia.name;

        return slotCollider != null ? slotCollider.name : "sem slot";
    }
}