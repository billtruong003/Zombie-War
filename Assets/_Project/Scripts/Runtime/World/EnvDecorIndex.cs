using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.World
{
    /// <summary>
    /// Baked with each map chunk (2026-10-01): where every decoration piece of the chunk stands and
    /// how much ground it covers, so something that appears at play time (a station) can hide the
    /// pieces standing on its spot and show them again when it goes. Loaded chunks register
    /// themselves; the lookup is a plain scan of a few hundred entries, done once per spawn.
    /// </summary>
    public sealed class EnvDecorIndex : MonoBehaviour
    {
        [System.Serializable]
        public struct Piece
        {
            public GameObject go;
            public GameObject blocker;   // its NavObstacle collider holder, if it blocks movement
            public Vector2 local;        // position in the chunk, x/z
            public float radius;         // footprint radius
        }

        public Piece[] pieces = new Piece[0];
        public float halfSize = 16f;

        static readonly List<EnvDecorIndex> Loaded = new();

        void OnEnable() => Loaded.Add(this);
        void OnDisable() => Loaded.Remove(this);

        /// Hides every piece whose footprint reaches within <paramref name="radius"/> of
        /// <paramref name="at"/>; the hidden objects are added to <paramref name="hidden"/>.
        public static void HideAround(Vector3 at, float radius, List<GameObject> hidden)
        {
            foreach (var index in Loaded)
            {
                Vector3 c = index.transform.position;
                if (Mathf.Abs(at.x - c.x) > index.halfSize + radius + 6f || Mathf.Abs(at.z - c.z) > index.halfSize + radius + 6f) continue;
                foreach (var p in index.pieces)
                {
                    if (p.go == null || !p.go.activeSelf) continue;
                    var w = index.transform.TransformPoint(new Vector3(p.local.x, 0f, p.local.y));
                    float dx = w.x - at.x, dz = w.z - at.z, r = radius + p.radius;
                    if (dx * dx + dz * dz > r * r) continue;
                    p.go.SetActive(false);
                    hidden.Add(p.go);
                    if (p.blocker != null && p.blocker.activeSelf) { p.blocker.SetActive(false); hidden.Add(p.blocker); }
                }
            }
        }
    }
}
