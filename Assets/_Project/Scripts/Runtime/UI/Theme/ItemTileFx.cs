using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Rarity look for an item tile (ItemTileFx shader): stripes for every tile, a rim glow in the
    /// rarity colour from Rare, a sweeping sheen from Epic. One shared material per rarity and
    /// aspect bucket, so tiles still batch. Screens call <see cref="SetRarity"/> when they fill a tile.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public sealed class ItemTileFx : BaseMeshEffect
    {
        static readonly Dictionary<int, Material> Cache = new();
        static Shader _shader;
        static readonly int Glow = Shader.PropertyToID("_Glow"), GlowColor = Shader.PropertyToID("_GlowColor"),
            Sheen = Shader.PropertyToID("_Sheen"), Stripes = Shader.PropertyToID("_Stripes"), Aspect = Shader.PropertyToID("_Aspect");

        [SerializeField, Range(-1, 4)] private int rarity = -1;

        static ItemTileFx() => ThemeService.Changed += () => { foreach (var m in Cache.Values) if (m != null) Object.Destroy(m); Cache.Clear(); };

        /// <summary>Adds the effect to a tile if missing and sets its rarity (-1 = plain).</summary>
        public static void On(Graphic g, int tier)
        {
            if (g == null) return;
            if (!g.TryGetComponent(out ItemTileFx fx)) { if (tier < 0) return; fx = g.gameObject.AddComponent<ItemTileFx>(); }
            fx.SetRarity(tier);
        }

        /// <summary>-1 = plain tile (no effect), 0..4 = Common..Legendary.</summary>
        public void SetRarity(int tier)
        {
            rarity = Mathf.Clamp(tier, -1, 4);
            Apply();
        }

        protected override void OnEnable() { base.OnEnable(); ThemeService.Changed += Apply; Apply(); }
        protected override void OnDisable() { ThemeService.Changed -= Apply; base.OnDisable(); }
        protected override void OnRectTransformDimensionsChange() { base.OnRectTransformDimensionsChange(); Apply(); }

        void Apply()
        {
            if (graphic == null) return;
            if (rarity < 0) { graphic.material = null; return; }
            var r = ((RectTransform)transform).rect;
            int aspect = r.height > 1f ? Mathf.Clamp(Mathf.RoundToInt(r.width / r.height * 4f), 1, 16) : 4;   // quarter steps
            graphic.material = Get(rarity, aspect);
        }

        static Material Get(int tier, int aspect4)
        {
            int key = tier * 100 + aspect4;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            if (_shader == null) _shader = Shader.Find("ZombieWar/UI/ItemTileFx");
            if (_shader == null) return null;
            m = new Material(_shader) { name = $"ItemTileFx r{tier} a{aspect4}", hideFlags = HideFlags.DontSave };
            var rc = ThemeService.Current != null ? ThemeService.Get(ThemePalette.Rarity(tier)) : Color.white;
            m.SetColor(GlowColor, Color.Lerp(rc, Color.white, 0.6f));
            m.SetFloat(Glow, tier >= 2 ? 0.8f + 0.25f * (tier - 2) : 0f);
            m.SetFloat(Sheen, tier >= 3 ? (tier == 4 ? 1f : 0.7f) : 0f);
            m.SetFloat(Stripes, ThemeService.Current != null && ThemeService.Current.dark ? 0.02f : 0.035f);   // additive: reads stronger on dark tiles
            m.SetFloat(Aspect, aspect4 / 4f);
            Cache[key] = m;
            return m;
        }

        /// <summary>Writes the tile's 0..1 rect position into uv1 for the shader.</summary>
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var r = ((RectTransform)transform).rect;
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.uv1 = new Vector4((v.position.x - r.xMin) / Mathf.Max(r.width, 1f), (v.position.y - r.yMin) / Mathf.Max(r.height, 1f), 0, 0);
                vh.SetUIVertex(v, i);
            }
        }
    }
}
