using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Studio "Random", built the way a stylist would (owner 2026-09-28):
    /// 1. pick a gender; 2. sometimes start from a designed look (a costume set of that gender);
    /// 3. decide the head: bare, a hat, or a mask; 4. that decision rules the rest: under a hat only
    /// compact hair, under a hood/helmet nothing else on the head, a mask means no beard and no
    /// glasses, beards only for men, hair accessories only for women with no hat;
    /// 5. clothes and the rest of the face follow the gender; 6. colours are weighted toward the
    /// top's colour (neutral, analogous, complementary, triad); 7. last safety net: never two pieces
    /// that clip (measured table, HordeCall/Costume/Measure Clashes).
    /// </summary>
    public sealed class CostumeRandomizer
    {
        [Serializable] sealed class PartJson { public string id, slot, color, g, cover; public int n; }
        [Serializable] sealed class ClashJson { public string a, b; public float s; }
        [Serializable] sealed class SetJson { public string id, g; }
        [Serializable] sealed class FileJson { public float voxel; public PartJson[] parts; public ClashJson[] clash; public SetJson[] sets; }

        readonly Dictionary<string, Color> _colors = new();
        readonly Dictionary<string, char> _gender = new();
        readonly Dictionary<string, int> _size = new();
        readonly HashSet<string> _fullCover = new();
        readonly Dictionary<string, char> _setGender = new();
        readonly HashSet<(string, string)> _clash = new();
        float _hairCompact = float.MaxValue, _hairTiny = float.MaxValue;

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
            var hair = new List<int>();
            if (f?.parts != null)
                foreach (var p in f.parts)
                {
                    if (!string.IsNullOrEmpty(p.color) && ColorUtility.TryParseHtmlString("#" + p.color, out var c)) _colors[p.id] = c;
                    if (!string.IsNullOrEmpty(p.g)) _gender[p.id] = p.g[0];
                    if (p.n > 0) _size[p.id] = p.n;
                    if (p.cover == "full") _fullCover.Add(p.id);
                    if (p.slot == "Hair" && p.n > 0) hair.Add(p.n);
                }
            if (hair.Count > 0)
            {
                hair.Sort();
                // Unisex hair (in no preset) that is big reads as a girl's style: women only.
                float big = hair[Mathf.Clamp(Mathf.RoundToInt(hair.Count * 0.6f), 0, hair.Count - 1)];
                foreach (var p in f.parts) if (p.slot == "Hair" && p.n > big && GenderOf(p.id) == 'U') _gender[p.id] = 'F';
                _hairCompact = hair[Mathf.Clamp(Mathf.RoundToInt(hair.Count * 0.45f), 0, hair.Count - 1)];
                _hairTiny = hair[Mathf.Clamp(Mathf.RoundToInt(hair.Count * 0.25f), 0, hair.Count - 1)];
            }
            if (f?.sets != null) foreach (var s in f.sets) if (!string.IsNullOrEmpty(s.g)) _setGender[s.id] = s.g[0];
            if (f?.clash != null) foreach (var c in f.clash) AddClash(c.a, c.b);
        }

        // ---- test seams
        public void AddClash(string a, string b) { _clash.Add((a, b)); _clash.Add((b, a)); }
        public void SetColor(string id, Color c) => _colors[id] = c;
        public void SetGender(string id, char g) => _gender[id] = g;
        public void SetSize(string id, int cells) => _size[id] = cells;
        public void SetFullCover(string id) => _fullCover.Add(id);
        public void SetHairLimits(int compact, int tiny) { _hairCompact = compact; _hairTiny = tiny; }

        public bool Clashes(string a, string b) => _clash.Contains((a, b));
        public char GenderOf(string id) => _gender.TryGetValue(id, out var g) ? g : 'U';
        public bool IsFullCover(string id) => _fullCover.Contains(id);

        public int CountClashes(IReadOnlyList<LoadoutState.PartSel> outfit)
        {
            int n = 0;
            for (int i = 0; i < outfit.Count; i++) for (int j = i + 1; j < outfit.Count; j++) if (Clashes(outfit[i].guid, outfit[j].guid)) n++;
            return n;
        }

        enum Head { Bare, Hat, Mask }

        sealed class Build
        {
            public char gender; public Head head; public bool fullCover;
            public readonly List<LoadoutState.PartSel> outfit = new();
            public readonly HashSet<string> required = new();
            public float anchorHue = -1f;
            public readonly List<float> loud = new();
            public bool Has(string slot) => outfit.Exists(p => p.slot == slot);
        }

        /// <summary>
        /// A styled random outfit from owned pieces. <paramref name="sets"/> (optional) are the
        /// designed looks; one of the right gender is used as the base about a third of the time.
        /// </summary>
        public List<LoadoutState.PartSel> Generate(ModularCostumeCatalog catalog, Func<string, bool> owned, System.Random rng,
                                                   IReadOnlyList<EconomyConfig.CostumeSetEntry> sets = null)
        {
            var b = new Build { gender = rng.NextDouble() < 0.5 ? 'M' : 'F' };
            if (catalog == null) return b.outfit;

            // 2. a designed look as the base (only its owned pieces; the rules fill the gaps)
            if (sets != null && rng.NextDouble() < 0.3)
            {
                var fits = new List<EconomyConfig.CostumeSetEntry>();
                foreach (var s in sets)
                {
                    if (s?.itemIds == null) continue;
                    char g = _setGender.TryGetValue(s.setId, out var sg) ? sg : 'U';
                    if (g != 'U' && g != b.gender) continue;
                    int own = 0; foreach (var id in s.itemIds) if (owned(id)) own++;
                    if (own >= Mathf.Max(3, s.itemIds.Count * 0.6f)) fits.Add(s);
                }
                if (fits.Count > 0)
                {
                    var set = fits[rng.Next(fits.Count)];
                    foreach (var id in set.itemIds)
                        if (owned(id) && catalog.TryFindByItemId(id, out var slot, out _) && !catalog.IsTechnicalCasualSlot(slot) && !b.Has(slot))
                        {
                            b.outfit.Add(new LoadoutState.PartSel { slot = slot, guid = id });
                            NoteColour(b, id);
                            if (slot == "Head") { b.head = Head.Hat; b.fullCover = IsFullCover(id); }
                            if (slot == "Mask") b.head = Head.Mask;
                        }
                    FillRequired(catalog, owned, rng, b);
                    return b.outfit;
                }
            }

            // 3. the head decision
            double h = rng.NextDouble();
            b.head = h < 0.45 ? Head.Bare : h < 0.85 ? Head.Hat : Head.Mask;

            // 4-5. slots in order, each with its rule
            Add(catalog, owned, rng, b, "Chest", 1f);
            Add(catalog, owned, rng, b, "Legs", 1f);
            Add(catalog, owned, rng, b, "Feet", 1f);
            if (b.head == Head.Hat)
            {
                // Plain hats three times out of four; hoods and helmets are the special look.
                bool wantFull = rng.NextDouble() < 0.25;
                Add(catalog, owned, rng, b, "Head", 1f, id => IsFullCover(id) == wantFull);
                if (!b.Has("Head")) Add(catalog, owned, rng, b, "Head", 1f);
                var hat = b.outfit.Find(p => p.slot == "Head").guid;
                if (hat == null) b.head = Head.Bare; else b.fullCover = IsFullCover(hat);
            }
            if (b.head == Head.Mask) { Add(catalog, owned, rng, b, "Mask", 1f); if (!b.Has("Mask")) b.head = Head.Bare; }

            float hairLimit = b.head == Head.Hat ? (b.fullCover ? _hairTiny : _hairCompact) : float.MaxValue;
            Add(catalog, owned, rng, b, "Hair", 1f, id => !_size.TryGetValue(id, out var n) || n <= hairLimit);

            bool faceFree = b.head != Head.Mask && !b.fullCover;
            if (b.gender == 'M' && faceFree) Add(catalog, owned, rng, b, "Beard", 0.3f);
            if (faceFree) Add(catalog, owned, rng, b, "Eyewear", 0.2f);
            if (b.gender == 'F' && b.head == Head.Bare) Add(catalog, owned, rng, b, "HairAccessory", 0.4f);
            if (!b.fullCover) Add(catalog, owned, rng, b, "Earring", b.gender == 'F' ? 0.4f : 0.1f);
            Add(catalog, owned, rng, b, "Back", 0.45f);
            Add(catalog, owned, rng, b, "Hands", 0.25f);
            Add(catalog, owned, rng, b, "Watch", 0.15f);
            Add(catalog, owned, rng, b, "Bracelet", b.gender == 'F' ? 0.25f : 0.1f);
            Add(catalog, owned, rng, b, "HandAccessory", 0.1f);
            FillRequired(catalog, owned, rng, b);
            return b.outfit;
        }

        /// Face features and any other required slot the rules did not touch.
        void FillRequired(ModularCostumeCatalog catalog, Func<string, bool> owned, System.Random rng, Build b)
        {
            foreach (var def in catalog.slotDefinitions)
            {
                if (catalog.IsTechnicalCasualSlot(def.id) || b.Has(def.id)) continue;
                bool required = def.required || !def.allowNone;
                if (required) Add(catalog, owned, rng, b, def.id, 1f);
            }
        }

        /// Adds one piece to a slot with the given chance: owned, the build's gender (or unisex),
        /// passing the rule, not clipping what is on (a required piece takes clipping optional ones
        /// off instead), weighted by colour.
        void Add(ModularCostumeCatalog catalog, Func<string, bool> owned, System.Random rng, Build b, string slotId, float chance, Func<string, bool> rule = null)
        {
            if (b.Has(slotId)) return;
            var def = catalog.GetSlotDefinition(slotId);
            var slot = catalog.GetSlot(slotId);
            if (def == null || slot == null) return;
            bool required = def.required || !def.allowNone;
            if (!required && rng.NextDouble() >= chance) return;

            var pool = new List<string>();
            foreach (var p in slot.parts) if (!string.IsNullOrEmpty(p.itemId) && owned(p.itemId)) pool.Add(p.itemId);
            if (pool.Count == 0) return;
            var styled = pool.FindAll(id => { char g = GenderOf(id); return (g == 'U' || g == b.gender) && (rule == null || rule(id)); });
            if (styled.Count == 0) { if (!required) return; styled = pool; }

            var fits = styled.FindAll(id => { foreach (var p in b.outfit) if (Clashes(id, p.guid)) return false; return true; });
            bool forced = false;
            if (fits.Count == 0) { if (!required) return; fits = styled; forced = true; }

            string pick = PickWeighted(fits, b, rng);
            if (forced)
                for (int k = b.outfit.Count - 1; k >= 0; k--)
                    if (!b.required.Contains(b.outfit[k].slot) && Clashes(pick, b.outfit[k].guid)) b.outfit.RemoveAt(k);
            b.outfit.Add(new LoadoutState.PartSel { slot = slotId, guid = pick });
            if (required) b.required.Add(slotId);
            NoteColour(b, pick);
        }

        void NoteColour(Build b, string id)
        {
            if (!_colors.TryGetValue(id, out var col) || Neutral(col)) return;
            Color.RGBToHSV(col, out float h, out _, out _);
            if (b.anchorHue < 0f) b.anchorHue = h;
            b.loud.Add(h);
        }

        string PickWeighted(List<string> ids, Build b, System.Random rng)
        {
            if (b.anchorHue < 0f) return ids[rng.Next(ids.Count)];
            var w = new float[ids.Count]; float total = 0f;
            for (int i = 0; i < ids.Count; i++) { w[i] = Harmony(ids[i], b.anchorHue, b.loud); total += w[i]; }
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

        /// Greys, near-blacks, near-whites and dull colours go with anything.
        public static bool Neutral(Color c)
        {
            Color.RGBToHSV(c, out _, out float s, out float v);
            return s < 0.22f || v < 0.2f || (v > 0.9f && s < 0.3f);
        }
    }
}
