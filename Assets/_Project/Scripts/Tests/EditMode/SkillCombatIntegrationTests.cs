using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.2b — proves the skill system is actually CONSULTED by combat.
    ///
    /// Before this run the card system was complete, tested and entirely inert: `ModifyHitDamage`,
    /// `OnKill`, `TryAbsorbDamage` and `PollPowers` had no callers outside tests, so all 23 cards
    /// changed nothing in play. These tests exist so that can never silently regress — a card system
    /// that passes its own unit tests while being disconnected is the exact failure being guarded.
    /// </summary>
    public class SkillCombatIntegrationTests
    {
        SkillRuntime _previous;

        [SetUp]
        public void SetUp()
        {
            _previous = SkillRuntime.Active;
            StatusCarrier.ClearAll();
            AutonomousPower.ResetGlobalBudget();
        }

        [TearDown]
        public void TearDown() => SkillRuntime.Active = _previous;

        // ─────────────────────────────────────────── call site 1: the damage path

        [Test]
        public void WeaponDamagePath_ConsultsTheSkillRuntime()
        {
            // Source-level proof that the hit site routes through the runtime. A behavioural test
            // would need a full weapon + physics scene; this asserts the wiring the report claims.
            string src = SourceText.Read("/_Project/Scripts/Runtime/Gameplay/Weapon.cs");

            StringAssert.Contains("SkillRuntime.Active", src, "the weapon must consult the skill runtime");
            StringAssert.Contains("ModifyHitDamage", src, "final damage must route through the cards");
            StringAssert.Contains("ApplyHitStatuses", src, "OnHit statuses must be applied at the hit site");
        }

        [Test]
        public void TakingDamageUpMeasurablyIncreasesOutputDamage()
        {
            var run = new SkillRuntime { EquippedFamily = WeaponClass.AssaultRifle };
            float before = run.ModifyHitDamage(100f, 1, 5f, 1f, 0f);

            run.Take(SkillCatalogDefs.StatDamage);
            float after = run.ModifyHitDamage(100f, 2, 5f, 1f, 0f);

            Assert.AreEqual(100f, before, 1e-3, "an empty build must not change damage");
            Assert.Greater(after, before, "a taken card must change the number that reaches the enemy");
        }

        // ─────────────────────────────────────────── call site 2: kills

        [Test]
        public void KillEventDrivesTheRuntime_SoulBurstCharges()
        {
            string src = SourceText.Read("/_Project/Scripts/Runtime/Gameplay/Skills/SkillCombatDriver.cs");
            StringAssert.Contains("Subscribe<ZombieKilledEvent>", src, "kills must feed the runtime");
            StringAssert.Contains("Unsubscribe<ZombieKilledEvent>", src, "and must unsubscribe on teardown");

            var run = new SkillRuntime();
            run.Take(SkillCatalogDefs.AutoSoulBurst);
            Assert.AreEqual(0, run.PollPowers(0f, 1f).Count);
            for (int i = 0; i < 12; i++) run.OnKill();
            Assert.AreEqual(1, run.PollPowers(1f, 1f).Count, "kills must charge Soul Burst");
        }

        // ─────────────────────────────────────────── call site 3: the player shield

        [Test]
        public void PlayerHealthConsultsTheShield_AndEnemiesDoNot()
        {
            string src = SourceText.Read("/_Project/Scripts/Runtime/Gameplay/Health.cs");

            StringAssert.Contains("TryAbsorbDamage", src, "player damage must consult Kinetic Shield");
            StringAssert.Contains("_isPlayer", src,
                "the shield must be gated on the owner being the player — an enemy must never spend the charge");
            StringAssert.Contains("PlayShieldBreak", src, "an absorbed hit must be made visible");
        }

        [Test]
        public void ShieldAbsorbsExactlyOneHitPerCharge()
        {
            var run = new SkillRuntime();
            run.Take(SkillCatalogDefs.UniKinetic);

            var p = Vector3.zero;
            for (int i = 0; i < 80; i++) { p += Vector3.forward; run.Tick(0.1f, p, true, false, 1f); }

            Assert.IsTrue(run.TryAbsorbDamage());
            Assert.IsFalse(run.TryAbsorbDamage());
        }

        // ─────────────────────────────────────────── call site 4: the power driver

        [Test]
        public void ADriverExists_TicksOnce_AndAppliesEveryPowerKind()
        {
            string src = SourceText.Read("/_Project/Scripts/Runtime/Gameplay/Skills/SkillCombatDriver.cs");

            StringAssert.Contains("PollPowers", src, "something must poll the powers each frame");
            StringAssert.Contains("Dispatch", src, "every proc must reach the module that owns it");

            // Phase A2: every card that procs is claimed by exactly one power module.
            var claimed = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var t in typeof(ZombieWar.Skills.Powers.PowerModule).Assembly.GetTypes())
            {
                if (t.IsAbstract || !typeof(ZombieWar.Skills.Powers.PowerModule).IsAssignableFrom(t)) continue;
                var m = (ZombieWar.Skills.Powers.PowerModule)System.Activator.CreateInstance(t);
                foreach (var id in m.ProcIds)
                {
                    if (claimed.TryGetValue(id, out var other)) Assert.Fail($"{id} is claimed by {other} and {t.Name}");
                    claimed[id] = t.Name;
                }
            }
            foreach (var id in new[] { SkillCatalogDefs.AutoChainLightning, SkillCatalogDefs.AutoOrdnance,
                                       SkillCatalogDefs.AutoSoulBurst, SkillCatalogDefs.AutoEmergency,
                                       SkillCatalogDefs.AutoFrostNova, SkillCatalogDefs.AutoBoomerang,
                                       SkillCatalogDefs.AutoAirstrike, SkillCatalogDefs.SmgStatic })
                Assert.IsTrue(claimed.ContainsKey(id), $"no power module handles {id}");

            StringAssert.Contains("TakeDamage", SourceText.Read("/_Project/Scripts/Runtime/Gameplay/Skills/Powers/PowerKit.cs"),
                "a power proc must actually damage something");
        }

        [Test]
        public void DriverIsAttachedToThePlayerWithItsFxBound()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab");
            Assert.IsNotNull(player);

            var driver = player.GetComponent<SkillCombatDriver>();
            Assert.IsNotNull(driver, "the power driver must live on the player, or no power can ever fire");
            var arsenal = player.GetComponent<SkillArsenal>();
            Assert.IsNotNull(arsenal, "the power host must live on the player");

            var lib = new SerializedObject(arsenal).FindProperty("library").objectReferenceValue as ZombieWar.Skills.Powers.SkillFxLibrary;
            Assert.IsNotNull(lib, "the player's SkillArsenal must reference the FX library");
            Assert.IsNotNull(lib.chain.sparkFx, "chain FX unbound");
            Assert.IsNotNull(lib.ordnance.blastFx, "explosion FX unbound");
            Assert.IsNotNull(lib.shield.breakFx, "shield-break FX unbound");

            Assert.IsNotNull(player.GetComponent<Health>(), "the driver needs the player's Health for HP triggers");
            Assert.IsNotNull(player.GetComponent<PlayerMovement>(), "the shield gate resolves off PlayerMovement");
        }

        // ─────────────────────────────────────────── the pooling trap

        [Test]
        public void EnemyDespawnClearsStatuses_SoARecycledEnemyInheritsNothing()
        {
            string src = SourceText.Read("/_Project/Scripts/Runtime/Gameplay/Zombies/ZombieBase.cs");
            StringAssert.Contains("StatusCarrier.Clear", src,
                "pooled enemies must clear their statuses on despawn");

            // And the behaviour it protects: reuse of an id must start clean.
            const int recycledId = 4242;
            StatusCarrier.Apply(recycledId, StatusKind.Exposed, 1f, 999f, 0f);
            StatusCarrier.Accumulate(recycledId, StatusKind.HitCount, 5f, 999f, 0f);
            Assert.IsTrue(StatusCarrier.Has(recycledId, StatusKind.Exposed, 1f));

            StatusCarrier.Clear(recycledId);

            Assert.AreEqual(0f, StatusCarrier.Get(recycledId, StatusKind.Exposed, 1f),
                "an Exposed enemy that dies must not hand its debuff to the next spawn");
            Assert.AreEqual(0f, StatusCarrier.Get(recycledId, StatusKind.HitCount, 1f),
                "Focus Fire's per-target counter must reset too, or damage spikes on a fresh enemy");
        }

        [Test]
        public void FocusFireRampDoesNotSurviveARecycle()
        {
            var run = new SkillRuntime { EquippedFamily = WeaponClass.AssaultRifle };
            run.Take(SkillCatalogDefs.ArFocusFire);
            const int id = 909;

            for (int i = 0; i < 5; i++) run.ModifyHitDamage(100f, id, 5f, 1f, 0f);
            float ramped = run.ModifyHitDamage(100f, id, 5f, 1f, 0f);

            StatusCarrier.Clear(id);                       // the enemy dies and its slot is reused
            float fresh = run.ModifyHitDamage(100f, id, 5f, 1f, 0f);

            Assert.Less(fresh, ramped, "a recycled enemy must not inherit the previous target's ramp");
        }

        // ─────────────────────────────────────────── guardrails under load

        [Test]
        public void GlobalProcCeilingStillHoldsWithEveryPowerTaken()
        {
            var run = new SkillRuntime();
            run.Take(SkillCatalogDefs.AutoChainLightning);
            run.Take(SkillCatalogDefs.AutoOrdnance);
            run.Take(SkillCatalogDefs.AutoSoulBurst);
            run.Take(SkillCatalogDefs.AutoEmergency);
            for (int i = 0; i < 50; i++) run.OnKill();

            int procs = 0;
            for (float t = 0f; t < 1f; t += 0.05f) procs += run.PollPowers(t, 0.1f).Count;

            Assert.LessOrEqual(procs, (int)AutonomousPower.GlobalProcsPerSecond,
                "however many powers are active, the shared ceiling holds");
        }

        [Test]
        public void PowerApplicationIsBoundedToTwoConcurrentExplosions()
        {
            string src = SourceText.Read("/_Project/Scripts/Runtime/Gameplay/Skills/Powers/SelfBurstPower.cs");
            StringAssert.Contains("_burstsThisFrame >= 2", src,
                "the damage cost of blasts must be bounded, not only the visual");
            StringAssert.Contains("PowerKit.PlaySized", src, "FX must go through the pool, never Instantiate");
            Assert.IsFalse(src.Contains("Instantiate"), "no ad-hoc instantiation in a proc");
            string driver = SourceText.Read("/_Project/Scripts/Runtime/Gameplay/Skills/SkillCombatDriver.cs");
            Assert.IsFalse(driver.Contains("Instantiate"), "no ad-hoc instantiation in the driver");
        }

        /// <summary>
        /// Regression: the first play-test of the power driver KILLED THE PLAYER. Powers are centred
        /// on the player, the broad-phase sweep returned the player's own collider, and a generic
        /// IDamageable lookup applied Chain Lightning to them. A power must never damage anything
        /// that is not an enemy.
        /// </summary>
        [Test]
        public void PowerDamageResolvesEnemiesOnly_NeverThePlayer()
        {
            string src = SourceText.Read("/_Project/Scripts/Runtime/Gameplay/Skills/Powers/PowerKit.cs");

            // G8 (04/10): the guarantee is now the type itself - PowerKit.Hit takes a ZombieBase and
            // there is no Collider/IDamageable overload a player-centred power could reach the player through.
            StringAssert.Contains("public static void Hit(ZombieBase enemy", src,
                "power damage must take the ENEMY type, not any IDamageable");
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(src, @"IDamageable\s+\w+|<IDamageable>"),
                "a generic IDamageable path lets a player-centred power damage the player");
            Assert.IsFalse(src.Contains("Hit(Collider"),
                "a Collider overload would resolve whatever it hit, the player included");
        }

        [Test]
        public void PowerProcUsesExactlyOneSpatialQuery()
        {
            // One query per proc, in each module's OnProc (Self Burst has two procs: its burst and the
            // Reaper's, one query each). The driver itself queries nothing.
            var expected = new System.Collections.Generic.Dictionary<string, int>
            {
                { "ChainPower", 1 }, { "OrdnancePower", 1 }, { "FrostNovaPower", 1 },
                { "AirstrikePower", 1 }, { "SelfBurstPower", 2 },
            };
            foreach (var kv in expected)
            {
                string src = System.IO.File.ReadAllText(
                    Application.dataPath + "/_Project/Scripts/Runtime/Gameplay/Skills/Powers/" + kv.Key + ".cs");
                int gathers = src.Split(new[] { "TargetQuery.Gather" }, System.StringSplitOptions.None).Length - 1;
                Assert.AreEqual(kv.Value, gathers, $"{kv.Key}: one Gather per proc keeps the one-query-per-proc guarantee");
            }
            string driver = SourceText.Read("/_Project/Scripts/Runtime/Gameplay/Skills/SkillCombatDriver.cs");
            Assert.IsFalse(driver.Contains("TargetQuery.Gather"), "the driver itself queries nothing");
        }
    }
}
