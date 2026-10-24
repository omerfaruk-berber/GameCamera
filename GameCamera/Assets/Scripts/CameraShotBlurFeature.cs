using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>Applies a brief edge blur only to cameras with an active Camera Shots blur transition.</summary>
[DisallowMultipleRendererFeature("Camera Shot Edge Blur")]
public sealed class CameraShotBlurFeature : ScriptableRendererFeature
{
    [SerializeField, Tooltip("Edge blur shader referenced by the renderer asset so player builds retain it.")]
    private Shader blurShader;

    private static readonly Dictionary<Camera, float> blurAmounts = new Dictionary<Camera, float>(4);
    private Material blurMaterial;
    private BlurPass blurPass;

    /// <summary>The explicitly referenced shader used by this renderer feature.</summary>
    public Shader BlurShader => blurShader;

    /// <summary>Sets a camera's transition blur strength. Zero and non-finite values clear the effect.</summary>
    public static void SetBlur(Camera camera, float amount)
    {
        if (camera == null)
        {
            return;
        }
        if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
        {
            ClearBlur(camera);
            return;
        }
        blurAmounts[camera] = Mathf.Clamp01(amount);
    }

    /// <summary>Removes a camera from the blur registry, including a destroyed camera's managed reference.</summary>
    public static void ClearBlur(Camera camera)
    {
        if (!ReferenceEquals(camera, null))
        {
            blurAmounts.Remove(camera);
        }
    }

    /// <summary>Returns an active camera's blur strength without allocating or searching the scene.</summary>
    public static bool TryGetBlur(Camera camera, out float amount)
    {
        amount = 0f;
        return camera != null && blurAmounts.TryGetValue(camera, out amount);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        blurAmounts.Clear();
    }

    public override void Create()
    {
        DisposeResources();
        if (blurShader == null || !blurShader.isSupported)
        {
            return;
        }
        blurMaterial = CoreUtils.CreateEngineMaterial(blurShader);
        blurPass = new BlurPass(blurMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        if (blurPass == null || camera == null || camera.cameraType != CameraType.Game ||
            !TryGetBlur(camera, out float amount))
        {
            return;
        }
        blurPass.Setup(amount);
        renderer.EnqueuePass(blurPass);
    }

    protected override void Dispose(bool disposing)
    {
        DisposeResources();
    }

    private void DisposeResources()
    {
        blurPass?.Dispose();
        blurPass = null;
        CoreUtils.Destroy(blurMaterial);
        blurMaterial = null;
    }

    private sealed class BlurPass : ScriptableRenderPass
    {
        private const string PassName = "Camera Shot Edge Blur";
        private const string VerticalPassName = "Camera Shot Edge Blur Vertical";
        private const string TextureName = "_CameraShotBlurTexture";
        // The Gaussian kernel's furthest bilinear sample is 3.23 times this radius.
        private const float MaximumRadiusPixels = 3f;
        private static readonly int parametersId = Shader.PropertyToID("_ShotBlurParameters");
        private readonly Material material;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private float amount;

#if URP_COMPATIBILITY_MODE
        private RTHandle temporaryColor;
#endif

        internal BlurPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            profilingSampler = new ProfilingSampler(PassName);
            requiresIntermediateTexture = true;
            // Renderer2D 17.3 checks Color input rather than requiresIntermediateTexture.
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        internal void Setup(float blurAmount)
        {
            amount = blurAmount;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer || amount <= 0f)
            {
                return;
            }

            TextureHandle source = resources.activeColorTexture;
            if (!source.IsValid())
            {
                return;
            }
            TextureDesc descriptor = renderGraph.GetTextureDesc(source);
            SetParameters(descriptor.width, descriptor.height);
            descriptor.name = TextureName;
            descriptor.clearBuffer = false;
            descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            descriptor.bindTextureMS = false;
            descriptor.filterMode = FilterMode.Point;
            descriptor.wrapMode = TextureWrapMode.Clamp;
            TextureHandle destination = renderGraph.CreateTexture(descriptor);

            RenderGraphUtils.BlitMaterialParameters blur =
                new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
            blur.propertyBlock = properties;
            renderGraph.AddBlitPass(blur, PassName);
            // Keep the original camera attachment alive for overlay cameras and persistent targets.
            RenderGraphUtils.BlitMaterialParameters vertical =
                new RenderGraphUtils.BlitMaterialParameters(destination, source, material, 1);
            vertical.propertyBlock = properties;
            renderGraph.AddBlitPass(vertical, VerticalPassName);
        }

        private void SetParameters(int width, int height)
        {
            float radius = MaximumRadiusPixels * amount;
            properties.SetVector(parametersId, new Vector4(amount,
                radius / Mathf.Max(1, width), radius / Mathf.Max(1, height), 0f));
        }

#if URP_COMPATIBILITY_MODE
#pragma warning disable CS0618, CS0672
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            RenderingUtils.ReAllocateHandleIfNeeded(ref temporaryColor, descriptor, FilterMode.Point,
                TextureWrapMode.Clamp, name: TextureName);
            SetParameters(descriptor.width, descriptor.height);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;
            if (source == null || temporaryColor == null || amount <= 0f)
            {
                return;
            }
            CommandBuffer command = CommandBufferPool.Get();
            using (new ProfilingScope(command, profilingSampler))
            {
                // BlitCameraTexture supplies RTHandle scaling and XR texture coordinates.
                material.SetVector(parametersId, properties.GetVector(parametersId));
                Blitter.BlitCameraTexture(command, source, temporaryColor, material, 0);
                Blitter.BlitCameraTexture(command, temporaryColor, source, material, 1);
            }
            context.ExecuteCommandBuffer(command);
            CommandBufferPool.Release(command);
        }
#pragma warning restore CS0618, CS0672
#endif

        internal void Dispose()
        {
#if URP_COMPATIBILITY_MODE
            temporaryColor?.Release();
            temporaryColor = null;
#endif
        }
    }
}
