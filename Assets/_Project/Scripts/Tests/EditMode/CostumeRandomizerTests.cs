using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// Owner 2026-09-27: Random must not pick pieces that clip each other.
    public class CostumeRandomizerTests
    {
        static ModularCostumeCatalog Catalog()
        {
            var c = ScriptableObject.CreateInstance<ModularCostumeCatalog>();
            void Slot(string id, bool required, params string[] items)
            {
                var s = new ModularCostumeCatalog.Slot { slot = id };
                foreach (var it in items) s.parts.Add(new ModularCostumeCatalog.PartEntry { name = it, itemId = it });
                c.slots.Add(s);
                c.slotDefinitions.Add(new ModularCostumeCatalog.SlotDefinition { id = id, required = required, allowNone = !required });
            }
            Slot("Chest", true, "top.red", "top.blue");
            Slot("Hair", true, "hair.a", "hair.b", "hair.c");
            Slot("Head", false, "hat.helmet", "hat.cap");
            Slot("Eyewear", false, "glass.goggles", "glass.round");
            return c;
        }

        [Test]
        public void Generate_NeverPicksClashingPieces()
        {
            var cat = Catalog();
            var r = new CostumeRandomizer();
            // The helmet clips every hair but c; goggles clip the helmet.
            r.AddClash("hat.helmet", "hair.a"); r.AddClash("hat.helmet", "hair.b");
            r.AddClash("glass.goggles", "hat.helmet");
            var rng = new System.Random(7);
            for (int i = 0; i < 500; i++)
            {
                var o = r.Generate(cat, _ => true, rng);
                Assert.AreEqual(0, r.CountClashes(o), "outfit " + i);
                Assert.IsTrue(o.Exists(p => p.slot == "Chest"), "top is required");
                Assert.IsTrue(o.Exists(p => p.slot == "Hair"), "hair is required");
            }
            Object.DestroyImmediate(cat);
        }

        [Test]
        public void Generate_RequiredSlotStillFilled_WhenEverythingClashes()
        {
            var cat = Catalog();
            var r = new CostumeRandomizer();
            foreach (var h in new[] { "hair.a", "hair.b", "hair.c" }) { r.AddClash("top.red", h); r.AddClash("top.blue", h); }
            var o = r.Generate(cat, _ => true, new System.Random(1));
            Assert.IsTrue(o.Exists(p => p.slot == "Hair"));
            Object.DestroyImmediate(cat);
        }

        [Test]
        public void Generate_OnlyOwnedPieces()
        {
            var cat = Catalog();
            var r = new CostumeRandomizer();
            var owned = new HashSet<string> { "top.blue", "hair.c", "hat.cap" };
            var rng = new System.Random(3);
            for (int i = 0; i < 100; i++)
                foreach (var p in r.Generate(cat, owned.Contains, rng)) Assert.IsTrue(owned.Contains(p.guid), p.guid);
            Object.DestroyImmediate(cat);
        }

        static ModularCostumeCatalog StyleCatalog()
        {
            var c = ScriptableObject.CreateInstance<ModularCostumeCatalog>();
            void Slot(string id, bool required, params string[] items)
            {
                var s = new ModularCostumeCatalog.Slot { slot = id };
                foreach (var it in items) s.parts.Add(new ModularCostumeCatalog.PartEntry { name = it, itemId = it });
                c.slots.Add(s);
                c.slotDefinitions.Add(new ModularCostumeCatalog.SlotDefinition { id = id, required = required, allowNone = !required });
            }
            Slot("Chest", true, "top.a", "dress.f");
            Slot("Hair", true, "hair.short", "hair.big.f", "hair.bun.f");
            Slot("Head", false, "hat.cap", "hood.full");
            Slot("Mask", false, "mask.a");
            Slot("Beard", false, "beard.a");
            Slot("Eyewear", false, "glass.a");
            Slot("Earring", false, "ear.a");
            Slot("HairAccessory", false, "bow.f");
            return c;
        }

        static CostumeRandomizer StyleRules()
        {
            var r = new CostumeRandomizer();
            r.SetGender("dress.f", 'F'); r.SetGender("hair.big.f", 'F'); r.SetGender("hair.bun.f", 'F'); r.SetGender("bow.f", 'F'); r.SetGender("beard.a", 'M');
            r.SetSize("hair.short", 100); r.SetSize("hair.big.f", 900); r.SetSize("hair.bun.f", 700);
            r.SetHairLimits(300, 150);
            r.SetFullCover("hood.full");
            return r;
        }

        [Test]
        public void Styling_FollowsTheHeadAndGenderRules()
        {
            var cat = StyleCatalog(); var r = StyleRules();
            var rng = new System.Random(5);
            for (int i = 0; i < 2000; i++)
            {
                var o = r.Generate(cat, _ => true, rng);
                bool Has(string id) => o.Exists(p => p.guid == id);
                bool female = Has("dress.f") || Has("hair.big.f") || Has("hair.bun.f") || Has("bow.f");
                bool male = Has("beard.a");
                Assert.IsFalse(female && male, "one gender per outfit");
                if (Has("mask.a")) { Assert.IsFalse(Has("beard.a"), "mask: no beard"); Assert.IsFalse(Has("glass.a"), "mask: no glasses"); }
                if (Has("hat.cap") || Has("hood.full")) { Assert.IsTrue(Has("hair.short"), "hat: compact hair only"); Assert.IsFalse(Has("bow.f"), "hat: no hair bow"); }
                if (Has("hood.full")) { Assert.IsFalse(Has("glass.a")); Assert.IsFalse(Has("ear.a")); Assert.IsFalse(Has("beard.a")); }
                Assert.IsFalse(Has("mask.a") && (Has("hat.cap") || Has("hood.full")), "hat or mask, not both");
            }
            Object.DestroyImmediate(cat);
        }

        [Test]
        public void Harmony_PrefersColoursNearTheTop()
        {
            var cat = ScriptableObject.CreateInstance<ModularCostumeCatalog>();
            var chest = new ModularCostumeCatalog.Slot { slot = "Chest" }; chest.parts.Add(new ModularCostumeCatalog.PartEntry { itemId = "top.red" });
            var legs = new ModularCostumeCatalog.Slot { slot = "Legs" };
            legs.parts.Add(new ModularCostumeCatalog.PartEntry { itemId = "pants.red" });
            legs.parts.Add(new ModularCostumeCatalog.PartEntry { itemId = "pants.green" });   // 120 deg: triad
            legs.parts.Add(new ModularCostumeCatalog.PartEntry { itemId = "pants.lime" });    // ~75 deg: clashes
            cat.slots.Add(chest); cat.slots.Add(legs);
            cat.slotDefinitions.Add(new ModularCostumeCatalog.SlotDefinition { id = "Chest", required = true });
            cat.slotDefinitions.Add(new ModularCostumeCatalog.SlotDefinition { id = "Legs", required = true });
            var r = new CostumeRandomizer();
            r.SetColor("top.red", Color.HSVToRGB(0f, 0.8f, 0.8f));
            r.SetColor("pants.red", Color.HSVToRGB(0.02f, 0.8f, 0.8f));
            r.SetColor("pants.green", Color.HSVToRGB(0.33f, 0.8f, 0.8f));
            r.SetColor("pants.lime", Color.HSVToRGB(0.21f, 0.8f, 0.8f));
            var counts = new Dictionary<string, int> { { "pants.red", 0 }, { "pants.green", 0 }, { "pants.lime", 0 } };
            var rng = new System.Random(11);
            for (int i = 0; i < 2000; i++) foreach (var p in r.Generate(cat, _ => true, rng)) if (p.slot == "Legs") counts[p.guid]++;
            Assert.Greater(counts["pants.red"], counts["pants.lime"] * 3);
            Assert.Greater(counts["pants.green"], counts["pants.lime"] * 2);
            Object.DestroyImmediate(cat);
        }
    }
}
