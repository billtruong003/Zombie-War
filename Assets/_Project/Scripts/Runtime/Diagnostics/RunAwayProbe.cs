#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Diagnostics
{
    /// <summary>
    /// M7.4a diagnostic — drives the player in a straight line and samples pressure.
    ///
    /// This exists to answer the owner's exact complaint with numbers rather than adjectives:
    /// "run away and nothing new appears". It measures alive count, threat tier, recycles, and the
    /// BEARING enemies arrive from, so a one-direction tail is visible in the data.
    ///
    /// <b>Compiled out of player builds</b> by the UNITY_EDITOR guard around this file (M7.4b).
    ///
    /// It lives in the runtime assembly rather than the Editor one for a concrete reason: an Editor
    /// assembly MonoBehaviour cannot be AddComponent'd during Play Mode, which is exactly how this is
    /// used. The define achieves the actual goal — no diagnostic code in a shipped build — without
    /// making the tool unusable.
    /// </summary>
    public class RunAwayProbe : MonoBehaviour
    {
        public static RunAwayProbe Instance { get; private set; }

        public Transform player;
        public Rigidbody body;
        public float runSpeed = 5f;
        public float sampleInterval = 10f;

        public readonly List<string> Samples = new();
        float _elapsed, _nextSample;
        int _lastArrivals;
        public readonly List<int> AliveSeries = new();
        public readonly List<float> RateSeries = new();

        /// <summary>Population variance of the alive-count series — the flatness metric.</summary>
        public float AliveVariance()
        {
            if (AliveSeries.Count == 0) return 0f;
            float mean = 0f; for (int i = 0; i < AliveSeries.Count; i++) mean += AliveSeries[i];
            mean /= AliveSeries.Count;
            float v = 0f; for (int i = 0; i < AliveSeries.Count; i++) { float d = AliveSeries[i] - mean; v += d * d; }
            return v / AliveSeries.Count;
        }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void FixedUpdate()
        {
            // Run in ONE direction, continuously — the scenario that used to empty the world.
            if (body != null)
                body.MovePosition(body.position + Vector3.forward * runSpeed * Time.fixedDeltaTime);
        }

        void Update()
        {
            if (player == null) return;
            _elapsed += Time.deltaTime;
            if (_elapsed < _nextSample) return;
            _nextSample += sampleInterval;

            int ahead = 0, behind = 0, left = 0, right = 0;
            var zs = FindObjectsByType<ZombieBase>(FindObjectsSortMode.None);
            for (int i = 0; i < zs.Length; i++)
            {
                Vector3 d = zs[i].transform.position - player.position;
                d.y = 0f;
                // Bearing relative to the run direction (+Z).
                if (d.z > Mathf.Abs(d.x)) ahead++;
                else if (-d.z > Mathf.Abs(d.x)) behind++;
                else if (d.x > 0f) right++;
                else left++;
            }

            var td = Threat.ThreatDirector.Instance;

            // Arrival RATE is the metric this milestone is judged on: same average, lower variance.
            int arrivals = Threat.ThreatDirector.ArrivalsThisRun;
            float perSecond = (arrivals - _lastArrivals) / Mathf.Max(0.001f, sampleInterval);
            _lastArrivals = arrivals;
            AliveSeries.Add(ZombieManager.AliveCount);
            RateSeries.Add(perSecond);

            Samples.Add(
                $"t={_elapsed:F0}s alive={ZombieManager.AliveCount} " +
                $"arrivals/s={perSecond:F2} " +
                $"attackers={ZombieManager.AttackSlotsInUse}/{ZombieManager.AttackSlotCap} " +
                $"tier={(td != null ? td.CurrentTier : -1)} " +
                $"recycled={ZombieManager.RecycledCount} " +
                $"travelled={player.position.magnitude:F0}m " +
                $"[ahead {ahead} | behind {behind} | left {left} | right {right}]");
        }
    }
}
#endif // UNITY_EDITOR
