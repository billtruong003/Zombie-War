using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.1 — the authoring gate. Twenty-nine weapons were onboarded from vendor packs as DATA ONLY:
    /// their grip and muzzle anchors are hand-authored by the owner and are deliberately absent.
    ///
    /// A weapon in that state must be impossible to equip and invisible to the Hub, shop and loadout.
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

        [Test]
        public void OnboardedWeaponsAreMarkedPending_AndHaveNoInferredAnchors()
        {
            var pending = AllWeapons().Where(w => !w.IsPlayable).ToList();
            Assert.Greater(pending.Count, 0, "M7.1 onboarded weapons should be present and pending");

            foreach (var w in pending)
            {
                Assert.IsFalse(w.useAuthoredGripPositions,
                    $"'{w.name}' is pending authoring, so authored grip positions must stay OFF — " +
                    "nothing may infer an anchor");

                if (w.weaponPrefab == null) continue;
                string path = AssetDatabase.GetAssetPath(w.weaponPrefab);
                var contents = PrefabUtility.LoadPrefabContents(path);
                var grips = contents.GetComponentInChildren<WeaponGripPoints>(true);
                bool anyAnchor = grips != null &&
                    (grips.RightHandGrip != null || grips.LeftHandGrip != null || grips.MuzzlePoint != null);
                PrefabUtility.UnloadPrefabContents(contents);

                Assert.IsFalse(anyAnchor,
                    $"'{w.name}' has an anchor placed but is still marked pending. Anchors are authored " +
                    "by the owner only; an inferred one must never appear here.");
            }
        }

        [Test]
        public void PendingWeapon_CannotBeEquippedThroughLoadout()
        {
            var pending = AllWeapons().FirstOrDefault(w => !w.IsPlayable && !w.twoHanded)
                       ?? AllWeapons().FirstOrDefault(w => !w.IsPlayable);
            Assert.IsNotNull(pending, "expected at least one pending weapon");

            int slot = pending.twoHanded ? 1 : 0;
            var result = LoadoutState.TryEquip(slot, pending);

            Assert.AreEqual(LoadoutState.EquipResult.InvalidWeapon, result,
                "a weapon awaiting owner grip authoring must be refused by the loadout");
        }

        [Test]
        public void PendingWeapon_IsRefusedByWeaponEquip()
        {
            var pending = AllWeapons().FirstOrDefault(w => !w.IsPlayable && !w.twoHanded);
            if (pending == null) Assert.Ignore("no one-handed pending weapon to test with");

            var go = new GameObject("weapon-host");
            var weapon = go.AddComponent<Weapon>();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Refusing to equip"));
            bool equipped = weapon.EquipToSlot(0, pending);

            Assert.IsFalse(equipped, "Weapon.EquipToSlot must refuse a pending weapon");
            Object.DestroyImmediate(go);
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
        public void LauncherIsBlockedForAMissingFireMode_NotMerelyPending()
        {
            var blocked = AllWeapons()
                .Where(w => w.Authoring == WeaponData.AuthoringStatus.BlockedNeedsProjectileFireMode).ToList();

            Assert.Greater(blocked.Count, 0,
                "the launcher body should be onboarded as data and blocked — FireMode.Projectile does not exist");
            foreach (var w in blocked) Assert.IsFalse(w.IsPlayable);
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
        public void ShippedTwentyFiveRemainPlayable_OnboardingDidNotRegressThem()
        {
            var playable = AllWeapons().Where(w => w.IsPlayable).ToList();
            Assert.AreEqual(25, playable.Count,
                "the 25 owner-accepted weapons must stay playable and nothing else may become playable " +
                "without owner sign-off");
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
