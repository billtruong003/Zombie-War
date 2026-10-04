using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// G11 (04/10): some screen text was baked from the economy data when the prefab was built
    /// (Daily tiles, the Pass premium price), so changed data would keep showing the old number.
    /// The prefabs are the owner's; these tests read them and fail when their text drifts.
    public class BakedCopyTests
    {
        const string PrefabPath = "Assets/_Project/UI/Prefabs/V2/UI_V2_Daily.prefab";
        const string PassPath = "Assets/_Project/UI/Prefabs/V2/UI_V2_Pass.prefab";

        static string TextAt(GameObject root, string path)
        {
            var t = Find(root.transform, path);
            Assert.IsNotNull(t, $"{path} missing in {root.name}");
            foreach (var c in t.GetComponents<Component>())
            {
                var prop = c != null ? c.GetType().GetProperty("text") : null;
                if (prop != null && prop.PropertyType == typeof(string)) return (string)prop.GetValue(c);
            }
            Assert.Fail($"{path} has no text");
            return null;
        }

        static Transform Find(Transform t, string path)
        {
            var hit = t.Find(path);
            if (hit != null) return hit;
            for (int i = 0; i < t.childCount; i++) if ((hit = Find(t.GetChild(i), path)) != null) return hit;
            return null;
        }

        [Test]
        public void TileAmounts_MatchTheRewardTable()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            for (int day = 1; day < DailyRewards.WelcomeDays; day++)
                Assert.AreEqual(DailyRewards.WelcomeReward(day).label, TextAt(prefab, $"Day{day}/Amount"), $"welcome day {day}");
            Assert.AreEqual(DailyRewards.WelcomeReward(DailyRewards.WelcomeDays).label, TextAt(prefab, $"Day{DailyRewards.WelcomeDays}/What"));
            foreach (int stamp in new[] { 7, 14, 21 })
                Assert.AreEqual(DailyRewards.StampReward(stamp).label, TextAt(prefab, $"M{stamp}/A"), $"stamp {stamp}");
        }

        [Test]
        public void PassPremiumButton_ShowsThePriceItCharges()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PassPath);
            Assert.IsNotNull(prefab);
            var screen = prefab.GetComponentInChildren<ZombieWar.UI.PassScreenV2>(true);
            var button = (UnityEngine.UI.Button)typeof(ZombieWar.UI.PassScreenV2)
                .GetField("premiumButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(screen);
            Assert.IsNotNull(button);
            string label = null;
            foreach (var c in button.GetComponentsInChildren<Component>(true))
            {
                var prop = c != null ? c.GetType().GetProperty("text") : null;
                if (prop != null && prop.PropertyType == typeof(string) && c.GetType().Name.StartsWith("TextMeshPro")) { label = (string)prop.GetValue(c); break; }
            }
            StringAssert.Contains(PassRewards.PremiumPrice, label);
        }
    }
}
