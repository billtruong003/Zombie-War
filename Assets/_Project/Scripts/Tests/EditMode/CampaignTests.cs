using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>Combat Power maths and the campaign stage gate. These guard the two rules that
    /// decide whether a player can start a stage, so a regression here silently breaks progression.</summary>
    public class CampaignTests
    {
        private WeaponData _smg;
        private WeaponData _shotgun;
        private CampaignCatalog _catalog;

        static WeaponData MakeWeapon(string id, float damage, float fireRate, int pellets, int mag, float reload)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            var so = new UnityEditor.SerializedObject(w);
            so.FindProperty("weaponId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            w.damage = damage;
            w.fireRate = fireRate;
            w.pelletCount = pellets;
            w.magazineSize = mag;
            w.reloadDuration = reload;
            return w;
        }

        [SetUp]
        public void SetUp()
        {
            // Fast, low-damage SMG vs slow, multi-pellet shotgun: raw `damage` would rank these
            // wrongly, which is exactly what CombatPower exists to avoid.
            _smg = MakeWeapon("weapon.smg.test", 10f, 12f, 1, 30, 1.5f);
            _shotgun = MakeWeapon("weapon.shotgun.test", 8f, 1.5f, 8, 6, 2.5f);

            _catalog = ScriptableObject.CreateInstance<CampaignCatalog>();
            var so = new UnityEditor.SerializedObject(_catalog);
            var levels = so.FindProperty("levels");
            levels.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var e = levels.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("levelId").stringValue = $"level.{i + 1}";
                e.FindPropertyRelative("displayName").stringValue = $"Màn {i + 1}";
                e.FindPropertyRelative("sceneName").stringValue = $"Map_Level{i + 1}";
                e.FindPropertyRelative("minimumPower").intValue = i * 1000;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in new Object[] { _smg, _shotgun, _catalog })
                if (o != null) Object.DestroyImmediate(o);
        }

        [Test]
        public void EffectiveDps_AccountsForPelletsAndReload()
        {
            // Shotgun: 8 dmg x 8 pellets = 64 per shot, 6 shots per magazine = 384 damage,
            // over (6 / 1.5) + 2.5 = 6.5 s  ->  ~59 dps.
            float dps = CombatPower.EffectiveDps(_shotgun, 1);
            Assert.AreEqual(384f / 6.5f, dps, 0.5f);
        }

        [Test]
        public void EffectiveDps_IsZeroForNullWeapon() =>
            Assert.AreEqual(0f, CombatPower.EffectiveDps(null, 1));

        [Test]
        public void EffectiveDps_RisesWithStarLevel()
        {
            float one = CombatPower.EffectiveDps(_smg, 1);
            float three = CombatPower.EffectiveDps(_smg, 3);
            Assert.Greater(three, one, "star upgrades must raise effective dps");
        }

        [Test]
        public void Evaluate_CountsBestWeaponFullyAndBackupsPartially()
        {
            float best = Mathf.Max(CombatPower.WeaponPower(_smg, 1), CombatPower.WeaponPower(_shotgun, 1));

            int single = CombatPower.Evaluate(new[] { _smg });
            int pair = CombatPower.Evaluate(new[] { _smg, _shotgun });

            Assert.Greater(pair, single, "filling more slots must always increase power");
            Assert.Less(pair, Mathf.RoundToInt(best * 2f), "backups must not count at full weight");
        }

        [Test]
        public void Evaluate_IgnoresNullSlots()
        {
            int withNulls = CombatPower.Evaluate(new[] { _smg, null, null });
            int without = CombatPower.Evaluate(new[] { _smg });
            Assert.AreEqual(without, withNulls);
        }

        [Test]
        public void Evaluate_EmptyLoadoutIsZero()
        {
            Assert.AreEqual(0, CombatPower.Evaluate(new WeaponData[0]));
            Assert.AreEqual(0, CombatPower.Evaluate(null));
        }

        [Test]
        public void FirstStage_IsAlwaysOpen()
        {
            var gate = _catalog.Evaluate(0, 0);
            Assert.IsTrue(gate.CanPlay, "a fresh profile must never be locked out of stage 1");
        }

        [Test]
        public void LaterStage_LockedUntilPreviousCleared()
        {
            var gate = _catalog.Evaluate(1, 999999);
            Assert.IsFalse(gate.CanPlay);
            Assert.AreEqual(LevelGate.Status.Locked, gate.State);
            StringAssert.Contains("Màn 1", gate.Reason, "the lock must name the stage that unlocks it");
        }

        [Test]
        public void ClearedPrevious_ButUnderpowered_ReportsPowerNotLock()
        {
            PlayerProfile.MarkLevelCompleted("level.1");
            try
            {
                var gate = _catalog.Evaluate(1, 10);
                Assert.IsFalse(gate.CanPlay);
                Assert.AreEqual(LevelGate.Status.Underpowered, gate.State,
                    "with the previous stage cleared the blocker must be power, not progression");
            }
            finally { ResetCampaign(); }
        }

        [Test]
        public void ClearedPreviousAndEnoughPower_Opens()
        {
            PlayerProfile.MarkLevelCompleted("level.1");
            try
            {
                Assert.IsTrue(_catalog.Evaluate(1, 5000).CanPlay);
            }
            finally { ResetCampaign(); }
        }

        [Test]
        public void MarkLevelCompleted_IsIdempotent()
        {
            try
            {
                PlayerProfile.MarkLevelCompleted("level.1");
                PlayerProfile.MarkLevelCompleted("level.1");
                PlayerProfile.MarkLevelCompleted("level.1");

                int count = 0;
                foreach (var id in PlayerProfile.CompletedLevelIds) if (id == "level.1") count++;
                Assert.AreEqual(1, count, "a replayed stage must not be recorded twice");
            }
            finally { ResetCampaign(); }
        }

        [Test]
        public void FirstClearReward_PaysExactlyOnce()
        {
            try
            {
                long before = PlayerProfile.Coin;

                Assert.IsTrue(PlayerProfile.TryClaimFirstClear("level.1", 500, 0, 0));
                Assert.IsFalse(PlayerProfile.TryClaimFirstClear("level.1", 500, 0, 0));
                Assert.IsFalse(PlayerProfile.TryClaimFirstClear("level.1", 500, 0, 0));

                Assert.AreEqual(before + 500, PlayerProfile.Coin,
                    "replaying a cleared stage must never repeat its first-clear reward");
            }
            finally { ResetCampaign(); }
        }

        [Test]
        public void Catalog_LookupByIdAndIndexAgree()
        {
            Assert.AreEqual("level.2", _catalog.Get(1).levelId);
            Assert.AreEqual(1, _catalog.IndexOf("level.2"));
            Assert.IsNotNull(_catalog.Find("level.3"));
            Assert.IsNull(_catalog.Find("level.nope"));
            Assert.IsNull(_catalog.Get(99));
        }

        static void ResetCampaign()
        {
            // These tests write real profile state; clear only what they touched.
            PlayerProfile.ClearCampaignProgressForTests();
        }
    }
}
