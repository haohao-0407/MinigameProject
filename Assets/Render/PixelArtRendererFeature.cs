using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Reduces the final camera color to a fixed resolution and restores it to the
/// camera resolution with nearest-neighbour sampling.
/// </summary>
public sealed class PixelArtRendererFeature : ScriptableRendererFeature
{
    private const RenderPassEvent PixelationEvent = RenderPassEvent.AfterRenderingTransparents;

    [Header("Pixel Resolution")]
    [SerializeField, Min(1)] private int m_TargetWidth = 320;
    [SerializeField, Min(1)] private int m_TargetHeight = 180;

    [Header("Cameras")]
    [SerializeField] private bool m_ApplyToSceneView;

    private PixelArtRenderPass m_RenderPass;

    public override void Create()
    {
        m_RenderPass = new PixelArtRenderPass(PixelationEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!ShouldRender(in renderingData))
        {
            return;
        }

        renderer.EnqueuePass(m_RenderPass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (!ShouldRender(in renderingData))
        {
            return;
        }

        m_RenderPass.Setup(
            renderer.cameraColorTargetHandle,
            Mathf.Max(1, m_TargetWidth),
            Mathf.Max(1, m_TargetHeight));
    }

    protected override void Dispose(bool disposing)
    {
        m_RenderPass?.Dispose();
        m_RenderPass = null;
    }

    private bool ShouldRender(in RenderingData renderingData)
    {
        CameraData cameraData = renderingData.cameraData;

        if (!cameraData.resolveFinalTarget)
        {
            return false;
        }

        if (cameraData.cameraType == CameraType.Game)
        {
            return true;
        }

        return m_ApplyToSceneView && cameraData.cameraType == CameraType.SceneView;
    }

    private sealed class PixelArtRenderPass : ScriptableRenderPass
    {
        private const string LowResolutionTextureName = "_PixelArtLowResolutionTexture";

        private readonly ProfilingSampler m_ProfilingSampler =
            new ProfilingSampler("Pixel Art Downsample/Upsample");

        private RTHandle m_CameraColor;
        private RTHandle m_LowResolutionTexture;
        private int m_TargetWidth;
        private int m_TargetHeight;

        public PixelArtRenderPass(RenderPassEvent passEvent)
        {
            renderPassEvent = passEvent;
        }

        public void Setup(RTHandle cameraColor, int targetWidth, int targetHeight)
        {
            m_CameraColor = cameraColor;
            m_TargetWidth = targetWidth;
            m_TargetHeight = targetHeight;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.width = m_TargetWidth;
            descriptor.height = m_TargetHeight;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;

            RenderingUtils.ReAllocateIfNeeded(
                ref m_LowResolutionTexture,
                descriptor,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: LowResolutionTextureName);

            ConfigureTarget(m_LowResolutionTexture);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_CameraColor == null || m_LowResolutionTexture == null)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                // Explicitly select the point-sampled blit shader pass for both directions.
                Blitter.BlitCameraTexture(cmd, m_CameraColor, m_LowResolutionTexture, bilinear: false);
                Blitter.BlitCameraTexture(cmd, m_LowResolutionTexture, m_CameraColor, bilinear: false);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            m_LowResolutionTexture?.Release();
            m_LowResolutionTexture = null;
        }
    }
}
