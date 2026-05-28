using UnityEngine;

[DisallowMultipleComponent]
public class InventarioScrollVR : MonoBehaviour
{
    private const float DistanciaSlotOculto = 1000f;

    [Header("Slots do inventario")]
    [SerializeField] private SlotInventario[] slots;

    [Header("Rolagem")]
    [SerializeField, Min(1)] private int colunas = 4;
    [SerializeField, Min(1)] private int linhasVisiveis = 3;
    [SerializeField, Min(0)] private int linhaInicial = 0;

    private SlotPose[] posicoesOriginais;
    private SlotPose[] posicoesVisiveis;

    public int LinhaInicial => linhaInicial;
    public int TotalLinhas => CalcularTotalLinhas();
    public int MaxLinhaInicial => CalcularMaxLinhaInicial();

    private void Awake()
    {
        NormalizarConfiguracao();
        CapturarPosicoesIniciais();
        AtualizarPagina();
    }

    private void OnValidate()
    {
        NormalizarConfiguracao();
    }

    public void RolarBaixo()
    {
        NormalizarConfiguracao();

        int maxLinhaInicial = CalcularMaxLinhaInicial();
        if (linhaInicial >= maxLinhaInicial)
            return;

        linhaInicial++;
        AtualizarPagina();
    }

    public void RolarCima()
    {
        NormalizarConfiguracao();

        if (linhaInicial <= 0)
            return;

        linhaInicial--;
        AtualizarPagina();
    }

    public void ProximaPagina()
    {
        RolarBaixo();
    }

    public void PaginaAnterior()
    {
        RolarCima();
    }

    public void AtualizarPagina()
    {
        NormalizarConfiguracao();
        GarantirPosicoesCapturadas();

        if (slots == null || slots.Length == 0)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            SlotInventario slot = slots[i];
            if (slot == null)
                continue;

            slot.SetVisivelNaRolagem(false);
            MoverSlotOculto(slot.transform, i);
        }

        int primeiroSlotVisivel = linhaInicial * colunas;
        int ultimoSlotVisivel = Mathf.Min(primeiroSlotVisivel + QuantidadeSlotsVisiveis(), slots.Length);

        for (int i = primeiroSlotVisivel; i < ultimoSlotVisivel; i++)
        {
            SlotInventario slot = slots[i];
            if (slot == null)
                continue;

            int linha = i / colunas;
            int coluna = i % colunas;
            int linhaNaJanela = linha - linhaInicial;
            int indiceVisual = linhaNaJanela * colunas + coluna;

            MoverSlotParaPosicaoVisivel(slot.transform, indiceVisual);
            slot.SetVisivelNaRolagem(true);
        }
    }

    private void NormalizarConfiguracao()
    {
        colunas = Mathf.Max(1, colunas);
        linhasVisiveis = Mathf.Max(1, linhasVisiveis);
        linhaInicial = Mathf.Clamp(linhaInicial, 0, CalcularMaxLinhaInicial());
    }

    private void GarantirPosicoesCapturadas()
    {
        int quantidadeVisivel = QuantidadeSlotsVisiveis();

        if (posicoesOriginais == null || slots == null || posicoesOriginais.Length != slots.Length ||
            posicoesVisiveis == null || posicoesVisiveis.Length != quantidadeVisivel)
        {
            CapturarPosicoesIniciais();
        }
    }

    private void CapturarPosicoesIniciais()
    {
        if (slots == null)
        {
            posicoesOriginais = null;
            posicoesVisiveis = null;
            return;
        }

        posicoesOriginais = new SlotPose[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                posicoesOriginais[i] = new SlotPose(slots[i].transform);
        }

        int quantidadeVisivel = QuantidadeSlotsVisiveis();
        posicoesVisiveis = new SlotPose[quantidadeVisivel];

        for (int i = 0; i < quantidadeVisivel; i++)
        {
            SlotInventario slotReferencia = i < slots.Length ? slots[i] : null;
            if (slotReferencia != null)
            {
                posicoesVisiveis[i] = new SlotPose(slotReferencia.transform);
                continue;
            }

            posicoesVisiveis[i] = BuscarPrimeiraPoseValida();
        }
    }

    private void MoverSlotParaPosicaoVisivel(Transform slotTransform, int indiceVisual)
    {
        if (slotTransform == null || posicoesVisiveis == null || indiceVisual < 0 || indiceVisual >= posicoesVisiveis.Length)
            return;

        posicoesVisiveis[indiceVisual].AplicarEm(slotTransform);
    }

    private void MoverSlotOculto(Transform slotTransform, int indiceSlot)
    {
        if (slotTransform == null)
            return;

        SlotPose basePose = BuscarPoseOcultaBase(indiceSlot);
        Vector3 deslocamento = Vector3.right * DistanciaSlotOculto + Vector3.up * (indiceSlot * 0.01f);
        slotTransform.localPosition = basePose.LocalPosition + deslocamento;
        slotTransform.localRotation = basePose.LocalRotation;
        slotTransform.localScale = basePose.LocalScale;
    }

    private SlotPose BuscarPoseOcultaBase(int indiceSlot)
    {
        if (posicoesOriginais != null && indiceSlot >= 0 && indiceSlot < posicoesOriginais.Length && posicoesOriginais[indiceSlot].Valida)
            return posicoesOriginais[indiceSlot];

        return BuscarPrimeiraPoseValida();
    }

    private SlotPose BuscarPrimeiraPoseValida()
    {
        if (posicoesVisiveis != null)
        {
            for (int i = 0; i < posicoesVisiveis.Length; i++)
            {
                if (posicoesVisiveis[i].Valida)
                    return posicoesVisiveis[i];
            }
        }

        if (posicoesOriginais != null)
        {
            for (int i = 0; i < posicoesOriginais.Length; i++)
            {
                if (posicoesOriginais[i].Valida)
                    return posicoesOriginais[i];
            }
        }

        return SlotPose.Identity;
    }

    private int QuantidadeSlotsVisiveis()
    {
        return Mathf.Max(1, colunas * linhasVisiveis);
    }

    private int CalcularTotalLinhas()
    {
        if (slots == null || slots.Length == 0)
            return 0;

        return Mathf.CeilToInt(slots.Length / (float)Mathf.Max(1, colunas));
    }

    private int CalcularMaxLinhaInicial()
    {
        return Mathf.Max(0, CalcularTotalLinhas() - Mathf.Max(1, linhasVisiveis));
    }

    private struct SlotPose
    {
        public static SlotPose Identity => new SlotPose(Vector3.zero, Quaternion.identity, Vector3.one, false);

        public readonly bool Valida;
        public readonly Vector3 LocalPosition;
        public readonly Quaternion LocalRotation;
        public readonly Vector3 LocalScale;

        public SlotPose(Transform transform)
        {
            Valida = transform != null;
            LocalPosition = transform != null ? transform.localPosition : Vector3.zero;
            LocalRotation = transform != null ? transform.localRotation : Quaternion.identity;
            LocalScale = transform != null ? transform.localScale : Vector3.one;
        }

        private SlotPose(Vector3 localPosition, Quaternion localRotation, Vector3 localScale, bool valida)
        {
            Valida = valida;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }

        public void AplicarEm(Transform transform)
        {
            if (transform == null || !Valida)
                return;

            transform.localPosition = LocalPosition;
            transform.localRotation = LocalRotation;
            transform.localScale = LocalScale;
        }
    }
}