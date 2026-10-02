using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ZombieWar
{
    /// <summary>
    /// Enemies that get a real (planar) shadow (owner 02/10: bosses and elites only, at every
    /// graphics tier; the crowd keeps its blob, the game being a many-enemy stress case). They
    /// register their renderer here; <see cref="PlanarShadowCastersFeature"/> draws only these
    /// with their material's "PlanarShadowVAT" pass, so normal enemies cost nothing extra.
    /// </summary>
    public static class PlanarShadowCasters
    {
        static readonly List<Renderer> Casters = new();

        public static IReadOnlyList<Renderer> All => Casters;

        public static void Add(Renderer r) { if (r != null && !Casters.Contains(r)) Casters.Add(r); }

        public static void Remove(Renderer r) => Casters.Remove(r);

        // Domain reload is off in this project: statics survive leaving Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Casters.Clear();
    }

    /// <summary>Draws the registered enemies' planar shadows after the opaques (where the player's
    /// "ZW Planar Shadows" pass draws), sharing its stencil so overlapping shadows never darken
    /// twice.</summary>
    public sealed class PlanarShadowCastersFeature : ScriptableRendererFeature
    {
        const string PassName = "PlanarShadowVAT";

        sealed class DrawPass : ScriptableRenderPass
        {
            sealed class Data { public IReadOnlyList<Renderer> casters; }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (PlanarShadowCasters.All.Count == 0 || Shader.GetGlobalFloat(PlanarOnId) < 0.5f) return;
                var res = frameData.Get<UniversalResourceData>();
                using var builder = renderGraph.AddRasterRenderPass<Data>("ZW Planar Shadows (bosses, elites)", out var data);
                data.casters = PlanarShadowCasters.All;
                builder.SetRenderAttachment(res.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(res.activeDepthTexture, AccessFlags.ReadWrite);   // the shared stencil
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((Data d, RasterGraphContext ctx) =>
                {
                    for (int i = 0; i < d.casters.Count; i++)
                    {
                        var r = d.casters[i];
                        if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || !r.isVisible) continue;
                        var m = r.sharedMaterial;
                        if (m == null) continue;
                        int pass = m.FindPass(PassName);
                        if (pass >= 0) ctx.cmd.DrawRenderer(r, m, 0, pass);
                    }
                });
            }

            static readonly int PlanarOnId = Shader.PropertyToID("_ZWPlanarShadowOn");
        }

        DrawPass _pass;

        public override void Create() => _pass = new DrawPass { renderPassEvent = RenderPassEvent.AfterRenderingOpaques };

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            renderer.EnqueuePass(_pass);
        }
    }
}
