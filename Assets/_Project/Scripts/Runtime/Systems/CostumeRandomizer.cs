using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Studio "Random" (owner 2026-09-27: random outfits must not have pieces overlapping each other).
    /// Slots are filled in order (top first, its colour is the anchor), each pick skips any piece
    /// that clips a piece already chosen (table measured by HordeCall/Costume/Measure Clashes),
    /// and colours are weighted toward the anchor: neutrals always fit, then analogous,
    /// complementary and triad hues; a third loud colour is discouraged. Required slots always get
    /// a piece; when every owned one clips something, the optional pieces it clips come off.
    /// </summary>
    public sealed class CostumeRandomizer
    {
        [Serializable] sealed class PartJson { public string id, slot, color; }
        [Serializable] sealed class ClashJson { public string a, b; public float s; }
        [Serializable] sealed class FileJson { public float voxel; public PartJson[] parts; public ClashJson[] clash; }

        readonly Dictionary<string, Color> _colors = new();
        readonly HashSet<(string, string)> _clash = new();

        /// Chance an optional slot is left empty, by slot (others use DefaultEmpty).
        static readonly Dictionary<string, float> EmptyChance = new()
        {
            { "Head", 0.4f }, { "Back", 0.5f }, { "Eyewear", 0.7f }, { "Mask", 0.85f }, { "Beard", 0.8f },
            { "Earring", 0.75f }, { "HairAccessory", 0.75f }, { "Hands", 0.7f }, { "Bracelet", 0.8f },
            { "Watch", 0.8f }, { "HandAccessory", 0.85f },
        };
        const float DefaultEmpty = 0.5f;

        /// Fill order: the top sets the palette, then what is most visible.
        static readonly string[] Order = { "Chest", "Legs", "Feet", "Head", "Hair", "Back", "Hands", "Eyewear", "Mask", "Beard", "HairAccessory", "Earring", "Watch", "Bracelet", "HandAccessory" };

        public int ClashPairs => _clash.Count;

        public static CostumeRandomizer Load()
        {
            var r = new CostumeRandomizer();
            var ta = Resources.Load<TextAsset>("Character/CostumeCompat");
            if (ta != null) r.Read(ta.text);
            return r;
        }

        public void Read(string json)
        {
            var f = JsonUtility.FromJson<FileJson>(json);
            if (f?.parts != null) foreach (var p in f.parts) if (ColorUtility.TryParseHtmlString("#" + p.color, out var c)) _colors[p.id] = c;
            if (f?.clash != null) foreach (var c in f.clash) { _clash.Add((c.a, c.b)); _clash.Add((c.b, c.a)); }
        }

        /// Test seam: declare two pieces clashing / give a piece a colour.
        public void AddClash(string a, string b) { _clash.Add((a, b)); _clash.Add((b, a)); }
        public void SetColor(string id, Color c) => _colors[id] = c;

        public bool Clashes(string a, string b) => _clash.Contains((a, b));

        /// <summary>Counts clashing pairs inside an outfit (for audits and tests).</summary>
        public int CountClashes(IReadOnlyList<LoadoutState.PartSel> outfit)
        {
            int n = 0;
            for (int i = 0; i < outfit.Count; i++) for (int j = i + 1; j < outfit.Count; j++) if (Clashes(outfit[i].guid, outfit[j].guid)) n++;
            return n;
        }

        public List<LoadoutState.PartSel> Generate(ModularCostumeCatalog catalog, Func<string, bool> owned, System.Random rng)
        {
            var outfit = new List<LoadoutState.PartSel>();
            if (catalog == null) return outfit;
            var chosen = new List<string>();
            var requiredSlots = new HashSet<string>();
            var loud = new List<float>();          // hues of saturated pieces so far
            float anchorHue = -1f;

            // Ordered slots first, then any other slot the catalog has.
            var slots = new List<ModularCostumeCatalog.SlotDefinition>();
            foreach (var id in Order) { var d = catalog.GetSlotDefinition(id); if (d != null) slots.Add(d); }
            foreach (var d in catalog.slotDefinitions) if (!slots.Contains(d)) slots.Add(d);

            foreach (var def in slots)
            {
                if (catalog.IsTechnicalCasualSlot(def.id)) continue;
                var slot = catalog.GetSlot(def.id);
                if (slot == null) continue;
                var pool = new List<string>();
                foreach (var p in slot.parts) if (!string.IsNullOrEmpty(p.itemId) && owned(p.itemId)) pool.Add(p.itemId);
                if (pool.Count == 0) continue;
                bool required = def.required || !def.allowNone;
                if (!required && rng.NextDouble() < (EmptyChance.TryGetValue(def.id, out var e) ? e : DefaultEmpty)) continue;

                var fits = pool.FindAll(id => { foreach (var c in chosen) if (Clashes(id, c)) return false; return true; });
                bool forced = false;
                if (fits.Count == 0) { if (!required) continue; fits = pool; forced = true; }

                string pick = PickWeighted(fits, anchorHue, loud, rng);
                if (forced)
                {
                    // A required piece has to go on: take off the optional pieces it clips instead.
                    for (int k = outfit.Count - 1; k >= 0; k--)
                        if (!requiredSlots.Contains(outfit[k].slot) && Clashes(pick, outfit[k].guid)) { chosen.Remove(outfit[k].guid); outfit.RemoveAt(k); }
                }
                chosen.Add(pick);
                outfit.Add(new LoadoutState.PartSel { slot = def.id, guid = pick });
                if (required) requiredSlots.Add(def.id);
                if (_colors.TryGetValue(pick, out var col) && !Neutral(col))
                {
                    Color.RGBToHSV(col, out float h, out _, out _);
                    if (anchorHue < 0f) anchorHue = h;
                    loud.Add(h);
                }
            }
            return outfit;
        }

        string PickWeighted(List<string> ids, float anchorHue, List<float> loud, System.Random rng)
        {
            if (anchorHue < 0f) return ids[rng.Next(ids.Count)];
            var w = new float[ids.Count]; float total = 0f;
            for (int i = 0; i < ids.Count; i++) { w[i] = Harmony(ids[i], anchorHue, loud); total += w[i]; }
            double r = rng.NextDouble() * total;
            for (int i = 0; i < ids.Count; i++) { r -= w[i]; if (r <= 0) return ids[i]; }
            return ids[ids.Count - 1];
        }

        /// Colour weight against the anchor hue: neutral 1, analogous 1, complementary 0.9,
        /// triad 0.7, clashing hue 0.2; a new loud hue when two are already worn x0.35.
        float Harmony(string id, float anchorHue, List<float> loud)
        {
            if (!_colors.TryGetValue(id, out var c) || Neutral(c)) return 1f;
            Color.RGBToHSV(c, out float h, out _, out _);
            float d = HueDistance(h, anchorHue) * 360f;
            float w = d < 35f ? 1f : (d > 150f ? 0.9f : (d > 105f && d < 135f ? 0.7f : 0.2f));
            bool newHue = true; foreach (var lh in loud) if (HueDistance(h, lh) * 360f < 30f) { newHue = false; break; }
            if (newHue && loud.Count >= 2) w *= 0.35f;
            return w;
        }

        static float HueDistance(float a, float b) { float d = Mathf.Abs(a - b); return Mathf.Min(d, 1f - d); }

        /// Greys, near-blacks, near-whites and dull browns go with anything.
        public static bool Neutral(Color c)
        {
            Color.RGBToHSV(c, out _, out float s, out float v);
            return s < 0.22f || v < 0.2f || (v > 0.9f && s < 0.3f);
        }
    }
}
