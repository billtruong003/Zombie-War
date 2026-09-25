using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace ZombieWar.Tests
{
    /// <summary>
    /// The shipped product is ONE endless world (GDD §1, M6 lock). These guard the boundaries that
    /// keep the retired campaign, wave and input designs from creeping back in.
    /// </summary>
    public class EndlessFlowTests
    {
        private const string ScenesDir = "Assets/_Project/Scenes";

        private static readonly string[] RetiredScenes =
            { "Map_Level2", "Map_Level3", "Map_Level4", "Map_Level5", "Map_GenTest" };

        [Test]
        public void BuildSettings_ShipOnlyBootstrapMenuAndTheWorld()
        {
            string[] enabled = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => Path.GetFileNameWithoutExtension(s.path))
                .ToArray();

            CollectionAssert.AreEquivalent(new[] { "Bootstrap", "Menu", GameFlow.GameplayScene }, enabled);
        }

        [Test]
        public void RetiredCampaignContent_IsGone()
        {
            foreach (string retired in RetiredScenes)
                Assert.IsFalse(File.Exists($"{ScenesDir}/{retired}.unity"), $"{retired}.unity is back.");
            Assert.IsFalse(Directory.Exists("Assets/_Project/Data/Waves"), "wave data is back");
            Assert.IsFalse(Directory.Exists("Assets/_Project/Data/Campaign"), "the campaign catalog is back");
        }

        [Test]
        public void RunOutcomes_HaveNoVictory()
        {
            CollectionAssert.AreEquivalent(
                new[] { "InProgress", "Died", "Abandoned" },
                System.Enum.GetNames(typeof(RunOutcome)),
                "an endless run ends by death or by walking away - nothing else");
        }

        [Test]
        public void ProductionWeapons_HaveNoMagazineOrReloadContract()
        {
            // Continuous fire, no magazine, no reload. If a magazine field returns the HUD would
            // have a reason to draw an ammo ring again, and this catches it first.
            System.Type t = typeof(WeaponData);
            foreach (string gone in new[] { "magazineSize", "reloadDuration", "ammo", "clipSize" })
                Assert.IsNull(t.GetField(gone), $"WeaponData has '{gone}' again.");
        }

        [Test]
        public void Hud_OffersNoFireReloadBombOrSwitchAction()
        {
            // The only inputs are movement, the level-up card and one context action (GDD §1).
            string src = File.ReadAllText("Assets/_Project/Scripts/Runtime/UI/HudController.cs");
            foreach (string banned in new[] { "TryThrow", "SwitchWeapon", "TryFire", "Reload" })
                StringAssert.DoesNotContain(banned, src, $"the HUD drives '{banned}' again");
        }
    }
}
