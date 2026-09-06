using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.0 — the catalog exists to make "add weapon 26" a one-entry change. That is only safe if
    /// save identity is provably stable across the change, so these tests guard the two things a
    /// catalog can silently break:
    ///
    ///   1. Which weapon a NEW player starts with (was derived from catalogOrder — presentation
    ///      data — so re-ordering the shop list would have changed it).
    ///   2. Whether an EXISTING player who owns the demoted duplicate shotgun still resolves it.
    ///
    /// The duplicate shotgun matters specifically: `WPN_Shotgun_BenelliM4` and `WPN_Shotgun_Generic`
    /// are the same mesh, so one is folded into the other as a variant. Folding it away in the UI
    /// must not un-own it for anyone who already bought it.
    /// </summary>
    public class WeaponCatalogIdentityTests
    {
        const string CatalogPath = "Assets/_Project/Resources/WeaponCatalog.asset";
        const string ShotgunBaseId = "weapon.shotgun.benelli_m4";
        const string ShotgunVariantId = "weapon.shotgun.generic";

        WeaponCatalog _previousActive;

        [SetUp] public void SetUp() => _previousActive = WeaponCatalog.Active;
        [TearDown] public void TearDown() => WeaponCatalog.Active = _previousActive;

        static WeaponCatalog LoadShipped() => AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);

        static WeaponData MakeWeapon(string id, int order, bool twoHanded)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            var so = new SerializedObject(w);
            so.FindProperty("weaponId").stringValue = id;
            so.FindProperty("catalogOrder").intValue = order;
            so.ApplyModifiedPropertiesWithoutUndo();
            w.twoHanded = twoHanded;
            w.name = id;
            return w;
        }

        // ------------------------------------------------------------------ shipped catalog

        [Test]
        public void ShippedCatalog_ExistsAndSatisfiesItsContract()
        {
            var catalog = LoadShipped();
            Assert.IsNotNull(catalog, $"No catalog at {CatalogPath}. Run ZombieWar/Weapons/Factory/Build Weapon Catalog.");
            CollectionAssert.IsEmpty(catalog.Validate(), "Catalog contract failures");
        }

        [Test]
        public void ShippedCatalog_CoversEveryWeaponDataExactlyOnce()
        {
            var catalog = LoadShipped();
            var guids = AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/_Project/Data/Weapons" });
            var ids = new HashSet<string>();
            foreach (var g in guids)
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g));
                if (w != null && !string.IsNullOrEmpty(w.WeaponId)) ids.Add(w.WeaponId);
            }

            Assert.AreEqual(ids.Count, catalog.Entries.Count,
                "Catalog entry count must match the number of WeaponData assets");
            foreach (var id in ids)
                Assert.IsNotNull(catalog.ById(id), $"WeaponData '{id}' is missing from the catalog");
        }

        [Test]
        public void ShippedCatalog_DeclaresExactlyOneStarter_AndItIsOneHanded()
        {
            var starter = LoadShipped().Starter;
            Assert.IsNotNull(starter, "No entry is flagged Starter");
            Assert.IsNotNull(starter.data, "Starter entry has no WeaponData");
            Assert.IsFalse(starter.data.twoHanded, "Starter must be one-handed — slot 0 refuses two-handed weapons");
        }

        // ------------------------------------------------------------------ the duplicate shotgun

        [Test]
        public void DuplicateShotgun_IsRecordedAsAVariant_AndNeitherIdIsDeleted()
        {
            var catalog = LoadShipped();
            var base_ = catalog.ById(ShotgunBaseId);
            var variant = catalog.ById(ShotgunVariantId);

            Assert.IsNotNull(base_, "Base shotgun id must still exist — a player may own it");
            Assert.IsNotNull(variant, "Demoted shotgun id must still exist — a player may own it");

            Assert.AreEqual(ShotgunBaseId, variant.baseWeaponId, "Variant must point at its base");
            Assert.IsTrue(variant.IsVariant);
            Assert.IsFalse(base_.IsVariant, "Base must not itself be a variant");
            Assert.AreEqual(base_.variantGroupId, variant.variantGroupId, "Both must share a variant group");
        }

        [Test]
        public void DemotedVariant_StillResolvesToItsOwnWeaponData_NotTheBase()
        {
            var catalog = LoadShipped();
            var data = catalog.DataById(ShotgunVariantId);

            Assert.IsNotNull(data, "An existing save owning the demoted shotgun must still resolve it");
            Assert.AreEqual(ShotgunVariantId, data.WeaponId,
                "The variant must resolve to its OWN asset — silently swapping it for the base would " +
                "change what an existing player owns");
        }

        [Test]
        public void DisplayEntries_FoldTheVariantAway_ButAllDataRemainsReachable()
        {
            var catalog = LoadShipped();
            var shown = catalog.DisplayEntries();

            Assert.IsFalse(shown.Exists(e => e.weaponId == ShotgunVariantId),
                "The duplicate must not appear as its own shop entry");
            Assert.IsTrue(shown.Exists(e => e.weaponId == ShotgunBaseId), "The base must still be shown");

            // M7.1 widened what DisplayEntries hides: variants, Disabled entries AND weapons still
            // awaiting owner grip authoring. Asserting the exact predicate rather than a bare count,
            // so the check stays strict as the arsenal grows.
            int expected = catalog.Entries.Count(e =>
                !e.IsVariant &&
                e.unlockMethod != WeaponCatalog.UnlockMethod.Disabled &&
                (e.data == null || e.data.IsPlayable));
            Assert.AreEqual(expected, shown.Count,
                "DisplayEntries must show exactly the non-variant, non-disabled, playable entries");
            Assert.Less(shown.Count, catalog.Entries.Count, "at least the duplicate is folded away");
            Assert.IsNotNull(catalog.DataById(ShotgunVariantId), "Folded away is not the same as removed");
        }

        [Test]
        public void BaseOf_ResolvesVariantToBase_AndIsIdentityOnTheBase()
        {
            var catalog = LoadShipped();
            Assert.AreEqual(ShotgunBaseId, catalog.BaseOf(ShotgunVariantId).weaponId);
            Assert.AreEqual(ShotgunBaseId, catalog.BaseOf(ShotgunBaseId).weaponId);
        }

        // ------------------------------------------------------------------ starter independence

        [Test]
        public void Starter_ComesFromTheCatalogFlag_NotFromLowestCatalogOrder()
        {
            // The flagged starter deliberately has the HIGHEST catalogOrder, so a rule that used
            // order would pick the other weapon and this test would fail.
            var low = MakeWeapon("test.low_order", 0, false);
            var flagged = MakeWeapon("test.flagged_starter", 99, false);

            var catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
            catalog.SetEntries(new List<WeaponCatalog.Entry>
            {
                new() { weaponId = "test.low_order", data = low, catalogOrder = 0,
                        unlockMethod = WeaponCatalog.UnlockMethod.Purchase },
                new() { weaponId = "test.flagged_starter", data = flagged, catalogOrder = 99,
                        unlockMethod = WeaponCatalog.UnlockMethod.Starter },
            });

            Assert.AreEqual("test.flagged_starter", catalog.Starter.weaponId);
            CollectionAssert.IsEmpty(catalog.Validate());

            Object.DestroyImmediate(low);
            Object.DestroyImmediate(flagged);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void ReorderingCatalogOrder_DoesNotChangeTheStarter()
        {
            var a = MakeWeapon("test.a", 0, false);
            var b = MakeWeapon("test.b", 1, false);

            var catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
            var entries = new List<WeaponCatalog.Entry>
            {
                new() { weaponId = "test.a", data = a, catalogOrder = 0,
                        unlockMethod = WeaponCatalog.UnlockMethod.Starter },
                new() { weaponId = "test.b", data = b, catalogOrder = 1,
                        unlockMethod = WeaponCatalog.UnlockMethod.Purchase },
            };
            catalog.SetEntries(entries);
            string before = catalog.Starter.weaponId;

            // Presentation-only reshuffle: swap the orders and the list positions.
            entries[0].catalogOrder = 1;
            entries[1].catalogOrder = 0;
            entries.Reverse();
            catalog.SetEntries(entries);

            Assert.AreEqual(before, catalog.Starter.weaponId,
                "catalogOrder is presentation only — reordering must never change the starter");

            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Validate_RejectsTwoStarters_AndAMissingVariantBase()
        {
            var a = MakeWeapon("test.a", 0, false);
            var b = MakeWeapon("test.b", 1, false);

            var twoStarters = ScriptableObject.CreateInstance<WeaponCatalog>();
            twoStarters.SetEntries(new List<WeaponCatalog.Entry>
            {
                new() { weaponId = "test.a", data = a, unlockMethod = WeaponCatalog.UnlockMethod.Starter },
                new() { weaponId = "test.b", data = b, unlockMethod = WeaponCatalog.UnlockMethod.Starter },
            });
            Assert.IsNotEmpty(twoStarters.Validate(), "Two starters must be rejected");

            var orphanVariant = ScriptableObject.CreateInstance<WeaponCatalog>();
            orphanVariant.SetEntries(new List<WeaponCatalog.Entry>
            {
                new() { weaponId = "test.a", data = a, unlockMethod = WeaponCatalog.UnlockMethod.Starter },
                new() { weaponId = "test.b", data = b, baseWeaponId = "test.does_not_exist" },
            });
            Assert.IsNotEmpty(orphanVariant.Validate(), "A variant pointing at a missing base must be rejected");

            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(twoStarters);
            Object.DestroyImmediate(orphanVariant);
        }
    }
}
