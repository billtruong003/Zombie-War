using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.1 — the authoring gate. A weapon onboarded as data only (no grip/muzzle anchors) must be
    /// impossible to equip and invisible to the Hub, shop and loadout. All 54 guns are finished
    /// since 2026-09-28 (WeaponAutoGrip), so the gate itself is tested on a pending fixture.
    /// If this gate leaks, the player gets a gun floating at an arbitrary transform in their hand,
    /// which is exactly the class of defect M7.0's grip work existed to eliminate.
    /// </summary>
    public class WeaponAuthoringGateTests
    {
        const string DataDir = "Assets/_Project/Data/Weapons";
        const string CatalogPath = "Assets/_Project/Resources/WeaponCatalog.asset";

        static List<WeaponData> AllWeapons() =>
            AssetDatabase.FindAssets("t:WeaponData", new[] { DataDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null).ToList();

        static WeaponCatalog Catalog() => AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);

        [Test]
        public void EveryWeaponIdIsUniqueAcrossTheWholeArsenal()
        {
            var all = AllWeapons();
            var dupes = all.GroupBy(w => w.WeaponId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            CollectionAssert.IsEmpty(dupes, "weaponId is save identity and must never collide");
            Assert.IsFalse(all.Any(w => string.IsNullOrEmpty(w.WeaponId)), "every weapon needs an id");
        }

        /// A weapon still waiting for its anchors. Every shipped gun is finished (2026-09-28), so
        /// the gate is proven on this in-memory fixture instead of on a real asset.
        static WeaponData PendingFixture(bool twoHanded)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            var so = new SerializedObject(w);
            so.FindProperty("weaponId").stringValue = "weapon.test.pending";
            so.FindProperty("authoringStatus").intValue = (int)WeaponData.AuthoringStatus.PendingOwnerAuthoring;
            so.ApplyModifiedPropertiesWithoutUndo();
            w.twoHanded = twoHanded;
            return w;
        }

        [Test]
        public void EveryWeapon_IsFinished_WithItsAnchors()
        {
            foreach (var w in AllWeapons())
            {
                Assert.IsTrue(w.IsPlayable, $"'{w.name}' is not finished ({w.Authoring})");
                Assert.IsNotNull(w.weaponPrefab, $"'{w.name}' has no prefab");
                var contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(w.weaponPrefab));
                var grips = contents.GetComponentInChildren<WeaponGripPoints>(true);
                bool ok = grips != null && grips.RightHandGrip != null && grips.MuzzlePoint != null
                          && (!w.twoHanded || grips.LeftHandGrip != null);
                PrefabUtility.UnloadPrefabContents(contents);
                Assert.IsTrue(ok, $"'{w.name}' is missing a grip or muzzle anchor");
            }
        }

        [Test]
        public void PendingWeapon_CannotBeEquippedThroughLoadout()
        {
            var pending = PendingFixture(false);
            var result = LoadoutState.TryEquip(pending);
            Assert.AreEqual(LoadoutState.EquipResult.InvalidWeapon, result,
                "a weapon awaiting grip authoring must be refused by the loadout");
            Object.DestroyImmediate(pending);
        }

        [Test]
        public void PendingWeapon_IsRefusedByWeaponEquip()
        {
            var pending = PendingFixture(false);
            var go = new GameObject("weapon-host");
            var weapon = go.AddComponent<Weapon>();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Refusing to equip"));
            bool equipped = weapon.Equip(pending);

            Assert.IsFalse(equipped, "Weapon.Equip must refuse a pending weapon");
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(pending);
        }

        [Test]
        public void PendingWeapons_NeverReachHubShopOrLoadout()
        {
            var catalog = Catalog();
            Assert.IsNotNull(catalog);

            var shown = catalog.DisplayEntries();
            foreach (var e in shown)
                Assert.IsTrue(e.data == null || e.data.IsPlayable,
                    $"'{e.weaponId}' is displayable but not playable — the Hub would offer an unauthored weapon");
        }

        [Test]
        public void OnlyTheLauncher_BlowsUp()
        {
            foreach (var w in AllWeapons())
                if (w.weaponClass == WeaponClass.Rocket) Assert.Greater(w.splashRadius, 0f, $"'{w.name}' is a launcher without a blast");
                else Assert.AreEqual(0f, w.splashRadius, $"'{w.name}' must not splash");
        }

        /// <summary>
        /// The Factory's headline promise: adding weapon N is ONE catalog entry. This proves the
        /// catalog is the complete roster — every WeaponData on disk is in it exactly once, so no
        /// consumer needs its own folder scan to see a new weapon.
        /// </summary>
        [Test]
        public void Weapon26IsOneCatalogEntry_CatalogCoversEveryWeaponExactlyOnce()
        {
            var all = AllWeapons();
            var catalog = Catalog();

            Assert.AreEqual(all.Count, catalog.Entries.Count,
                "catalog entry count must equal the number of WeaponData assets");

            foreach (var w in all)
            {
                var entry = catalog.ById(w.WeaponId);
                Assert.IsNotNull(entry, $"'{w.name}' ({w.WeaponId}) is not in the catalog");
                Assert.AreSame(w, entry.data, $"catalog entry for '{w.WeaponId}' points at a different asset");
            }

            CollectionAssert.IsEmpty(catalog.Validate());
        }

        /// <summary>
        /// A5 fail-loud. The build must break when a weapon exists on disk without a catalog entry,
        /// because that is precisely the state in which a weapon ships with no audio key and the
        /// defect only surfaces when a player fires it.
        /// </summary>
        [Test]
        public void WeaponWithoutACatalogEntry_IsReportedAsAProblem_NotIgnored()
        {
            var real = Catalog();
            Assert.IsNotNull(real);

            // A catalog missing exactly one real weapon.
            var incomplete = ScriptableObject.CreateInstance<WeaponCatalog>();
            var trimmed = new List<WeaponCatalog.Entry>(real.Entries);
            var dropped = trimmed[trimmed.Count - 1];
            trimmed.RemoveAt(trimmed.Count - 1);
            incomplete.SetEntries(trimmed);

            var problems = EditorTools.WeaponCatalogAccess.Reconcile(incomplete);

            Assert.IsNotEmpty(problems, "a weapon with no catalog entry must be reported");
            Assert.IsTrue(problems.Exists(p => p.Contains(dropped.weaponId)),
                $"the report must name the offending weapon '{dropped.weaponId}'");

            Object.DestroyImmediate(incomplete);
        }

        [Test]
        public void RealCatalogReconcilesWithTheFolder_SoTheBuildDoesNotFail()
        {
            CollectionAssert.IsEmpty(EditorTools.WeaponCatalogAccess.Reconcile(Catalog()),
                "the shipped catalog and the weapon folder must agree");
        }

        [Test]
        public void NoWeaponMaterialIsVendorOwned_G1b()
        {
            var offenders = new List<string>();
            foreach (var w in AllWeapons())
            {
                if (w.weaponPrefab == null) continue;
                string path = AssetDatabase.GetAssetPath(w.weaponPrefab);
                var contents = PrefabUtility.LoadPrefabContents(path);
                foreach (var r in contents.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        string mp = AssetDatabase.GetAssetPath(m);
                        if (mp.StartsWith("Assets/ThirdParty/") || mp.StartsWith("Assets/Low Poly ") ||
                            mp.StartsWith("Assets/Synty/"))
                            offenders.Add($"{w.name} -> {mp}");
                    }
                PrefabUtility.UnloadPrefabContents(contents);
            }
            CollectionAssert.IsEmpty(offenders,
                "a vendor pack reimport must not be able to restyle the arsenal");
        }
    }
}
