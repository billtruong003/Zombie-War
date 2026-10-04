using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ZombieWar
{
    /// <summary>The game's bloom settings, per map through the volume profile.</summary>
    [Serializable, VolumeComponentMenu("HordeCall/Toon Bloom")]
    public sealed class ToonBloom : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 3f);
        [Tooltip("Brightness (LDR, 0-1) above which a pixel glows.")]
        public ClampedFloatParameter threshold = new ClampedFloatParameter(0.8f, 0f, 1f);
        [Tooltip("Softens the threshold so glow fades in instead of switching on.")]
        public ClampedFloatParameter knee = new ClampedFloatParameter(0.35f, 0f, 1f);
        public ColorParameter tint = new ColorParameter(Color.white);

        public bool IsActive() => intensity.value > 0.001f;
        public bool IsTileCompatible() => false;
    }

    /// <summary>
    /// Cheap bloom (2026-10-02): dual filter (Bjorge, SIGGRAPH 2015) below quarter resolution, added
    /// onto the camera colour before URP's post (grading, vignette) runs. Iterations follow the
    /// graphics tier: none on low, 2 on mid, 3 on high. Replaces URP's Bloom.
    /// </summary>
    public sealed class ToonBloomFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader shader;

        sealed class BloomPass : ScriptableRenderPass
        {
            static readonly int ParamsId = Shader.PropertyToID("_BloomParams");
            static readonly int TintId = Shader.PropertyToID("_BloomTint");
            const int MaxIterations = 4;
            readonly TextureHandle[] _levels = new TextureHandle[MaxIterations + 1];
            // Texture names built once: "_ToonBloom" + i made two or three strings every frame.
            static readonly string[] LevelNames = BuildNames();
            static string[] BuildNames()
            {
                var n = new string[MaxIterations + 1];
                for (int i = 0; i < n.Length; i++) n[i] = "_ToonBloom" + i;
                return n;
            }
            public Material material;

            public BloomPass() { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing; }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var settings = VolumeManager.instance.stack.GetComponent<ToonBloom>();
                int iterations = Mathf.Min(GraphicsTier.BloomIterations, MaxIterations);
                if (material == null || settings == null || !settings.IsActive() || iterations <= 0) return;
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var camera = frameData.Get<UniversalCameraData>();

                material.SetVector(ParamsId, new Vector4(settings.threshold.value, settings.knee.value, settings.intensity.value, 0f));
                material.SetColor(TintId, settings.tint.value);

                var desc = camera.cameraTargetDescriptor;
                desc.depthBufferBits = 0; desc.msaaSamples = 1; desc.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
                int w = Mathf.Max(1, desc.width / 4), h = Mathf.Max(1, desc.height / 4);
                desc.width = w; desc.height = h;
                _levels[0] = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_ToonBloom0", false, FilterMode.Bilinear);
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(resources.activeColorTexture, _levels[0], material, 0), "ToonBloom Prefilter");

                int made = 0;
                for (int i = 1; i <= iterations; i++)
                {
                    w = Mathf.Max(1, w / 2); h = Mathf.Max(1, h / 2);
                    desc.width = w; desc.height = h;
                    _levels[i] = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, LevelNames[i], false, FilterMode.Bilinear);
                    renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(_levels[i - 1], _levels[i], material, 1), "ToonBloom Down");
                    made = i;
                    if (w == 1 && h == 1) break;
                }
                for (int i = made; i > 0; i--)
                    renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(_levels[i], _levels[i - 1], material, 2), "ToonBloom Up");

                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(_levels[0], resources.activeColorTexture, material, 3), "ToonBloom Composite");
            }
        }

        BloomPass _pass;
        Material _material;

        public override void Create()
        {
            if (shader == null) shader = Shader.Find("Hidden/HordeCall/ToonBloom");
            if (shader != null && _material == null) _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new BloomPass { material = _material };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || renderingData.cameraData.cameraType != CameraType.Game) return;
            // Only the game view (02/10): not the menu's character preview drawn into a render
            // texture, and not cameras with post off (the Low preset, the menu).
            if (!renderingData.cameraData.postProcessEnabled || renderingData.cameraData.camera.targetTexture != null) return;
            // In play only during a run: the menu's camera has post on too (GameSettings) and its UI
            // whites would bloom. Edit mode keeps it so the fluid lab shows the lava glowing.
            if (Application.isPlaying && !GameFlow.InGameplay) return;
            if (GraphicsTier.BloomIterations <= 0) return;
            var settings = VolumeManager.instance.stack.GetComponent<ToonBloom>();
            if (settings == null || !settings.IsActive()) return;
            _pass.ConfigureInput(ScriptableRenderPassInput.Color);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(_material);
    }
}
