using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// DepthBlitFeature (Atualizado p/ URP novo / Unity 6)
// - Não usa CopyDepthPass interno (que mudou assinatura)
// - Gera uma textura "depth em cor" (R32_SFloat) usando um fullscreen blit
// - Requer: um shader/material que leia _CameraDepthTexture (ex: depth copy shader)
// - Continua usando seu DepthBlitEdgePass existente do sample
public class DepthBlitFeature : ScriptableRendererFeature
{
    [Header("Pass Events")]
    public RenderPassEvent evt_Depth = RenderPassEvent.AfterRenderingOpaques;
    public RenderPassEvent evt_Edge = RenderPassEvent.AfterRenderingOpaques;

    [Header("Materials / Shaders")]
    [Tooltip("Shader que converte _CameraDepthTexture para um RT em formato de cor (R32/R16).")]
    public Shader copyDepthShader;

    [Tooltip("Material do efeito de borda/visualização (seu DepthBlitEdgePass usa isso).")]
    public Material depthEdgeMaterial;

    // RTHandle para armazenar depth como cor
    private RTHandle m_DepthRTHandle;
    private const string k_DepthRTName = "_MyDepthTexture";

    private Material m_CopyDepthMaterial;

    private DepthToColorPass m_DepthToColorPass;
    private DepthBlitEdgePass m_DepthEdgePass; // vem do sample (mantém o seu)

    // ---------------- PASS: Depth -> Color ----------------
    private class DepthToColorPass : ScriptableRenderPass
    {
        private Material m_Material;
        private RTHandle m_SourceColor;
        private RTHandle m_Destination;

        public DepthToColorPass(RenderPassEvent evt, Material mat)
        {
            renderPassEvent = evt;
            m_Material = mat;

            // Isso força URP a garantir que existe depth texture disponível como _CameraDepthTexture
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void Setup(RTHandle sourceColor, RTHandle destination)
        {
            m_SourceColor = sourceColor;
            m_Destination = destination;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // destino é um RTHandle já alocado fora
            ConfigureTarget(m_Destination);
            ConfigureClear(ClearFlag.None, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_Material == null || m_Destination == null)
                return;

            var cmd = CommandBufferPool.Get("DepthToColorPass");

            // Blit fullscreen: usa sourceColor apenas como "input dummy" para Blitter,
            // mas o shader deve ler _CameraDepthTexture e escrever no destino.
            Blitter.BlitCameraTexture(cmd, m_SourceColor, m_Destination, m_Material, 0);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    public override void Create()
    {
        // Material do depth copy
        if (m_CopyDepthMaterial == null && copyDepthShader != null)
            m_CopyDepthMaterial = CoreUtils.CreateEngineMaterial(copyDepthShader);

        // Pass depth->color
        if (m_DepthToColorPass == null)
            m_DepthToColorPass = new DepthToColorPass(evt_Depth, m_CopyDepthMaterial);

        // Pass de borda (do seu sample)
        // Mantém a assinatura original que você já tinha:
        m_DepthEdgePass = new DepthBlitEdgePass(depthEdgeMaterial, evt_Edge);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;

        // Aloca RTHandle para depth em cor
        var desc = renderingData.cameraData.cameraTargetDescriptor;

        // Vamos guardar depth como cor (evita treta de copiar depth nativo)
        desc.depthBufferBits = 0;
        desc.msaaSamples = 1;

        // R32_SFloat é ótimo pra depth; se sua plataforma não suportar, tente R16_UNorm
        desc.graphicsFormat = GraphicsFormat.R32_SFloat;

        RenderingUtils.ReAllocateIfNeeded(
            ref m_DepthRTHandle,
            desc,
            FilterMode.Bilinear,
            TextureWrapMode.Clamp,
            name: k_DepthRTName
        );

        // Setup do pass depth->color
        m_DepthToColorPass.Setup(renderer.cameraColorTargetHandle, m_DepthRTHandle);

        // Passa RT pro edge pass (do sample)
        m_DepthEdgePass.SetRTHandle(ref m_DepthRTHandle, renderer.cameraColorTargetHandle);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;

        if (copyDepthShader == null)
            return;

        if (m_CopyDepthMaterial == null)
            m_CopyDepthMaterial = CoreUtils.CreateEngineMaterial(copyDepthShader);

        // garante que o pass está com o material certo
        if (m_DepthToColorPass == null)
            m_DepthToColorPass = new DepthToColorPass(evt_Depth, m_CopyDepthMaterial);

        renderer.EnqueuePass(m_DepthToColorPass);

        if (m_DepthEdgePass != null)
            renderer.EnqueuePass(m_DepthEdgePass);
    }

    protected override void Dispose(bool disposing)
    {
        m_DepthRTHandle?.Release();
        m_DepthRTHandle = null;

        CoreUtils.Destroy(m_CopyDepthMaterial);
        m_CopyDepthMaterial = null;

        m_DepthToColorPass = null;
        m_DepthEdgePass = null;
    }
}
