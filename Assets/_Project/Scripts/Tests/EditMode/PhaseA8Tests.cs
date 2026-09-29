using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// <summary>Phase A8: the Magnet, Bomb and Freeze Clock items.</summary>
    public class PhaseA8Tests
    {
        [Test]
        public void EffectValuesNeverRenumber()
        {
            // Serialized in pickup prefabs; 2 was the retired bomb pickup and stays unused.
            Assert.AreEqual(0, (int)PickupEffect.Currency);
            Assert.AreEqual(1, (int)PickupEffect.Health);
            Assert.AreEqual(3, (int)PickupEffect.Magnet);
            Assert.AreEqual(4, (int)PickupEffect.Chest);
            Assert.AreEqual(5, (int)PickupEffect.Bomb);
            Assert.AreEqual(6, (int)PickupEffect.Freeze);
            Assert.IsFalse(System.Enum.IsDefined(typeof(PickupEffect), 2));
        }

        [Test]
        public void OnlyTheThreeItemsAreMechanic()
        {
            Assert.IsTrue(MechanicItems.IsMechanic(PickupEffect.Magnet));
            Assert.IsTrue(MechanicItems.IsMechanic(PickupEffect.Bomb));
            Assert.IsTrue(MechanicItems.IsMechanic(PickupEffect.Freeze));
            Assert.IsFalse(MechanicItems.IsMechanic(PickupEffect.Currency));
            Assert.IsFalse(MechanicItems.IsMechanic(PickupEffect.Health));
            Assert.IsFalse(MechanicItems.IsMechanic(PickupEffect.Chest));
        }

        [Test]
        public void TheDropTableGivesAllThree_MagnetMostOften()
        {
            int magnet = 0, bomb = 0, freeze = 0;
            for (int i = 0; i < 1000; i++)
            {
                var e = MechanicItems.Pick(i / 1000f);
                Assert.IsTrue(MechanicItems.IsMechanic(e));
                if (e == PickupEffect.Magnet) magnet++; else if (e == PickupEffect.Bomb) bomb++; else freeze++;
            }
            Assert.AreEqual(450, magnet);
            Assert.AreEqual(350, bomb);
            Assert.AreEqual(200, freeze);
            Assert.AreEqual(PickupEffect.Freeze, MechanicItems.Pick(0.9999f));
        }

        [Test]
        public void TheBombKillsANormalEnemy_AndTakesAShareOfAnElite()
        {
            Assert.Greater(MechanicItems.BombDamage(false, 500f), 500f);
            Assert.AreEqual(300f, MechanicItems.BombDamage(true, 1000f), 0.001f);
            Assert.Less(MechanicItems.EliteFreezeSeconds, MechanicItems.FreezeSeconds);
        }

        [TestCase("pickup_magnet", PickupEffect.Magnet)]
        [TestCase("pickup_bomb", PickupEffect.Bomb)]
        [TestCase("pickup_freeze", PickupEffect.Freeze)]
        public void EachItemHasItsPoolPrefab(string key, PickupEffect effect)
        {
            var go = Resources.Load<GameObject>("Pools/" + key);
            Assert.IsNotNull(go, key);
            var p = go.GetComponent<Pickup>();
            Assert.IsNotNull(p, key);
            Assert.AreEqual(effect, p.Effect);
            Assert.IsTrue(p.IsMechanic);
            Assert.IsNotNull(go.transform.Find("Model"), key + " model");
            Assert.Greater(go.GetComponentsInChildren<MeshRenderer>(true).Length, 0, key + " renders");
        }

        [Test]
        public void AnIgnoredItemBlinksThenLeaves_WithoutGoingOff()
        {
            var prefab = Resources.Load<GameObject>("Pools/pickup_bomb");
            var go = Object.Instantiate(prefab);
            try
            {
                var p = go.GetComponent<Pickup>();
                p.Init(PlayerProfile.CurrencyKind.Coin, 0, null, Vector3.zero);
                var far = new Vector3(100f, 0f, 0f);
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                bool blinked = false;
                for (float t = 0f; t < 44.9f; t += 0.05f)
                {
                    p.Tick(0.05f, far, 4.5f, true);   // a magnet sweep must not pull it either
                    if (t > 41f && !renderers[0].enabled) blinked = true;
                }
                Assert.IsTrue(go.activeSelf, "still there before 45 s");
                Assert.IsFalse(p.Collected);
                Assert.IsTrue(blinked, "blinks in its last seconds");
                for (int i = 0; i < 10; i++) p.Tick(0.05f, far, 4.5f, true);
                Assert.IsFalse(go.activeSelf, "gone after 45 s");
                foreach (var r in renderers) Assert.IsTrue(r.enabled, "handed back visible");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheLibraryBindsEveryItemEffect()
        {
            var lib = UnityEditor.AssetDatabase.LoadAssetAtPath<ZombieWar.Skills.Powers.SkillFxLibrary>(
                ZombieWar.EditorTools.SkillFxLibraryMigration.LibraryPath);
            Assert.IsNotNull(lib);
            Assert.IsNotNull(lib.items.magnetFx);
            Assert.IsNotNull(lib.items.bombFx);
            Assert.IsNotNull(lib.items.bombHitFx);
            Assert.IsNotNull(lib.items.freezeFx);
            Assert.IsNotNull(lib.items.freezeHitFx);
        }
    }
}
