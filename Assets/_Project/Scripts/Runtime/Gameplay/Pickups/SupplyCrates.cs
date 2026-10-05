using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// 05/10: a loot crate turns up near the player every so often (the breakable props were gone
    /// from the maps, so nothing on the ground was worth walking to). Auto-aim prefers a crate once
    /// the player walks up to it (DestructibleProp.AimBias); it breaks into XP and the item the
    /// player needs (PickupManager.DropCrateLoot). Added by PickupManager, so it lives only in a run.
    /// </summary>
    public sealed class SupplyCrates : MonoBehaviour
    {
        public const string PoolKey = "prop_crate";
        // Backlog #31 (reward ladder): the first crate at 0:45, between the first card (~0:30) and
        // the golden zombie (1:00), so the first minute has a new thing to walk to every ~15 s.
        public const float FirstAfter = 45f;
        public const float Every = 45f;
        public const int MaxOnMap = 2;
        public const float MinDistance = 7f, MaxDistance = 10f;

        float _nextAt;
        readonly System.Collections.Generic.List<DestructibleProp> _crates = new();

        void OnEnable() => _nextAt = Time.time + FirstAfter;

        void Update()
        {
            if (Time.time < _nextAt) return;
            _nextAt = Time.time + Every;
            _crates.RemoveAll(c => c == null || !c.isActiveAndEnabled);
            if (_crates.Count >= MaxOnMap) return;
            var player = PlayerMovement.Instance;
            if (player == null || Bill.Pool == null) return;
            if (TryPlace(player, out Vector3 at)) Spawn(at);
        }

        /// <summary>A free spot ahead of the player (where they are heading), else any free spot.</summary>
        static bool TryPlace(PlayerMovement player, out Vector3 at)
        {
            Vector3 origin = player.transform.position;
            Vector3 ahead = player.AimDirection; ahead.y = 0f;
            float baseAngle = ahead.sqrMagnitude > 0.01f ? Mathf.Atan2(ahead.z, ahead.x) : Random.value * Mathf.PI * 2f;
            for (int i = 0; i < 12; i++)
            {
                float spread = i < 6 ? 1.05f : Mathf.PI;   // ±60° first, then all around
                float a = baseAngle + Random.Range(-spread, spread);
                float d = Random.Range(MinDistance, MaxDistance);
                at = origin + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
                if (!WorldNav.MapNavigator.Blocked(at)) return true;
            }
            at = default;
            return false;
        }

        void Spawn(Vector3 at)
        {
            var go = Bill.Pool.Spawn(PoolKey, at, Quaternion.Euler(0f, Random.value * 360f, 0f));
            if (go == null || !go.TryGetComponent(out DestructibleProp crate)) return;
            crate.PoolKey = PoolKey;
            _crates.Add(crate);
        }
    }
}
