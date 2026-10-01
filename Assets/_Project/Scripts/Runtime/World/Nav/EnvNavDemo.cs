using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.WorldNav
{
    /// <summary>
    /// Env Sandbox only (2026-10-01): a crowd of stand-in enemies crossing a patch of map tiles
    /// toward one point, steered by a <see cref="FlowField"/>, so the pathing around basins and
    /// over bridges can be seen before it touches the game's enemies. Not used in any game scene.
    /// </summary>
    public sealed class EnvNavDemo : MonoBehaviour
    {
        public Vector3 spawnCentre = new(0f, 0f, -42f);   // local
        public float spawnHalfWidth = 40f;
        public Vector3 target = new(0f, 0f, 40f);         // local
        public float window = 104f;
        public int count = 120;
        public float speed = 3.2f;
        public float resolveInterval = 0.25f;

        FlowField _field;
        readonly List<Transform> _agents = new();
        readonly List<Vector3> _vel = new();
        float _nextSolve;
        Transform _marker;

        void Start()
        {
            _field = new FlowField(window, 0.5f, 1 << LayerMask.NameToLayer("NavObstacle"));
            _field.Rasterize(transform.position);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.9f, 0.15f, 0.2f) };
            for (int i = 0; i < count; i++)
            {
                var a = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(a.GetComponent<Collider>());
                a.name = "Agent";
                a.transform.SetParent(transform, false);
                a.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                a.GetComponent<MeshRenderer>().sharedMaterial = mat;
                _agents.Add(a.transform);
                _vel.Add(Vector3.zero);
            }
            var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(m.GetComponent<Collider>());
            m.name = "Target";
            m.transform.SetParent(transform, false);
            m.transform.localPosition = target;
            m.transform.localScale = new Vector3(2.5f, 3f, 2.5f);
            m.GetComponent<MeshRenderer>().sharedMaterial = new Material(mat) { color = new Color(0.2f, 0.55f, 1f) };
            _marker = m.transform;
            Restart();
        }

        /// Puts every agent back on the spawn line (the capture tool calls this before its timed shots).
        public void Restart()
        {
            var rng = new System.Random(5);
            for (int i = 0; i < _agents.Count; i++)
            {
                Vector3 p;
                int tries = 0;
                do
                {
                    p = transform.TransformPoint(spawnCentre + new Vector3((float)(rng.NextDouble() * 2 - 1) * spawnHalfWidth, 0f, (float)(rng.NextDouble() * 2 - 1) * 3f));
                } while (_field.IsBlocked(p) && ++tries < 20);
                _agents[i].position = p + Vector3.up * 0.9f;
                _vel[i] = Vector3.zero;
            }
            _nextSolve = 0f;
        }

        void Update()
        {
            if (_field == null) return;
            Vector3 goal = _marker.position;
            if (Time.time >= _nextSolve)
            {
                _field.Solve(goal);
                _nextSolve = Time.time + resolveInterval;
            }
            float dt = Time.deltaTime;
            for (int i = 0; i < _agents.Count; i++)
            {
                Vector3 p = _agents[i].position; p.y = 0f;
                Vector3 toGoal = goal - p; toGoal.y = 0f;
                if (toGoal.magnitude < 2.5f) continue;
                Vector3 dir = _field.Direction(p);
                if (dir == Vector3.zero) dir = toGoal.normalized;
                // Light separation so the crowd reads as a crowd, not a single file.
                Vector3 sep = Vector3.zero;
                for (int k = 0; k < _agents.Count; k++)
                {
                    if (k == i) continue;
                    Vector3 d = p - _agents[k].position; d.y = 0f;
                    float m2 = d.sqrMagnitude;
                    if (m2 > 0.0001f && m2 < 0.64f) sep += d / m2;
                }
                Vector3 desired = (dir + sep * 0.08f).normalized * speed;
                _vel[i] = Vector3.Lerp(_vel[i], desired, 1f - Mathf.Exp(-8f * dt));
                Vector3 next = p + _vel[i] * dt;
                // The obstacle is solid: a step into a blocked cell slides along its free axis.
                if (_field.IsBlocked(next))
                {
                    var nx = new Vector3(next.x, 0f, p.z);
                    var nz = new Vector3(p.x, 0f, next.z);
                    next = !_field.IsBlocked(nx) ? nx : !_field.IsBlocked(nz) ? nz : p;
                }
                _agents[i].position = next + Vector3.up * 0.9f;
                ZombieWar.World.GrassBenders.Submit(next, 0.8f);
            }
        }
    }
}
