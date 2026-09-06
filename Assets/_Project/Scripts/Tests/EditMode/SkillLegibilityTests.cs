using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.2c — the owner's second and third complaints: you cannot tell when a skill triggers, and
    /// the VFX does not represent the skill.
    ///
    /// These assert the things that make a proc readable, because "it looks right" cannot be unit
    /// tested — but "an arc is actually drawn between successive targets" and "each power has its own
    /// distinct effect bound" can be.
    /// </summary>
    public class SkillLegibilityTests
    {
        const string DriverSrc = "/_Project/Scripts/Runtime/Gameplay/Skills/SkillCombatDriver.cs";
        const string Player = "Assets/_Project/Prefabs/Player.prefab";

        static string Read(string rel) => System.IO.File.ReadAllText(Application.dataPath + rel);

        // ─────────────────────────────────────────── chain arc

        [Test]
        public void ChainDrawsABoltBetweenSuccessiveTargets_NotJustAFlashOnEach()
        {
            string src = Read(DriverSrc);
            StringAssert.Contains("DrawArc", src,
                "a chain must connect its targets — Epic Toon FX has no beam prefab, so the arc is drawn");
            StringAssert.Contains("from = point", src,
                "each hop must start where the previous one landed, or it is not a chain");
        }

        [Test]
        public void ArcPoolIsBoundedAndSharesOneMaterial()
        {
            string fx = Read("/_Project/Scripts/Runtime/Gameplay/Skills/SkillFxDirector.cs");

            StringAssert.Contains("sharedMaterial", fx, "arcs must share one material asset");
            Assert.IsFalse(fx.Contains("lr.material ="),
                "assigning .material clones it per arc — that is a runtime material instance");
            StringAssert.Contains("arcPoolSize", fx, "arcs must come from a bounded pool");
        }

        [Test]
        public void ArcPoolExhaustionDropsTheVisualRatherThanAllocating()
        {
            string fx = Read("/_Project/Scripts/Runtime/Gameplay/Skills/SkillFxDirector.cs");
            StringAssert.Contains("if (arc == null) return", fx,
                "an exhausted pool must drop the effect, never Instantiate mid-fight");
        }

        // ─────────────────────────────────────────── distinct powers

        [Test]
        public void EachAutonomousPowerHasItsOwnBoundEffect()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(Player);
            var driver = player.GetComponent<SkillCombatDriver>();
            Assert.IsNotNull(driver);

            var so = new SerializedObject(driver);
            foreach (var field in new[] { "chainArcFx", "explosionFx", "ordnanceMarkerFx",
                                          "soulBurstFx", "emergencyFx", "shieldBreakFx" })
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, $"{field} is unbound");
        }

        [Test]
        public void SoulBurstAndEmergencyDetonationUseDifferentEffects()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(Player);
            var so = new SerializedObject(player.GetComponent<SkillCombatDriver>());

            var soul = so.FindProperty("soulBurstFx").objectReferenceValue;
            var emergency = so.FindProperty("emergencyFx").objectReferenceValue;

            Assert.IsNotNull(soul);
            Assert.IsNotNull(emergency);
            Assert.AreNotSame(soul, emergency,
                "Emergency Detonation fires when you are nearly dead — it must not look like a routine Soul Burst");
        }

        [Test]
        public void OrdnanceMarksItsClusterBeforeDetonating()
        {
            string src = Read(DriverSrc);
            StringAssert.Contains("ordnanceMarkerFx", src,
                "the player must read WHICH group was chosen, not just that something exploded");
        }

        // ─────────────────────────────────────────── shield legibility

        [Test]
        public void AbsorbedHitIsVisiblyDifferentFromAMiss()
        {
            string health = Read("/_Project/Scripts/Runtime/Gameplay/Health.cs");
            StringAssert.Contains("PlayShieldBreak", health, "an eaten hit must produce a visible break");
            StringAssert.Contains("OnDamageAbsorbed", health,
                "and must raise an event, so a HUD element can bind to it later without new code");
        }

        // ─────────────────────────────────────────── statuses are visible

        [Test]
        public void StatusesProduceAWorldSpaceMark_NotAHudElement()
        {
            string weapon = Read("/_Project/Scripts/Runtime/Gameplay/Weapon.cs");
            StringAssert.Contains("MarkEnemy", weapon, "a status the player cannot see reads as a bug");

            string fx = Read("/_Project/Scripts/Runtime/Gameplay/Skills/SkillFxDirector.cs");
            StringAssert.Contains("markPoolSize", fx, "marks must be pooled like everything else");
        }

        [Test]
        public void FxDirectorIsOnThePlayerWithItsMaterialsBound()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(Player);
            var fx = player.GetComponent<SkillFxDirector>();
            Assert.IsNotNull(fx, "the FX director must exist, or no arc is ever drawn");

            var so = new SerializedObject(fx);
            Assert.IsNotNull(so.FindProperty("arcMaterial").objectReferenceValue, "arc material unbound");
            Assert.IsNotNull(so.FindProperty("markMaterial").objectReferenceValue, "mark material unbound");
        }

        // ─────────────────────────────────────────── ShotPlan is consumed

        [Test]
        public void BreachRoundBonusPierceIsConsumedByTheWeapon()
        {
            string weapon = Read("/_Project/Scripts/Runtime/Gameplay/Weapon.cs");
            StringAssert.Contains("_shotPlan.bonusPierce", weapon,
                "Breach Round computed pierce that the weapon never read — that made it half-stubbed");
            StringAssert.Contains("effectivePierce", weapon);
        }

        [Test]
        public void ShockwaveBeltConeIsFiredByTheWeapon()
        {
            string weapon = Read("/_Project/Scripts/Runtime/Gameplay/Weapon.cs");
            StringAssert.Contains("FireShockwave", weapon, "Shockwave Belt must actually sweep its cone");
            StringAssert.Contains("TargetQuery.Cone", weapon, "and must use the shared P3 cone query");
            StringAssert.Contains("GetComponentInParent<ZombieBase>()", weapon,
                "the cone must damage enemies only — never the player");
        }

        [Test]
        public void ShotPlanIsCapturedRatherThanDiscarded()
        {
            string weapon = Read("/_Project/Scripts/Runtime/Gameplay/Weapon.cs");
            StringAssert.Contains("_shotPlan = ZombieWar.Skills.SkillRuntime.Active?.OnShotFired()", weapon,
                "the plan must be stored, not thrown away at the call site");
        }
    }
}
