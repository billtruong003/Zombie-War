using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Spinning blade visuals shared by Orbit Blades and Boomerang: the built-in saw disc,
    /// an optional mesh override, and the flat trails that lie in the blade's plane.</summary>
    public static class BladeVisuals
    {
        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>A blade holder under <paramref name="root"/>, inactive. With <paramref name="trailTime"/>
        /// above zero it carries a flat trail (owner 2026-09-29: a camera-facing ribbon read as a tilted fin).</summary>
        public static Transform Make(Transform root, string name, GameObject meshModel, Material sawMaterial,
                                     Material trailMaterial, float scale, Color trailColor, float trailTime, Color tint,
                                     out TrailRenderer trail)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(root, false);
            if (meshModel == null)
            {
                var disc = new GameObject("saw");
                disc.transform.SetParent(holder, false);
                disc.transform.localScale = Vector3.one * scale;
                disc.AddComponent<MeshFilter>().sharedMesh = SawMesh();
                var mr = disc.AddComponent<MeshRenderer>();
                mr.sharedMaterial = sawMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (tint != Color.white)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor(BaseColorId, tint);
                    mr.SetPropertyBlock(block);
                }
            }
            else
            {
                var model = Object.Instantiate(meshModel, holder);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat, edge outward
                model.transform.localScale = Vector3.one * scale;
                foreach (var col in model.GetComponentsInChildren<Collider>()) Object.Destroy(col);
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
            trail = null;
            if (trailTime > 0f)
            {
                var flat = new GameObject("trail").transform;
                flat.SetParent(holder, false);
                flat.localRotation = Quaternion.Euler(90f, 0f, 0f);   // ribbon faces up: the trail material is one-sided
                flat.localPosition = Vector3.up * 0.06f;               // just above the blade, which would hide it
                trail = flat.gameObject.AddComponent<TrailRenderer>();
                trail.alignment = LineAlignment.TransformZ;
                trail.time = trailTime;
                trail.minVertexDistance = 0.08f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.15f * scale), new Keyframe(1f, 0f));
                trail.startColor = trailColor;
                trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                if (trailMaterial != null) trail.sharedMaterial = trailMaterial;
            }
            holder.gameObject.SetActive(false);
            return holder;
        }

        /// <summary>A thin white trail on the blade's tip (the mesh point farthest from its centre),
        /// so the spin draws a loop around the flight path. Flat, like the saws' trails.</summary>
        public static TrailRenderer TipTrail(Transform holder, float scale, Material trailMaterial)
        {
            Vector3 tip = new(0.5f * scale, 0f, 0f);
            var mf = holder.GetComponentInChildren<MeshFilter>(true);
            if (mf != null && mf.sharedMesh != null)
            {
                float best = 0f;
                foreach (var v in mf.sharedMesh.vertices)
                    if (v.sqrMagnitude > best) { best = v.sqrMagnitude; tip = holder.InverseTransformPoint(mf.transform.TransformPoint(v)); }
                tip.y = 0f;
            }
            var go = new GameObject("tipTrail").transform;
            go.SetParent(holder, false);
            go.localPosition = tip + Vector3.up * 0.04f;
            go.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tr = go.gameObject.AddComponent<TrailRenderer>();
            tr.alignment = LineAlignment.TransformZ;
            tr.time = 0.3f;
            tr.minVertexDistance = 0.05f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.07f * scale), new Keyframe(1f, 0f));
            tr.startColor = new Color(1f, 1f, 1f, 0.9f);
            tr.endColor = new Color(1f, 1f, 1f, 0f);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            if (trailMaterial != null) tr.sharedMaterial = trailMaterial;
            return tr;
        }

        static Mesh _saw;

        /// <summary>
        /// A flat eight-tooth saw disc (radius 0.5, in the ground plane), vertex-coloured: dark hub,
        /// steel body, bright swept teeth. Built once and shared. Double-sided so it reads from any tilt.
        /// </summary>
        public static Mesh SawMesh()
        {
            if (_saw != null) return _saw;
            const int teeth = 8;
            const float hub = 0.16f, valley = 0.36f, tip = 0.5f;
            var hubCol = new Color(0.30f, 0.33f, 0.40f);
            var bodyCol = new Color(0.78f, 0.84f, 0.92f);
            var tipCol = Color.white;

            var v = new List<Vector3> { Vector3.zero };
            var c = new List<Color> { hubCol };
            int ring = teeth * 2;
            for (int i = 0; i < ring; i++)
            {
                float a = i * Mathf.PI * 2f / ring;
                v.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * hub); c.Add(hubCol);
            }
            for (int i = 0; i < ring; i++)
            {
                // Even = valley, odd = tooth tip swept forward, so it reads as a saw that spins.
                bool isTip = (i & 1) == 1;
                float a = (i + (isTip ? 0.6f : 0f)) * Mathf.PI * 2f / ring;
                float r = isTip ? tip : valley;
                v.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r); c.Add(isTip ? tipCol : bodyCol);
            }

            var t = new List<int>();
            for (int i = 0; i < ring; i++)
            {
                int h0 = 1 + i, h1 = 1 + (i + 1) % ring;
                int o0 = 1 + ring + i, o1 = 1 + ring + (i + 1) % ring;
                t.AddRange(new[] { 0, h1, h0 });
                t.AddRange(new[] { h0, h1, o1, h0, o1, o0 });
            }
            int single = t.Count;
            for (int i = 0; i < single; i += 3) t.AddRange(new[] { t[i], t[i + 2], t[i + 1] });   // back faces

            _saw = new Mesh { name = "SkillSawDisc" };
            _saw.SetVertices(v);
            _saw.SetColors(c);
            _saw.SetTriangles(t, 0);
            _saw.RecalculateBounds();
            _saw.RecalculateNormals();
            return _saw;
        }
    }
}
