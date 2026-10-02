using System.Collections.Generic;
using UnityEngine;

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
}
