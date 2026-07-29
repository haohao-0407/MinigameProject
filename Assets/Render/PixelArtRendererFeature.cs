using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Reduces the camera color to a fixed resolution, maps it to a finite palette
/// in Lab color space, and restores it with nearest-neighbour sampling.
/// </summary>
public sealed class PixelArtRendererFeature : ScriptableRendererFeature
{
    private const int MaxPaletteColors = 32;
    private const RenderPassEvent PixelationEvent = RenderPassEvent.AfterRenderingTransparents;

    [Header("Pixel Resolution")]
    [SerializeField, Min(1)] private int m_TargetWidth = 320;
    [SerializeField, Min(1)] private int m_TargetHeight = 180;

    [Header("Lab Palette Mapping")]
    [SerializeField] private bool m_EnablePaletteMapping = true;
    [SerializeField] private Shader m_PaletteShader;
    [SerializeField] private Color[] m_Palette = CreateDefaultPalette();

    [Header("Cameras")]
    [SerializeField] private bool m_ApplyToSceneView;

    private PixelArtRenderPass m_RenderPass;

    public override void Create()
    {
        Shader paletteShader = m_PaletteShader != null
            ? m_PaletteShader
            : Resources.Load<Shader>("PixelArtPaletteLab");

        if (paletteShader == null)
        {
            paletteShader = Shader.Find("Hidden/PixelArt/PaletteLab");
        }

        m_RenderPass = new PixelArtRenderPass(PixelationEvent, paletteShader);
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
            Mathf.Max(1, m_TargetHeight),
            m_EnablePaletteMapping,
            m_Palette);
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

    private static Color[] CreateDefaultPalette()
    {
        // DawnBringer 16. Values are stored as sRGB and converted in the shader.
        return new Color[]
        {
            new Color32(20, 12, 28, 255),
            new Color32(68, 36, 52, 255),
            new Color32(48, 52, 109, 255),
            new Color32(78, 74, 78, 255),
            new Color32(133, 76, 48, 255),
            new Color32(52, 101, 36, 255),
            new Color32(208, 70, 72, 255),
            new Color32(117, 113, 97, 255),
            new Color32(89, 125, 206, 255),
            new Color32(210, 125, 44, 255),
            new Color32(133, 149, 161, 255),
            new Color32(109, 170, 44, 255),
            new Color32(210, 170, 153, 255),
            new Color32(109, 194, 202, 255),
            new Color32(218, 212, 94, 255),
            new Color32(222, 238, 214, 255)
        };
    }

    private sealed class PixelArtRenderPass : ScriptableRenderPass
    {
        private const string LowResolutionTextureName = "_PixelArtLowResolutionTexture";
        private const string PaletteTextureName = "_PixelArtPaletteTexture";

        private static readonly int PaletteColorsId = Shader.PropertyToID("_PaletteColors");
        private static readonly int PaletteCountId = Shader.PropertyToID("_PaletteCount");

        private readonly ProfilingSampler m_ProfilingSampler =
            new ProfilingSampler("Pixel Art Downsample/Palette/Upsample");
        private readonly Vector4[] m_PaletteColors = new Vector4[MaxPaletteColors];
        private readonly Material m_PaletteMaterial;

        private RTHandle m_CameraColor;
        private RTHandle m_LowResolutionTexture;
        private RTHandle m_PaletteTexture;
        private int m_TargetWidth;
        private int m_TargetHeight;
        private int m_PaletteCount;
        private bool m_UsePaletteMapping;

        public PixelArtRenderPass(RenderPassEvent passEvent, Shader paletteShader)
        {
            renderPassEvent = passEvent;
            m_PaletteMaterial = paletteShader != null
                ? CoreUtils.CreateEngineMaterial(paletteShader)
                : null;
        }

        public void Setup(
            RTHandle cameraColor,
            int targetWidth,
            int targetHeight,
            bool enablePaletteMapping,
            Color[] palette)
        {
            m_CameraColor = cameraColor;
            m_TargetWidth = targetWidth;
            m_TargetHeight = targetHeight;
            m_PaletteCount = Mathf.Min(palette?.Length ?? 0, MaxPaletteColors);
            m_UsePaletteMapping = enablePaletteMapping &&
                                  m_PaletteMaterial != null &&
                                  m_PaletteCount > 0;

            for (int i = 0; i < m_PaletteCount; i++)
            {
                m_PaletteColors[i] = palette[i];
            }
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

            RenderingUtils.ReAllocateIfNeeded(
                ref m_PaletteTexture,
                descriptor,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: PaletteTextureName);

            ConfigureTarget(m_LowResolutionTexture);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_CameraColor == null ||
                m_LowResolutionTexture == null ||
                m_PaletteTexture == null)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                // First reduce the fully rendered scene geometry with point sampling.
                Blitter.BlitCameraTexture(cmd, m_CameraColor, m_LowResolutionTexture, bilinear: false);

                RTHandle upsampleSource = m_LowResolutionTexture;
                if (m_UsePaletteMapping)
                {
                    m_PaletteMaterial.SetInt(PaletteCountId, m_PaletteCount);
                    m_PaletteMaterial.SetVectorArray(PaletteColorsId, m_PaletteColors);
                    Blitter.BlitCameraTexture(
                        cmd,
                        m_LowResolutionTexture,
                        m_PaletteTexture,
                        m_PaletteMaterial,
                        0);
                    upsampleSource = m_PaletteTexture;
                }

                // Restore the quantized low-resolution image without interpolation.
                Blitter.BlitCameraTexture(cmd, upsampleSource, m_CameraColor, bilinear: false);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            m_LowResolutionTexture?.Release();
            m_LowResolutionTexture = null;
            m_PaletteTexture?.Release();
            m_PaletteTexture = null;
            CoreUtils.Destroy(m_PaletteMaterial);
        }
    }
}
