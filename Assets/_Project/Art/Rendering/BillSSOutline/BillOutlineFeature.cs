// Imported from https://github.com/billtruong003/Bill-SSOutline @ 2acf5b72 (pinned)
// Isolated under ZombieWar.Rendering.BillSSOutline for A/B evaluation against the STW outline.
// Do not merge into the Stylized Toon World Kit.
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ZombieWar.Rendering.BillSSOutline
{
    public class OutlineFeature : ScriptableRendererFeature
    {
        // Static Event để các hệ thống render thủ công (như Foliage) đăng ký vẽ vào Mask
        public static event Action<RasterCommandBuffer, LayerMask> OnRenderFoliageMask;

        class LayerMaskPass : ScriptableRenderPass
        {
            private Material maskMaterial;
            private LayerMask layerMask;
            private FilteringSettings filteringSettings;
            private readonly ShaderTagId[] shaderTags;
            private string profilerTag;
            private string textureName;

            public TextureHandle MaskTexture { get; private set; }

            public LayerMaskPass(string tag, string texName)
            {
                profilerTag = tag;
                textureName = texName;
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

                shaderTags = new ShaderTagId[]
                {
                    new ShaderTagId("UniversalForward"),
                    new ShaderTagId("UniversalForwardOnly"),
                    new ShaderTagId("SRPDefaultUnlit"),
                    new ShaderTagId("LightweightForward")
                };
                filteringSettings = new FilteringSettings(RenderQueueRange.all);
            }

            public void Setup(LayerMask mask)
            {
                this.layerMask = mask;
                filteringSettings.layerMask = mask;
                if (maskMaterial == null) maskMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Outline/SelectionMask"));
            }

            private class MaskData
            {
                public RendererListHandle rendererList;
                public TextureHandle maskDest;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                MaskTexture = TextureHandle.nullHandle;

                if (maskMaterial == null || layerMask == 0) return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();

                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.colorFormat = RenderTextureFormat.R8;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;

                TextureDesc texDesc = new TextureDesc(desc);
                texDesc.name = textureName;
                texDesc.clearBuffer = true;
                texDesc.clearColor = Color.black;

                MaskTexture = renderGraph.CreateTexture(texDesc);

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                TextureHandle depthTexture = resourceData.activeDepthTexture;

                RendererListParams rlParams = new RendererListParams(
                    renderingData.cullResults,
                    new DrawingSettings(shaderTags[0], new SortingSettings(cameraData.camera))
                    {
                        overrideMaterial = maskMaterial,
                        overrideMaterialPassIndex = 0
                    },
                    filteringSettings
                );

                for (int i = 1; i < shaderTags.Length; ++i)
                    rlParams.drawSettings.SetShaderPassName(i, shaderTags[i]);

                RendererListHandle rendererList = renderGraph.CreateRendererList(rlParams);

                using (var builder = renderGraph.AddRasterRenderPass<MaskData>(profilerTag, out var passData))
                {
                    passData.rendererList = rendererList;
                    passData.maskDest = MaskTexture;

                    builder.UseRendererList(passData.rendererList);
                    builder.SetRenderAttachment(passData.maskDest, 0, AccessFlags.Write);

                    if (depthTexture.IsValid()) builder.SetRenderAttachmentDepth(depthTexture, AccessFlags.Read);

                    // Copy LayerMask to local variable to avoid closure capture issues
                    LayerMask currentMask = layerMask;

                    builder.SetRenderFunc((MaskData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.rendererList);

                        // Trigger Foliage Rendering
                        OnRenderFoliageMask?.Invoke(context.cmd, currentMask);
                    });
                }
            }
            public void Dispose() { CoreUtils.Destroy(maskMaterial); }
        }

        class OutlinePass : ScriptableRenderPass
        {
            private Material material;
            private OutlineVolume volumeSettings;
            private const string ShaderName = "Hidden/FullScreen/Outline";

            private static readonly int ThicknessID = Shader.PropertyToID("_Thickness");
            private static readonly int ColorID = Shader.PropertyToID("_OutlineColor");
            private static readonly int DepthThresholdID = Shader.PropertyToID("_DepthThreshold");
            private static readonly int NormalThresholdID = Shader.PropertyToID("_NormalThreshold");
            private static readonly int ColorThresholdID = Shader.PropertyToID("_ColorThreshold");
            private static readonly int DebugModeID = Shader.PropertyToID("_DebugMode");
            private static readonly int SelectionMaskID = Shader.PropertyToID("_SelectionMaskTexture");
            private static readonly int OcclusionMaskID = Shader.PropertyToID("_OcclusionMaskTexture");
            private static readonly int FadeParamsID = Shader.PropertyToID("_FadeParams");

            private LayerMaskPass selectionMaskPass;
            private LayerMaskPass occlusionMaskPass;

            private class PassData
            {
                public Material material;
                public TextureHandle source;
                public TextureHandle destination;
                public TextureHandle mask;
                public TextureHandle occlusion;
            }

            public OutlinePass() { renderPassEvent = RenderPassEvent.AfterRenderingTransparents; }

            public void SetupReference(LayerMaskPass selectPass, LayerMaskPass occludePass)
            {
                this.selectionMaskPass = selectPass;
                this.occlusionMaskPass = occludePass;
            }

            private bool UpdateMaterial()
            {
                var stack = VolumeManager.instance.stack;
                volumeSettings = stack.GetComponent<OutlineVolume>();
                if (volumeSettings == null || !volumeSettings.IsActive()) return false;
                if (material == null) material = CoreUtils.CreateEngineMaterial(Shader.Find(ShaderName));
                if (material == null) return false;

                material.SetFloat(ThicknessID, volumeSettings.thickness.value);
                material.SetColor(ColorID, volumeSettings.outlineColor.value);
                material.SetFloat(DepthThresholdID, volumeSettings.depthThreshold.value);
                material.SetFloat(NormalThresholdID, volumeSettings.normalThreshold.value);
                material.SetFloat(ColorThresholdID, volumeSettings.colorThreshold.value);
                material.SetInt(DebugModeID, (int)volumeSettings.debugMode.value);

                material.SetVector(FadeParamsID, new Vector4(
                    volumeSettings.fadeDistanceStart.value,
                    volumeSettings.fadeDistanceEnd.value,
                    volumeSettings.fadeHeightMin.value,
                    volumeSettings.fadeHeightMax.value
                ));

                SetKeyword("USE_DEPTH", volumeSettings.useDepth.value);
                SetKeyword("USE_NORMALS", volumeSettings.useNormals.value);
                SetKeyword("USE_COLOR", volumeSettings.useColor.value);
                SetKeyword("ALGO_SOBEL", volumeSettings.algorithm.value == OutlineVolume.OutlineAlgorithm.Sobel);
                SetKeyword("ALGO_ROBERTS", volumeSettings.algorithm.value == OutlineVolume.OutlineAlgorithm.RobertsCross);

                SetKeyword("USE_DISTANCE_FADE", volumeSettings.useDistanceFade.value);
                SetKeyword("USE_HEIGHT_FADE", volumeSettings.useHeightFade.value);
                SetKeyword("USE_OCCLUSION_MASK", volumeSettings.occlusionLayer.value != 0);

                var mode = volumeSettings.mode.value;
                SetKeyword("OUTLINE_FULL", mode == OutlineVolume.OutlineMode.FullScreen);
                SetKeyword("OUTLINE_SELECTION", mode == OutlineVolume.OutlineMode.SelectionOnly);
                SetKeyword("OUTLINE_MIXED", mode == OutlineVolume.OutlineMode.Mixed);

                return true;
            }

            private void SetKeyword(string k, bool v) { if (v) material.EnableKeyword(k); else material.DisableKeyword(k); }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!UpdateMaterial()) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (resourceData.isActiveTargetBackBuffer) return;

                TextureHandle source = resourceData.activeColorTexture;

                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                TextureDesc texDesc = new TextureDesc(desc);
                texDesc.name = "OutlineTemp";
                texDesc.clearBuffer = false;

                TextureHandle tempTexture = renderGraph.CreateTexture(texDesc);

                TextureHandle maskHandle = (selectionMaskPass != null) ? selectionMaskPass.MaskTexture : TextureHandle.nullHandle;
                TextureHandle occlusionHandle = (occlusionMaskPass != null) ? occlusionMaskPass.MaskTexture : TextureHandle.nullHandle;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Outline Composite", out var passData))
                {
                    passData.material = material;
                    passData.source = source;
                    passData.destination = tempTexture;
                    passData.mask = maskHandle;
                    passData.occlusion = occlusionHandle;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    if (passData.mask.IsValid()) builder.UseTexture(passData.mask, AccessFlags.Read);
                    if (passData.occlusion.IsValid()) builder.UseTexture(passData.occlusion, AccessFlags.Read);

                    builder.SetRenderAttachment(passData.destination, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        if (data.mask.IsValid()) data.material.SetTexture(SelectionMaskID, data.mask);
                        if (data.occlusion.IsValid()) data.material.SetTexture(OcclusionMaskID, data.occlusion);
                        else data.material.SetTexture(OcclusionMaskID, Texture2D.blackTexture);

                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }

                resourceData.cameraColor = tempTexture;
            }
            public void Dispose() { CoreUtils.Destroy(material); }
        }

        private LayerMaskPass selectionPass;
        private LayerMaskPass occlusionPass;
        private OutlinePass outlinePass;

        public override void Create()
        {
            selectionPass = new LayerMaskPass("Outline Selection Mask", "_SelectionMaskTexture");
            occlusionPass = new LayerMaskPass("Outline Occlusion Mask", "_OcclusionMaskTexture");
            outlinePass = new OutlinePass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraData cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game ||
                cameraData.renderType != CameraRenderType.Base)
                return;

            var stack = VolumeManager.instance.stack;
            var settings = stack.GetComponent<OutlineVolume>();

            if (settings != null && settings.IsActive())
            {
                outlinePass.SetupReference(selectionPass, occlusionPass);

                // Optimization over stock (2acf5b7): the selection redraw only runs when a mode
                // that actually consumes the selection mask is active. Stock enqueued it even in
                // FullScreen mode, re-rendering every selected-layer object into an unused mask.
                bool wantsSelection = settings.mode.value != OutlineVolume.OutlineMode.FullScreen
                                      && settings.selectionLayer.value != 0;
                if (wantsSelection)
                {
                    selectionPass.Setup(settings.selectionLayer.value);
                    renderer.EnqueuePass(selectionPass);
                }
                if (settings.occlusionLayer.value != 0)
                {
                    occlusionPass.Setup(settings.occlusionLayer.value);
                    renderer.EnqueuePass(occlusionPass);
                }

                // Optimization over stock: request only the renderer inputs the active feature set
                // needs. Stock requested Color|Depth|Normal unconditionally - the Color (opaque
                // texture) copy is never sampled by the composite (it reads the blit source), and
                // the DepthNormals prepass is pure waste when normal edges are disabled.
                var inputs = ScriptableRenderPassInput.None;
                if (settings.useDepth.value || settings.useDistanceFade.value || settings.useHeightFade.value)
                    inputs |= ScriptableRenderPassInput.Depth;
                if (settings.useNormals.value)
                    inputs |= ScriptableRenderPassInput.Normal;
                outlinePass.ConfigureInput(inputs);

                renderer.EnqueuePass(outlinePass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            selectionPass.Dispose();
            occlusionPass.Dispose();
            outlinePass.Dispose();
        }
    }
}
