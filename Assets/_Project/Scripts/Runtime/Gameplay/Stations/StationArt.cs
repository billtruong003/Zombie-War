using System;
using UnityEngine;

namespace ZombieWar.Stations
{
    /// <summary>
    /// Phase A9 (owner 2026-09-29: "modern sci-fi, not like before"): what each station looks like —
    /// its body, the reward icon over it and the burst when it pays — plus the Heal Zone's field.
    /// Loaded from Resources, so the look is data and the director on the Player prefab needs no
    /// new references. Built by HordeCall/Stations/Build A9 Stations.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieWar/Stations/Station Art", fileName = "StationArt")]
    public sealed class StationArt : ScriptableObject
    {
        [Serializable]
        public sealed class Look
        {
            public StationKind kind;
            public GameObject body;
            [Tooltip("What the station pays, shown inside the floating icon frame.")]
            public Sprite rewardIcon;
            [Tooltip("The burst when it completes (or, for the Heal Zone, switches on).")]
            public ParticleSystem completeFx;
        }

        public Look[] looks = Array.Empty<Look>();

        [Header("Heal Zone")]
        public ParticleSystem healFieldFx;
        public float healFieldNativeRadius = 2f;
        [Tooltip("A small heal burst on the player while the field heals them.")]
        public ParticleSystem healTickFx;

        public const string ResourcePath = "StationArt";

        static StationArt _instance;
        static bool _looked;

        public static StationArt Instance
        {
            get
            {
                if (_instance == null && !_looked)
                {
                    _looked = true;
                    _instance = Resources.Load<StationArt>(ResourcePath);
                }
                return _instance;
            }
        }

        Look Find(StationKind kind)
        {
            foreach (var l in looks) if (l != null && l.kind == kind) return l;
            return null;
        }

        public GameObject BodyFor(StationKind kind) => Find(kind)?.body;
        public Sprite IconFor(StationKind kind) => Find(kind)?.rewardIcon;
        public ParticleSystem CompleteFxFor(StationKind kind) => Find(kind)?.completeFx;
    }
}
