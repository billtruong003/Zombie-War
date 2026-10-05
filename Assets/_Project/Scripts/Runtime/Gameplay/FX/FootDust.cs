using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// 05/10 owner: running kicks up a soft puff of dust at the player's feet (Epic Toon
    /// DustDirtyPoofSoft, copied to Resources/FX/FootDust). Pooled through FxPool and spaced out so
    /// it reads as footsteps, not a trail. Added by PickupManager, so it lives only in a run.
    /// </summary>
    public sealed class FootDust : MonoBehaviour
    {
        public const float MinSpeed = 1.5f, Every = 0.3f, Scale = 0.45f;

        ParticleSystem _prefab;
        bool _loaded;
        float _nextAt;
        Vector3 _last;
        bool _hasLast;

        void Update()
        {
            var player = PlayerMovement.Instance;
            if (player == null) { _hasLast = false; return; }
            Vector3 p = player.transform.position;
            float speed = _hasLast ? new Vector2(p.x - _last.x, p.z - _last.z).magnitude / Mathf.Max(Time.deltaTime, 1e-4f) : 0f;
            _last = p; _hasLast = true;
            if (speed < MinSpeed || Time.time < _nextAt) return;
            _nextAt = Time.time + Every;
            if (!_loaded) { _prefab = Resources.Load<ParticleSystem>("FX/FootDust"); _loaded = true; }
            if (_prefab != null) FxPool.Play(_prefab, new Vector3(p.x, p.y + 0.05f, p.z), Quaternion.identity, Scale);
        }
    }
}
