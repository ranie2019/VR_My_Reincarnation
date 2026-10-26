using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class ItemInventarioEscalavel : MonoBehaviour
{
    private const float TamanhoMinimoBounds = 0.0001f;

    [HideInInspector] public bool escalaOriginalSalva = false;
    [HideInInspector] public Vector3 escalaOriginalMundo;

    public bool PossuiEscalaOriginalMundoValida()
    {
        return escalaOriginalSalva && EscalaValida(escalaOriginalMundo);
    }

    public Vector3 ObterEscalaOriginalMundo()
    {
        return escalaOriginalMundo;
    }

    public void DefinirEscalaOriginalMundo(Vector3 escalaOriginal)
    {
        if (!EscalaValida(escalaOriginal))
            return;

        var escalaComp = GetComponent<EscalaOriginalItem>();
        if (escalaComp != null && escalaComp.inicializado && EscalaValida(escalaComp.escalaOriginal))
        {
            escalaOriginalMundo = escalaComp.escalaOriginal;
            escalaOriginalSalva = true;
            return;
        }

        if (PossuiEscalaOriginalMundoValida())
        {
            if (escalaComp == null)
                escalaComp = gameObject.AddComponent<EscalaOriginalItem>();

            if (!escalaComp.inicializado || !EscalaValida(escalaComp.escalaOriginal))
            {
                escalaComp.DefinirEscalaOriginal(escalaOriginalMundo);
            }

            return;
        }

        escalaOriginalMundo = escalaOriginal;
        escalaOriginalSalva = true;

        if (escalaComp == null)
            escalaComp = gameObject.AddComponent<EscalaOriginalItem>();

        if (escalaComp != null)
        {
            escalaComp.DefinirEscalaOriginal(escalaOriginal);
        }
    }

    public void ForcarEscalaOriginalMundo(Vector3 escalaOriginal)
    {
        if (!EscalaValida(escalaOriginal))
            return;

        escalaOriginalMundo = escalaOriginal;
        escalaOriginalSalva = true;

        var escalaComp = GetComponent<EscalaOriginalItem>();
        if (escalaComp == null)
            escalaComp = gameObject.AddComponent<EscalaOriginalItem>();

        escalaComp.DefinirEscalaOriginal(escalaOriginal);
    }

    public bool AjustarRenderersParaSlotVisual(Transform slotTransform, Bounds boundsPermitido)
    {
        GarantirEscalaOriginal();
        if (!PossuiEscalaOriginalMundoValida())
            return false;

        return AjustarRenderersParaSlotVisual(slotTransform, boundsPermitido, escalaOriginalMundo);
    }

    public bool AjustarRenderersParaSlotVisual(Transform slotTransform, Bounds boundsPermitido, Vector3 escalaBaseMundo)
    {
        return AjustarRenderersParaSlotVisual(slotTransform, boundsPermitido, escalaBaseMundo, 1f, out _);
    }

    public bool AjustarRenderersParaSlotVisual(
        Transform slotTransform,
        Bounds boundsPermitido,
        Vector3 escalaBaseMundo,
        float fatorMaximo,
        out float fatorAprovado)
    {
        fatorAprovado = 1f;
        if (slotTransform == null || !TamanhoValido(boundsPermitido.size))
            return false;

        if (EscalaValida(escalaBaseMundo))
            ForcarEscalaOriginalMundo(escalaBaseMundo);

        GarantirEscalaOriginal();
        if (!PossuiEscalaOriginalMundoValida())
            return false;

        Vector3 escalaLocalBase = ConverterEscalaMundoParaLocal(escalaOriginalMundo);

        if (!EscalaValida(escalaLocalBase))
            return false;

        float componenteMenor = Mathf.Min(
            Mathf.Abs(escalaLocalBase.x),
            Mathf.Min(Mathf.Abs(escalaLocalBase.y), Mathf.Abs(escalaLocalBase.z)));
        float fatorMinimo = Mathf.Clamp(0.00011f / componenteMenor, 0.000001f, 1f);
        fatorMaximo = Mathf.Clamp(fatorMaximo, fatorMinimo, 1f);

        if (!TentarAplicarFatorECentralizar(slotTransform, boundsPermitido, escalaLocalBase, fatorMaximo, out bool coube))
            return false;

        if (coube)
        {
            fatorAprovado = fatorMaximo;
            return true;
        }

        // Procura primeiro uma escala que caiba, reduzindo uniformemente.
        // Depois faz busca binaria para recuperar a maior escala aprovada.
        float fatorMaiorQueCabe = 0f;
        float fatorMenorQueNaoCabe = fatorMaximo;
        float fatorCandidato = fatorMaximo * 0.5f;
        bool encontrouEscalaValida = false;

        for (int tentativa = 0; tentativa < 32; tentativa++)
        {
            fatorCandidato = Mathf.Max(fatorCandidato, fatorMinimo);
            if (!TentarAplicarFatorECentralizar(slotTransform, boundsPermitido, escalaLocalBase, fatorCandidato, out coube))
                return false;

            if (coube)
            {
                fatorMaiorQueCabe = fatorCandidato;
                encontrouEscalaValida = true;
                break;
            }

            fatorMenorQueNaoCabe = fatorCandidato;
            if (fatorCandidato <= fatorMinimo)
                break;

            fatorCandidato *= 0.5f;
        }

        if (!encontrouEscalaValida)
            return false;

        for (int tentativa = 0; tentativa < 16; tentativa++)
        {
            fatorCandidato = (fatorMaiorQueCabe + fatorMenorQueNaoCabe) * 0.5f;
            if (!TentarAplicarFatorECentralizar(slotTransform, boundsPermitido, escalaLocalBase, fatorCandidato, out coube))
                return false;

            if (coube)
                fatorMaiorQueCabe = fatorCandidato;
            else
                fatorMenorQueNaoCabe = fatorCandidato;
        }

        if (!TentarAplicarFatorECentralizar(
                slotTransform,
                boundsPermitido,
                escalaLocalBase,
                fatorMaiorQueCabe,
                out coube) || !coube)
            return false;

        fatorAprovado = fatorMaiorQueCabe;
        return true;
    }

    private bool TentarAplicarFatorECentralizar(
        Transform slotTransform,
        Bounds boundsPermitido,
        Vector3 escalaLocalBase,
        float fator,
        out bool coube)
    {
        coube = false;
        Vector3 escalaCandidata = escalaLocalBase * fator;
        if (!EscalaValida(escalaCandidata))
            return false;

        AplicarEscalaLocal(escalaCandidata);
        if (!TentarObterRendererBoundsNoEspacoDoSlot(slotTransform, boundsPermitido.center, out Bounds boundsRenderers))
            return false;

        CentralizarNoSlot(slotTransform, boundsRenderers, boundsPermitido);
        Physics.SyncTransforms();

        if (!TentarObterRendererBoundsNoEspacoDoSlot(slotTransform, boundsPermitido.center, out boundsRenderers))
            return false;

        coube = BoundsDentroDoPermitido(boundsRenderers, boundsPermitido);
        return true;
    }

    public bool EscalaVisualAtualCabeNoSlot(Transform slotTransform, Bounds boundsPermitido)
    {
        return slotTransform != null &&
               TamanhoValido(boundsPermitido.size) &&
               TentarObterRendererBoundsNoEspacoDoSlot(slotTransform, boundsPermitido.center, out Bounds boundsRenderers) &&
               BoundsDentroDoPermitido(boundsRenderers, boundsPermitido);
    }

    private void GarantirEscalaOriginal()
    {
        var escalaComp = GetComponent<EscalaOriginalItem>();
        if (escalaComp != null && escalaComp.inicializado)
        {
            if (EscalaValida(escalaComp.escalaOriginal))
            {
                escalaOriginalMundo = escalaComp.escalaOriginal;
                escalaOriginalSalva = true;
                return;
            }
        }

        if (PossuiEscalaOriginalMundoValida())
            return;

        if (!gameObject.activeInHierarchy || !EscalaValida(transform.lossyScale))
            return;

        escalaOriginalMundo = transform.lossyScale;
        escalaOriginalSalva = true;

        if (escalaComp != null && !escalaComp.inicializado)
        {
            escalaComp.DefinirEscalaOriginal(escalaOriginalMundo);
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
        if (!EscalaValida(escalaLocal))
            return;

        transform.localScale = escalaLocal;

        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.SetTargetLocalScale(escalaLocal);

        Physics.SyncTransforms();
    }

    private bool TentarObterRendererBoundsNoEspacoDoSlot(Transform slotTransform, Vector3 centroInicial, out Bounds boundsLocal)
    {
        boundsLocal = new Bounds(centroInicial, Vector3.zero);
        bool achou = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy ||
                (r.transform != transform && !r.transform.IsChildOf(transform))) continue;
            if (!BoundsValido(r.bounds)) continue;

            EncapsularBoundsNoSlot(slotTransform, r.bounds, ref boundsLocal, ref achou);
        }

        return achou;
    }

    private static void EncapsularBoundsNoSlot(Transform slotTransform, Bounds boundsMundo, ref Bounds boundsLocal, ref bool achou)
    {
        if (slotTransform == null || !BoundsValido(boundsMundo))
            return;

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
        if (slotTransform == null || !VetorFinito(pontoMundo))
            return;

        Vector3 pontoLocal = slotTransform.InverseTransformPoint(pontoMundo);
        if (!VetorFinito(pontoLocal))
            return;

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
        if (slotTransform == null || !BoundsValido(boundsItem) || !BoundsValido(boundsPermitido))
            return;

        Vector3 deslocamentoLocal = boundsPermitido.center - boundsItem.center;
        Vector3 deslocamentoMundo = slotTransform.TransformVector(deslocamentoLocal);
        if (!VetorFinito(deslocamentoMundo))
            return;

        Vector3 novaPosicao = transform.position + deslocamentoMundo;
        if (!VetorFinito(novaPosicao))
            return;

        transform.position = novaPosicao;
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

    private static bool TamanhoValido(Vector3 tamanho)
    {
        return VetorFinito(tamanho) &&
               tamanho.x > TamanhoMinimoBounds &&
               tamanho.y > TamanhoMinimoBounds &&
               tamanho.z > TamanhoMinimoBounds;
    }

    private static bool EscalaValida(Vector3 escala)
    {
        const float minimo = 0.0001f;
        return VetorFinito(escala) &&
               Mathf.Abs(escala.x) > minimo &&
               Mathf.Abs(escala.y) > minimo &&
               Mathf.Abs(escala.z) > minimo;
    }

    private static float DividirSeguro(float valor, float divisor)
    {
        if (!ValorFinito(valor))
            return 1f;

        if (!ValorFinito(divisor) || Mathf.Approximately(divisor, 0f))
            return valor;

        float resultado = valor / divisor;
        return ValorFinito(resultado) ? resultado : valor;
    }

    private static bool BoundsValido(Bounds bounds)
    {
        return VetorFinito(bounds.center) && TamanhoValido(bounds.size);
    }

    private static bool VetorFinito(Vector3 valor)
    {
        return ValorFinito(valor.x) && ValorFinito(valor.y) && ValorFinito(valor.z);
    }

    private static bool ValorFinito(float valor)
    {
        return !float.IsNaN(valor) && !float.IsInfinity(valor);
    }
}
