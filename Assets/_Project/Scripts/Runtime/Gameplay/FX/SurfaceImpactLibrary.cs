using System;
using UnityEngine;

namespace ZombieWar
{
    /// One Epic Toon effect and sound per material. Lives at Resources/SurfaceImpacts.asset.
    [CreateAssetMenu(menuName = "ZombieWar/FX/Surface Impacts", fileName = "SurfaceImpacts")]
    public sealed class SurfaceImpactLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SurfaceKind kind;
            public ParticleSystem fx;
            public float scale = 1f;
            [Tooltip("Played for props and ground only: an enemy already plays its own impact sound.")]
            public string sfxKey;
            public float volume = 0.4f;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public Entry For(SurfaceKind kind)
        {
            for (int i = 0; i < entries.Length; i++) if (entries[i].kind == kind) return entries[i];
            return null;
        }

        static SurfaceImpactLibrary _instance;
        static bool _looked;
        public static SurfaceImpactLibrary Instance
        {
            get
            {
                if (!_looked) { _instance = Resources.Load<SurfaceImpactLibrary>("SurfaceImpacts"); _looked = true; }
                return _instance;
            }
        }
    }
}
