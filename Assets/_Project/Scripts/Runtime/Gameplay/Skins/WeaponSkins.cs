using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skins
{
    /// <summary>
    /// M8 gun skin sets, season 1 proposal (owner: a skin is one shader look sold as a set, so it
    /// covers every gun with no per-gun art). Each set is a <c>ZombieWar/Weapon/SkinSet</c> style
    /// plus its colours; <see cref="WeaponSkinApplier"/> puts it on a gun. Values are the
    /// proposal the owner approves from screenshots; they move to a ScriptableObject once approved.
    /// </summary>
    public static class WeaponSkins
    {
        public sealed class Set
        {
            public string id, name, theme;
            public int style;
            public Color a, b, c;   // base, second, glow (HDR)
            public Color r0, r1, r2;   // palette the gun's own parts are mapped onto (dark, mid, light)
            public Color edgeColor = Color.white;
            public float metal = 0.2f, edge = 1f;
            // Toon metal (stepped specular/highlight/rim); off unless set.
            public bool toonMetal;
            public float specCut = 0.82f, hiOffset = 0.93f, rimCut = 0.72f;
            public Color specColor = new(1f, 0.85f, 0.3f), hiColor = new(1f, 0.98f, 0.8f, 0.6f), rimColor = new(0.9f, 0.6f, 0.15f, 1f);
            public float scale, speed = 1f, detail = 0.5f, glow = 1f, spec = 0.4f, gloss = 48f, rimPower = 3f;
        }

        public const string ShaderName = "ZombieWar/Weapon/SkinSet";

        public static readonly Set[] Season1 =
        {
            new() { id = "inferno",  name = "Inferno",      theme = "Charcoal shell split by crawling lava",      style = 0,
                    a = new Color(0.07f, 0.055f, 0.05f), b = new Color(0.38f, 0.07f, 0.03f), c = new Color(2.6f, 0.8f, 0.12f), scale = 9f, detail = 0.5f, r0 = new Color(0.03f, 0.025f, 0.025f), r1 = new Color(0.13f, 0.085f, 0.07f), r2 = new Color(0.3f, 0.19f, 0.14f), metal = 0.15f, edge = 1.2f, edgeColor = new Color(1.4f, 0.5f, 0.15f) },
            new() { id = "cosmos",   name = "Cosmos",       theme = "Night-violet body, drifting nebula, stars",   style = 1,
                    a = new Color(0.08f, 0.07f, 0.2f), b = new Color(0.36f, 0.13f, 0.52f), c = new Color(1.8f, 1.5f, 2.8f), scale = 10f, spec = 0.6f, gloss = 80f, r0 = new Color(0.03f, 0.02f, 0.08f), r1 = new Color(0.14f, 0.08f, 0.3f), r2 = new Color(0.45f, 0.3f, 0.75f), metal = 0.35f, edge = 0.9f, edgeColor = new Color(0.6f, 0.7f, 1.4f) },
            new() { id = "frostbite", name = "Frostbite",   theme = "Cracked ice crystals with a frost rim",       style = 2,
                    a = new Color(0.32f, 0.55f, 0.72f), b = new Color(0.72f, 0.88f, 0.97f), c = new Color(0.9f, 1.4f, 2.0f), scale = 9f, spec = 1.1f, gloss = 120f, detail = 0.5f, r0 = new Color(0.22f, 0.42f, 0.6f), r1 = new Color(0.55f, 0.75f, 0.9f), r2 = new Color(0.9f, 0.97f, 1f), metal = 0.5f, edge = 1.3f, edgeColor = new Color(1.1f, 1.2f, 1.3f) },
            new() { id = "biohazard", name = "Biohazard",   theme = "Olive gunmetal dripping pulsing acid",        style = 3,
                    a = new Color(0.12f, 0.14f, 0.07f), b = new Color(0.25f, 0.55f, 0.08f), c = new Color(1.1f, 3.2f, 0.35f), scale = 9f, detail = 0.5f, r0 = new Color(0.05f, 0.06f, 0.03f), r1 = new Color(0.16f, 0.19f, 0.09f), r2 = new Color(0.36f, 0.42f, 0.2f), metal = 0.3f, edge = 0.8f, edgeColor = new Color(0.6f, 1.4f, 0.3f) },
            new() { id = "neon",     name = "Neon Circuit", theme = "Matte black, cyan and magenta traces",       style = 4,
                    a = new Color(0.035f, 0.04f, 0.05f), b = new Color(0.035f, 0.04f, 0.05f), c = new Color(0.25f, 2.6f, 3.2f), scale = 12f, spec = 0.7f, gloss = 90f, detail = 0.5f, r0 = new Color(0.02f, 0.02f, 0.03f), r1 = new Color(0.07f, 0.075f, 0.09f), r2 = new Color(0.2f, 0.21f, 0.24f), metal = 0.35f, edge = 1.2f, edgeColor = new Color(0.3f, 1.4f, 1.8f) },
            new() { id = "gilded",   name = "Gilded",       theme = "Polished gold with engraved scrollwork",     style = 5,
                    a = new Color(0.95f, 0.68f, 0.24f), b = new Color(0.45f, 0.28f, 0.08f), c = new Color(1.8f, 1.35f, 0.6f), scale = 11f, spec = 1.4f, gloss = 96f, detail = 0.5f, r0 = new Color(0.62f, 0.28f, 0.04f), r1 = new Color(0.93f, 0.52f, 0.08f), r2 = new Color(1f, 0.66f, 0.16f), metal = 0f, toonMetal = true, glow = 0f,
                    specCut = 0.9f, specColor = new Color(1f, 0.84f, 0.26f), hiOffset = 0.93f, hiColor = new Color(1f, 0.98f, 0.82f, 0.7f),
                    rimCut = 0.7f, rimColor = new Color(1f, 0.72f, 0.25f, 0.55f), edge = 1.4f, edgeColor = new Color(1.6f, 1.4f, 0.9f) },
        };

        static Shader _shader;
        static readonly Dictionary<string, Material> Cache = new();

        /// <summary>One shared material per (set, gun texture). Null when the shader is missing.</summary>
        public static Material MaterialFor(Set set, Texture baseMap)
        {
            string key = set.id + "|" + (baseMap != null ? baseMap.GetInstanceID() : 0);
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            if (_shader == null) _shader = Shader.Find(ShaderName);
            if (_shader == null) return null;
            m = new Material(_shader) { name = $"Skin_{set.id}", hideFlags = HideFlags.DontSave };
            m.SetFloat("_Style", set.style);
            m.SetColor("_ColorA", set.a);
            m.SetColor("_ColorB", set.b);
            m.SetColor("_ColorC", set.c);
            m.SetFloat("_Scale", set.scale);
            m.SetFloat("_Speed", set.speed);
            m.SetFloat("_Detail", set.detail);
            m.SetFloat("_Glow", set.glow);
            m.SetFloat("_Spec", set.spec);
            m.SetFloat("_Gloss", set.gloss);
            m.SetFloat("_RimPower", set.rimPower);
            m.SetColor("_Ramp0", set.r0);
            m.SetColor("_Ramp1", set.r1);
            m.SetColor("_Ramp2", set.r2);
            m.SetFloat("_Metal", set.metal);
            m.SetFloat("_Edge", set.edge);
            m.SetColor("_EdgeColor", set.edgeColor);
            m.SetFloat("_ToonMetal", set.toonMetal ? 1f : 0f);
            m.SetFloat("_SpecCut", set.specCut);
            m.SetFloat("_HiOffset", set.hiOffset);
            m.SetFloat("_RimCut", set.rimCut);
            m.SetColor("_SpecColor2", set.specColor);
            m.SetColor("_HiColor", set.hiColor);
            m.SetColor("_RimColor", set.rimColor);
            if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
            Cache[key] = m;
            return m;
        }

        public static Texture BaseMapOf(Material m)
        {
            if (m == null) return null;
            if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) return m.GetTexture("_BaseMap");
            if (m.HasProperty("_MainTex")) return m.GetTexture("_MainTex");
            return null;
        }
    }

    /// <summary>
    /// Puts a skin set on every renderer under this gun and keeps the pattern locked to the gun:
    /// each frame it hands the shader the gun root's matrix and length, so the pattern runs
    /// continuously across separate parts and does not swim as the gun moves.
    /// </summary>
    public sealed class WeaponSkinApplier : MonoBehaviour
    {
        Renderer[] _renderers;
        Material[][] _original;
        MaterialPropertyBlock _mpb;
        float _size = 1f;
        public WeaponSkins.Set Current { get; private set; }

        static readonly int RootId = Shader.PropertyToID("_SkinRootW2L");
        static readonly int SizeId = Shader.PropertyToID("_SkinSize");

        public void Apply(WeaponSkins.Set set)
        {
            Collect();
            Current = set;
            for (int r = 0; r < _renderers.Length; r++)
            {
                var src = _original[r];
                var mats = new Material[src.Length];
                for (int k = 0; k < src.Length; k++)
                    mats[k] = set != null ? WeaponSkins.MaterialFor(set, WeaponSkins.BaseMapOf(src[k])) ?? src[k] : src[k];
                _renderers[r].sharedMaterials = mats;
            }
            Push();
        }

        void Collect()
        {
            if (_renderers != null) return;
            _renderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer));
            _original = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++) _original[i] = _renderers[i].sharedMaterials;
            // Gun length in root space: the longest side of all parts' bounds.
            var inv = transform.worldToLocalMatrix;
            Bounds b = default; bool any = false;
            foreach (var r in _renderers)
            {
                var wb = r.bounds;
                var c = inv.MultiplyPoint3x4(wb.center);
                if (!any) { b = new Bounds(c, Vector3.zero); any = true; }
                b.Encapsulate(inv.MultiplyPoint3x4(wb.min));
                b.Encapsulate(inv.MultiplyPoint3x4(wb.max));
            }
            _size = any ? Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z))) : 1f;
        }

        void LateUpdate() => Push();

        void Push()
        {
            if (_renderers == null || Current == null) return;
            _mpb ??= new MaterialPropertyBlock();
            var w2l = transform.worldToLocalMatrix;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetMatrix(RootId, w2l);
                _mpb.SetFloat(SizeId, _size);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
