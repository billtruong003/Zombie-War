using System.Collections.Generic;
using UnityEngine;
using ZombieWar.World;

namespace ZombieWar.Stations
{
    /// <summary>
    /// A station makes room for itself when it appears (2026-10-01). Stations are placed at play time
    /// from the anchor grid, over a map whose decoration was baked in advance, so:
    ///  - <see cref="Resolve"/> moves the spot, by at most a few metres, to where the station's ring
    ///    and a walkway around it touch no obstacle (basins, bridges' banks, rocks, trunks, big
    ///    crates: everything on the NavObstacle layer). The candidate order is fixed, so the same
    ///    anchor always lands on the same spot.
    ///  - the <see cref="StationClearing"/> component hides the decoration standing on the ring and
    ///    shows it again when the station goes, whichever way it goes.
    /// </summary>
    public static class StationClearance
    {
        public const float ClearRadius = 4.4f;     // ring 2.9 m + a 1.5 m walkway
        public const float SearchRadius = 10f;
        const float Roomy = 6f;                    // preferred: no obstacle this close (keeps trunks off the beam)

        static int _mask = -1;
        static int Mask => _mask >= 0 ? _mask : (_mask = LayerMask.GetMask("NavObstacle"));

        static bool Clear(Vector3 p, float r) =>
            !Physics.CheckCapsule(p + Vector3.up * 0.3f, p + Vector3.up * 1.2f, r, Mask, QueryTriggerInteraction.Ignore);

        /// The spot the station should stand on: the anchor itself when it is clear, otherwise the
        /// first clear candidate on rings of 2, 4, 6, 8 and 10 m. Roomy spots win over merely clear ones.
        public static Vector3 Resolve(Vector3 anchor)
        {
            if (Mask == 0) return anchor;
            Vector3 firstClear = anchor;
            bool found = false;
            for (int ring = 0; ring <= 5; ring++)
            {
                float d = ring * SearchRadius / 5f;
                int count = ring == 0 ? 1 : ring * 8;
                for (int k = 0; k < count; k++)
                {
                    float a = (k + 0.5f * ring) / count * Mathf.PI * 2f;
                    var p = anchor + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
                    if (!Clear(p, ClearRadius)) continue;
                    if (Clear(p, Roomy)) return p;
                    if (!found) { firstClear = p; found = true; }
                }
            }
            return firstClear;
        }
    }

    /// <summary>Hides the decoration on a station's ring while the station exists.</summary>
    public sealed class StationClearing : MonoBehaviour
    {
        readonly List<GameObject> _hidden = new();

        Vector3 _at;

        void Start()
        {
            _at = transform.position;
            EnvDecorIndex.HideAround(_at, StationClearance.ClearRadius, _hidden);
            // The hidden pieces take their blockers with them: enemies may now walk the ring.
            if (_hidden.Count > 0) ZombieWar.WorldNav.MapNavigator.MarkDirty(_at, StationClearance.ClearRadius);
            // Grass on the ring lies flat instead of being cut to a bald patch.
            GrassBenders.AddStanding(_at, 3.2f);
        }

        void OnDestroy()
        {
            foreach (var go in _hidden) if (go != null) go.SetActive(true);
            if (_hidden.Count > 0) ZombieWar.WorldNav.MapNavigator.MarkDirty(_at, StationClearance.ClearRadius);
            _hidden.Clear();
            GrassBenders.RemoveStanding(_at);
        }
    }
}
