using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using ZombieWar.Skills;
using ZombieWar.Skills.Powers;

namespace ZombieWar.Tests
{
    /// <summary>Phase A2: powers are one module each and read their assets from the FX library.</summary>
    public class PhaseA2Tests
    {
        const string LibraryPath = "Assets/_Project/Data/Skills/SkillFxLibrary.asset";

        [Test]
        public void EveryAssetInTheFxLibraryIsBound()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SkillFxLibrary>(LibraryPath);
            Assert.IsNotNull(lib, "the FX library is missing");
            foreach (var section in typeof(SkillFxLibrary).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = section.GetValue(lib);
                Assert.IsNotNull(value, $"section {section.Name} is null");
                foreach (var f in section.FieldType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType)) continue;
                    var o = f.GetValue(value) as UnityEngine.Object;
                    Assert.IsTrue(o != null, $"{section.Name}.{f.Name} is unbound: the power would show nothing");
                }
            }
        }

        [Test]
        public void EveryModuleBuildsWithoutAHost()
        {
            // Modules are plain classes: constructing one must not touch the scene (the host attaches later).
            foreach (var t in typeof(PowerModule).Assembly.GetTypes())
            {
                if (t.IsAbstract || !typeof(PowerModule).IsAssignableFrom(t)) continue;
                Assert.DoesNotThrow(() => System.Activator.CreateInstance(t), t.Name);
            }
        }

        [Test]
        public void ProcPowersFireOnTheirTableCooldown()
        {
            // Chain Lightning at rank 5 (cd 4 s). The A2 bench measured one proc fewer per 20 s before
            // the module refactor; this pins the spacing to the card's table.
            var run = new SkillRuntime { UnlockLevel = int.MaxValue };
            for (int i = 0; i < 5; i++) run.Take(SkillCatalogDefs.AutoChainLightning);
            AutonomousPower.ResetGlobalBudget();
            int procs = 0;
            for (float t = 100f; t <= 121f; t += 0.05f)
                foreach (var p in run.PollPowers(t, 1f))
                    if (p.skillId == SkillCatalogDefs.AutoChainLightning) procs++;
            Assert.AreEqual(6, procs, "t = 100, 104, … 120: six procs on a 4 s cooldown");
        }
    }
}
