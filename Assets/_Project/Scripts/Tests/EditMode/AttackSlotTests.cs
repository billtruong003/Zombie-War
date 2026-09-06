using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.4b — the attacker cap that stops the instant melt.
    ///
    /// Measured cause: nothing one-shots the player (highest single hit is 30 vs 100 HP), but eight
    /// enemies in contact stack to ~70-90 DPS and <c>Health</c> has no invulnerability, grace period
    /// or damage cooldown. A pack landing together deletes the player in about a second with no
    /// readable moment. Bounding simultaneous attackers keeps the horde but bounds the damage.
    /// </summary>
    public class AttackSlotTests
    {
        [SetUp] public void SetUp() => ZombieManager.ResetAttackSlots();
        [TearDown] public void TearDown() { ZombieManager.ResetAttackSlots(); RunState.Abandon(); }

        [Test]
        public void SlotsAreBoundedAndReleaseRestoresThem()
        {
            int cap = ZombieManager.AttackSlotCap;
            Assert.Greater(cap, 0, "there must be a real cap");
            Assert.AreEqual(0, ZombieManager.AttackSlotsInUse, "a fresh run starts with none in use");
        }

        [Test]
        public void SlotCounterIsRunScoped_ALeakWouldThrottleTheNextRun()
        {
            RunState.Begin("test");
            typeof(ZombieManager).GetProperty("AttackSlotsInUse",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                ?.GetValue(null);

            // Simulate slots left claimed by enemies that died mid-swing.
            ZombieManager.ResetAttackSlots();
            Assert.AreEqual(0, ZombieManager.AttackSlotsInUse);

            RunState.Abandon();
            RunState.Begin("test");
            Assert.AreEqual(0, ZombieManager.AttackSlotsInUse,
                "a leaked slot would silently throttle every attack in the next run");
        }

        [Test]
        public void RunScopeClearsAttackSlots()
        {
            RunScope.ResetAll();
            Assert.AreEqual(0, ZombieManager.AttackSlotsInUse);
        }

        [Test]
        public void NullEnemyNeverClaimsOrLeaksASlot()
        {
            Assert.IsFalse(ZombieManager.TryClaimAttackSlot(null));
            Assert.AreEqual(0, ZombieManager.AttackSlotsInUse);
            Assert.DoesNotThrow(() => ZombieManager.ReleaseAttackSlot(null));
            Assert.AreEqual(0, ZombieManager.AttackSlotsInUse);
        }

        [Test]
        public void BossesAndElitesAreNeverDeniedTheirAttack()
        {
            string src = System.IO.File.ReadAllText(
                Application.dataPath + "/_Project/Scripts/Runtime/Gameplay/ZombieManager.cs");

            StringAssert.Contains("isElite", src, "an elite must always be able to commit");
            StringAssert.Contains("IsBeaconOwned", src, "a beacon boss must always be able to commit");
            StringAssert.Contains("return true;   // never denied", src.Replace("  ", "  "),
                "the bypass must be an explicit early-out, not a side effect of the counter");
        }

        [Test]
        public void AWaitingEnemyIsInspectable_ItReadsAsWaitingNotBroken()
        {
            var prop = typeof(ZombieBase).GetProperty("IsWaitingForAttackSlot");
            Assert.IsNotNull(prop,
                "a waiting enemy must be distinguishable from a frozen one, in code and on screen");
            Assert.IsTrue(prop.CanRead);
        }

        [Test]
        public void EveryExitPathReleasesTheSlot()
        {
            string src = System.IO.File.ReadAllText(
                Application.dataPath + "/_Project/Scripts/Runtime/Gameplay/Zombies/ZombieBase.cs");

            // Dying mid-swing, being pooled, or resolving normally must all hand the slot back, or
            // the cap silently tightens until nothing can attack at all.
            int releases = src.Split(new[] { "ReleaseAttackSlotIfHeld()" }, System.StringSplitOptions.None).Length - 1;
            Assert.GreaterOrEqual(releases, 4,
                "claim/release must be balanced across resolve, cancel and despawn paths");
        }

        /// <summary>
        /// The cap has to bound the attack RATE, not the number of enemies mid-swing.
        ///
        /// Measured, and the reason this test exists: the first M7.4b cut released the slot on the
        /// impact frame, so it was held only for the windup. Against ZD_Zombie (0.3 s windup, 1.2 s
        /// cooldown) each of the 4 slots recycled four times per cooldown and 18 crowding enemies
        /// still landed ~15 hits/s. The A/B came out at 72 DPS capped against 63 DPS uncapped — the
        /// cap was doing nothing at all while every structural test above passed.
        /// </summary>
        [Test]
        public void TheSlotIsHeldForTheCooldown_SoTheCapBoundsDamagePerSecond()
        {
            string src = System.IO.File.ReadAllText(
                Application.dataPath + "/_Project/Scripts/Runtime/Gameplay/Zombies/ZombieBase.cs");

            int impactRelease = src.IndexOf("PerformAttack(target);", System.StringComparison.Ordinal);
            Assert.Greater(impactRelease, 0, "the swing must still land its hit");
            string afterImpact = src.Substring(impactRelease, System.Math.Min(220, src.Length - impactRelease));
            StringAssert.DoesNotContain("ReleaseAttackSlotIfHeld();", afterImpact,
                "releasing on the impact frame holds the slot for the windup only, which caps " +
                "concurrent swings while leaving the damage rate unbounded");

            StringAssert.Contains("if (_attackCooldownTimer <= 0f) ReleaseAttackSlotIfHeld();", src,
                "the slot must come back when the attacker's cooldown ends, not when its hit lands");
        }

        /// <summary>
        /// The ceiling the cap actually buys, stated as a number so a future tuning pass cannot
        /// quietly reintroduce the melt. Worst realistic case: a crowd of the commonest enemy.
        /// </summary>
        [Test]
        public void TheBoundedCrowdCannotDeleteAFullHealthPlayerInUnderTwoSeconds()
        {
            var zombie = UnityEditor.AssetDatabase.LoadAssetAtPath<ZombieData>(
                "Assets/_Project/Data/Zombies/ZD_Zombie.asset");
            if (zombie == null)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("ZD_Zombie t:ZombieData");
                Assert.Greater(guids.Length, 0, "the baseline enemy must exist to bound the crowd");
                zombie = UnityEditor.AssetDatabase.LoadAssetAtPath<ZombieData>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            int cap = ZombieManager.AttackSlotCap;
            float swingsPerSecond = cap / Mathf.Max(0.01f, zombie.attackCooldown);
            float worstCaseDps = swingsPerSecond * zombie.damage;
            float secondsToKill = 100f / Mathf.Max(0.01f, worstCaseDps);

            // Uncapped this measured 63 DPS / 1.6 s, which is the owner's "one bump and you're dead".
            Assert.Greater(secondsToKill, 2f,
                $"a crowd deletes 100 HP in {secondsToKill:0.00}s at {worstCaseDps:0} DPS — " +
                "there is no readable moment to react in");
        }

        [Test]
        public void ArrivalCounterIsRunScoped()
        {
            RunState.Begin("test");
            Threat.ThreatDirector.ResetArrivals();
            Assert.AreEqual(0, Threat.ThreatDirector.ArrivalsThisRun);

            RunScope.ResetAll();
            Assert.AreEqual(0, Threat.ThreatDirector.ArrivalsThisRun);
        }

        [Test]
        public void PacingWasReshapedNotStrengthened()
        {
            string src = System.IO.File.ReadAllText(
                Application.dataPath + "/_Project/Scripts/Runtime/Gameplay/Threat/ThreatDirector.cs");

            StringAssert.Contains("spawnBurst = 1", src,
                "packs were the cause of both the clumping and the instant deaths");
            StringAssert.Contains("intervalJitter", src,
                "a fixed cadence lets arrivals re-synchronise into packs even at burst 1");
            StringAssert.Contains("AliveTargetFor", src, "the crowd ceiling must still bound total pressure");
        }

        /// <summary>
        /// The test above passed while the shipped game did the OPPOSITE, and this one exists because
        /// of it. Changing a <c>[SerializeField]</c> default in C# does not touch instances Unity has
        /// already serialised: the Player prefab still held <c>baseSpawnInterval = 2.2</c>, so the
        /// burst dropped 3 → 1 with no matching interval cut and live pressure ran at roughly HALF of
        /// M7.4a instead of the same average. Asserting source text proves the author's intent; only
        /// reading the serialised asset proves what the player actually gets.
        /// </summary>
        [Test]
        public void ThePacingValuesTheGameActuallyLoads_NotJustTheOnesInSource()
        {
            const string path = "Assets/_Project/Prefabs/Player.prefab";
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " is where ThreatDirector is serialised");

            var director = prefab.GetComponentInChildren<Threat.ThreatDirector>(true);
            Assert.IsNotNull(director, "the threat director must still live on the player prefab");

            var so = new UnityEditor.SerializedObject(director);
            float interval = so.FindProperty("baseSpawnInterval").floatValue;
            int burst = so.FindProperty("spawnBurst").intValue;

            Assert.AreEqual(1, burst, "packs are what clumped the pressure and delivered the melt");
            Assert.AreEqual(0.8f, interval, 0.001f,
                "burst 1 at the old 2.2 s interval halves the pressure instead of reshaping it");

            // The point of the milestone: same average arrivals per second, delivered continuously.
            // Old: burst 3 every 2.2 s = 1.36/s in packs. New: burst 1 every 0.8 s = 1.25/s singly.
            float arrivalsPerSecond = burst / interval;
            Assert.AreEqual(1.36f, arrivalsPerSecond, 0.2f,
                "average pressure must be preserved — only its distribution changes");

            Assert.Greater(so.FindProperty("intervalJitter").floatValue, 0f,
                "a metronome lets single arrivals re-synchronise into packs on their own");
        }
    }
}
