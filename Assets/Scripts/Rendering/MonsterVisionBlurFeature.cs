using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class MonsterVisionBlurFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader blurShader;
        public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        [Range(1, 4)] public int downsample = 1;
        public bool debugSolid;
        public Color debugColor = Color.magenta;
    }

    public Settings settings = new Settings();

    private Material material;
    private MonsterVisionBlurPass pass;

    public override void Create()
    {
        if (settings.blurShader == null)
            settings.blurShader = Shader.Find("Hidden/MonsterVisionBlur");

        if (settings.blurShader != null)
            material = CoreUtils.CreateEngineMaterial(settings.blurShader);

        pass = new MonsterVisionBlurPass(material, settings.downsample)
        {
            renderPassEvent = settings.passEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null)
            return;

        var cam = renderingData.cameraData.camera;
        var controller = cam.GetComponent<MonsterVisionBlurController>();
        if (controller == null || !controller.IsActive)
            return;

        pass.Setup(controller, settings.debugSolid, settings.debugColor);
        renderer.EnqueuePass(pass);
    }

    private class MonsterVisionBlurPass : ScriptableRenderPass
    {
        private static readonly int BlurStartId = Shader.PropertyToID("_BlurStart");
        private static readonly int BlurEndId = Shader.PropertyToID("_BlurEnd");
        private static readonly int MaxRadiusId = Shader.PropertyToID("_MaxBlurRadius");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int TintStrengthId = Shader.PropertyToID("_TintStrength");
        private static readonly int TintUseBlurId = Shader.PropertyToID("_TintUseBlur");
        private static readonly int TintMinId = Shader.PropertyToID("_TintMin");
        private static readonly int VignetteColorId = Shader.PropertyToID("_VignetteColor");
        private static readonly int VignetteIntensityId = Shader.PropertyToID("_VignetteIntensity");
        private static readonly int VignetteSmoothnessId = Shader.PropertyToID("_VignetteSmoothness");
        private static readonly int VignetteRadiusId = Shader.PropertyToID("_VignetteRadius");
        private static readonly int DebugSolidId = Shader.PropertyToID("_DebugSolid");
        private static readonly int DebugColorId = Shader.PropertyToID("_DebugColor");

        private readonly Material material;
        private readonly int downsample;
        private bool debugSolid;
        private Color debugColor;
        private RTHandle temp;
        private RTHandle source;
        private MonsterVisionBlurController controller;

        public MonsterVisionBlurPass(Material material, int downsample)
        {
            this.material = material;
            this.downsample = Mathf.Max(1, downsample);
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        }

        public void Setup(MonsterVisionBlurController controller, bool debugSolid, Color debugColor)
        {
            this.controller = controller;
            this.source = null;
            this.debugSolid = debugSolid;
            this.debugColor = debugColor;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.width /= downsample;
            desc.height /= downsample;

            RenderingUtils.ReAllocateIfNeeded(ref temp, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "MonsterVisionBlurTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || controller == null || source == null)
            {
                source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                if (material == null || controller == null || source == null)
                    return;
            }

            float blurStart = Mathf.Max(0f, controller.BlurStart);
            float blurEnd = Mathf.Max(blurStart, controller.BlurEnd);

            material.SetFloat(BlurStartId, blurStart);
            material.SetFloat(BlurEndId, blurEnd);
            material.SetFloat(MaxRadiusId, Mathf.Max(0f, controller.MaxBlurRadius));
            material.SetColor(TintColorId, controller.TintColor);
            material.SetFloat(TintStrengthId, Mathf.Clamp01(controller.TintStrength));
            material.SetFloat(TintUseBlurId, controller.TintUsesBlur ? 1f : 0f);
            material.SetFloat(TintMinId, Mathf.Clamp01(controller.TintMin));
            material.SetColor(VignetteColorId, controller.VignetteColor);
            material.SetFloat(VignetteIntensityId, Mathf.Clamp01(controller.VignetteIntensity));
            material.SetFloat(VignetteSmoothnessId, Mathf.Clamp01(controller.VignetteSmoothness));
            material.SetFloat(VignetteRadiusId, Mathf.Clamp01(controller.VignetteRadius));
            material.SetFloat(DebugSolidId, debugSolid ? 1f : 0f);
            material.SetColor(DebugColorId, debugColor);

            var cmd = CommandBufferPool.Get("MonsterVisionBlur");
            using (new ProfilingScope(cmd, new ProfilingSampler("MonsterVisionBlur")))
            {
                Blitter.BlitCameraTexture(cmd, source, temp, material, 0);
                Blitter.BlitCameraTexture(cmd, temp, source);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null || controller == null)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            float blurStart = Mathf.Max(0f, controller.BlurStart);
            float blurEnd = Mathf.Max(blurStart, controller.BlurEnd);
            float maxRadius = Mathf.Max(0f, controller.MaxBlurRadius);

            // Use cameraColor (intermediate buffer) instead of activeColorTexture,
            // which may point to the final RenderTexture or backbuffer and cannot
            // be used with GetTextureDesc or blitted to in RenderGraph.
            TextureHandle source = resourceData.cameraColor;
            if (!source.IsValid())
                return;

            // Build temp descriptor from camera data to avoid calling GetTextureDesc
            // on the backbuffer (which throws ArgumentException in AfterRenderingPostProcessing).
            var rtDesc = cameraData.cameraTargetDescriptor;
            var tempDesc = new TextureDesc(rtDesc.width / downsample, rtDesc.height / downsample)
            {
                colorFormat = rtDesc.graphicsFormat,
                msaaSamples = MSAASamples.None,
                clearBuffer = false,
                name = "MonsterVisionBlurTemp"
            };

            TextureHandle temp = renderGraph.CreateTexture(tempDesc);

            // Set material properties directly (MPB on BlitMaterialParameters is unreliable
            // across URP versions when the destination is the backbuffer).
            material.SetFloat(BlurStartId, blurStart);
            material.SetFloat(BlurEndId, blurEnd);
            material.SetFloat(MaxRadiusId, maxRadius);
            material.SetColor(TintColorId, controller.TintColor);
            material.SetFloat(TintStrengthId, Mathf.Clamp01(controller.TintStrength));
            material.SetFloat(TintUseBlurId, controller.TintUsesBlur ? 1f : 0f);
            material.SetFloat(TintMinId, Mathf.Clamp01(controller.TintMin));
            material.SetColor(VignetteColorId, controller.VignetteColor);
            material.SetFloat(VignetteIntensityId, Mathf.Clamp01(controller.VignetteIntensity));
            material.SetFloat(VignetteSmoothnessId, Mathf.Clamp01(controller.VignetteSmoothness));
            material.SetFloat(VignetteRadiusId, Mathf.Clamp01(controller.VignetteRadius));
            material.SetFloat(DebugSolidId, debugSolid ? 1f : 0f);
            material.SetColor(DebugColorId, debugColor);

            // Copy active color -> temp
            RenderGraphUtils.BlitMaterialParameters copyParams = new(source, temp, Blitter.GetBlitMaterial(TextureDimension.Tex2D), 0);
            renderGraph.AddBlitPass(copyParams, "MonsterVisionBlur_Copy");

            // Apply effect temp -> active color
            RenderGraphUtils.BlitMaterialParameters blurParams = new(temp, source, material, 0);
            renderGraph.AddBlitPass(blurParams, "MonsterVisionBlur_Apply");
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // Keep temp RTHandle allocated for reuse.
        }
    }
}
