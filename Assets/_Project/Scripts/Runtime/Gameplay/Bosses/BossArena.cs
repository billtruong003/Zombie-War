using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Bosses
{
    /// <summary>
    /// The ring a Titan is fought in (backlog #39/#42). Wall segments on the NavObstacle layer, so they
    /// stop the player and the crowd while bullets pass (Weapon ignores NavObstacle). Two lab options:
    /// <b>Solid</b> (a plain wall) and <b>Shock</b> (touching it knocks the player back inside and stings),
    /// for the owner to pick. Removed when the Titan dies.
    /// </summary>
    public sealed class BossArena : MonoBehaviour
    {
        public enum WallMode { Solid, Shock }

        public float Radius { get; private set; }
        public WallMode Mode { get; private set; }
        public Vector3 Centre => transform.position;

        public const float ShockDamage = 8f, ShockPush = 3f, SegmentLength = 2.4f;
        readonly List<GameObject> _walls = new();
        float _nextShockAt;

        public static BossArena Build(Vector3 centre, float radius, WallMode mode, GameObject wallPrefab)
        {
            var go = new GameObject("BossArena");
            go.transform.position = new Vector3(centre.x, 0f, centre.z);
            var a = go.AddComponent<BossArena>();
            a.Radius = radius; a.Mode = mode;
            int layer = LayerMask.NameToLayer("NavObstacle");
            int n = Mathf.Max(12, Mathf.CeilToInt(2f * Mathf.PI * radius / SegmentLength));
            for (int i = 0; i < n; i++)
            {
                float ang = i * Mathf.PI * 2f / n;
                Vector3 pos = go.transform.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;
                var rot = Quaternion.LookRotation(new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang)), Vector3.up);
                GameObject w;
                if (wallPrefab != null) w = Instantiate(wallPrefab, pos, rot, go.transform);
                else
                {
                    w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    w.transform.SetParent(go.transform, true);
                    w.transform.SetPositionAndRotation(pos + Vector3.up * 0.6f, rot);
                    w.transform.localScale = new Vector3(0.5f, 1.2f, SegmentLength);
                }
                if (w.GetComponentInChildren<Collider>() == null)
                {
                    var box = w.AddComponent<BoxCollider>();
                    box.size = new Vector3(0.6f, 2f, SegmentLength); box.center = Vector3.up;
                }
                if (layer >= 0) foreach (var t in w.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                if (mode == WallMode.Shock) foreach (var r in w.GetComponentsInChildren<Renderer>()) r.material.color = new Color(1f, 0.45f, 0.2f);
                a._walls.Add(w);
            }
            WorldNav.MapNavigator.MarkDirty(go.transform.position, radius + 4f);
            return a;
        }

        void Update()
        {
            if (Mode != WallMode.Shock) return;
            var p = PlayerMovement.Instance;
            if (p == null || Time.time < _nextShockAt) return;
            Vector3 d = p.transform.position - Centre; d.y = 0f;
            if (d.magnitude < Radius - 1.1f) return;
            _nextShockAt = Time.time + 0.5f;
            var rb = p.GetComponent<Rigidbody>();
            Vector3 back = Centre + d.normalized * (Radius - 1.1f - ShockPush);
            if (rb != null) rb.MovePosition(new Vector3(back.x, p.transform.position.y, back.z));
            p.GetComponentInParent<IDamageable>()?.TakeDamage(ShockDamage);
        }

        public void Remove()
        {
            Vector3 c = Centre; float r = Radius;
            Destroy(gameObject);
            WorldNav.MapNavigator.MarkDirty(c, r + 4f);
        }
    }
}
