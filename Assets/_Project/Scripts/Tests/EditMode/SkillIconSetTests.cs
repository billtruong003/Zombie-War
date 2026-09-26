using NUnit.Framework;
using ZombieWar.UI;

namespace ZombieWar.Tests
{
    public class SkillIconSetTests
    {
        [Test]
        public void FallbackBadgeIsTwoLetters()
        {
            Assert.AreEqual("OB", SkillIconSet.Abbreviation("Orbit Blades"));
            Assert.AreEqual("AI", SkillIconSet.Abbreviation("Airstrike"));
            Assert.AreEqual("RG", SkillIconSet.Abbreviation("Run & Gun"));
            Assert.AreEqual("?", SkillIconSet.Abbreviation(""));
        }

        [Test]
        public void EveryCardHasADistinctBadgeWithinItsLayer()
        {
            var seen = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var d in ZombieWar.Skills.SkillCatalogDefs.All)
            {
                if (d.layer != ZombieWar.Skills.SkillLayer.Autonomous) continue;
                string key = SkillIconSet.Abbreviation(d.displayName);
                Assert.IsFalse(seen.ContainsKey(key), $"{d.displayName} and {(seen.ContainsKey(key) ? seen[key] : "")} share the badge {key} on the skill bar");
                seen[key] = d.displayName;
            }
        }
    }
}
