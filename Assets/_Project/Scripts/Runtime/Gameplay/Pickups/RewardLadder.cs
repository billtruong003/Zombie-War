using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Backlog #31, genre rules 1 and 7: the first minutes pay on a clock, so a new player always has
    /// something coming. 0:45 the first supply crate (<see cref="SupplyCrates"/>), 1:00 a golden
    /// zombie that bursts into a level of XP and some coin, 2:00 and 4:00 a chest dropped in front of
    /// the player. Times are run time, so they pause with the run. Added by PickupManager.
    /// </summary>
    public sealed class RewardLadder : MonoBehaviour
    {
        public const float GoldenAt = 60f;
        public static readonly float[] ChestTimes = { 120f, 240f };
        public const float GoldenHealthMultiplier = 10f, GoldenCoin = 40f;
        public const int GoldenOrbs = 14;
        public static readonly Color GoldenTint = new(1f, 0.78f, 0.12f, 0.85f);

        RunState _run;
        bool _goldenDone;
        int _nextChest;
        ZombieBase _golden;

        void OnEnable() => Bill.Events?.Subscribe<ZombieKilledEvent>(OnKilled);
        void OnDisable() => Bill.Events?.Unsubscribe<ZombieKilledEvent>(OnKilled);

        void Update()
        {
            var run = RunState.Current;
            if (run == null || run.IsOver) return;
            if (run != _run) { _run = run; _goldenDone = false; _nextChest = 0; _golden = null; }

            if (!_goldenDone && run.Duration >= GoldenAt) { _goldenDone = true; SpawnGolden(); }
            if (_nextChest < ChestTimes.Length && run.Duration >= ChestTimes[_nextChest])
            {
                _nextChest++;
                DropChestAhead();
            }
        }

        void SpawnGolden()
        {
            var director = Threat.ThreatDirector.Instance;
            var spawner = FindFirstObjectByType<ZombieSpawner>();
            var data = director != null ? director.PickCrowdFodder() : null;
            if (spawner == null || data == null) return;
            var z = spawner.Spawn(data);
            if (z == null) return;
            _golden = z;
            if (z.TryGetComponent(out Health hp)) hp.Configure(hp.Max * GoldenHealthMultiplier);
            z.SetStatusTint(GoldenTint);
        }

        void OnKilled(ZombieKilledEvent e)
        {
            if (_golden == null || e.Source != _golden) return;
            _golden = null;
            var run = RunState.Current;
            int xp = run != null ? run.XpForNextLevel : 30;
            PickupManager.Instance?.SpawnXpBurst(e.Position, xp, GoldenOrbs);
            PickupManager.SpawnBonusCoin(e.Position, Mathf.RoundToInt(GoldenCoin));
            Bill.Audio?.PlayCue("sfx.skill.evolve", e.Position, SfxPriority.High, 0.6f);
        }

        /// <summary>A free spot a few metres ahead of the player (where they are heading).</summary>
        static void DropChestAhead()
        {
            var player = PlayerMovement.Instance;
            var pm = PickupManager.Instance;
            if (player == null || pm == null) return;
            Vector3 origin = player.transform.position;
            Vector3 ahead = player.AimDirection; ahead.y = 0f;
            float baseAngle = ahead.sqrMagnitude > 0.01f ? Mathf.Atan2(ahead.z, ahead.x) : Random.value * Mathf.PI * 2f;
            for (int i = 0; i < 12; i++)
            {
                float a = baseAngle + Random.Range(-0.8f, 0.8f) * (i < 6 ? 1f : 3f);
                Vector3 at = origin + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(4f, 6f);
                if (WorldNav.MapNavigator.Blocked(at)) continue;
                pm.SpawnChest(at);
                return;
            }
            pm.SpawnChest(origin + new Vector3(1.5f, 0f, 1.5f));
        }
    }
}
