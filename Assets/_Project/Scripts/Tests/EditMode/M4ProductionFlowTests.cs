using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// EditMode M4 closeout: san pham la MOT the gioi thu tuc vo tan.
    ///
    /// M4 chot huong: khong con tien trinh map-sang-map. `Map_Level1` la the gioi production duy nhat;
    /// Map 2-5 lui ve noi dung khong-production nhung VAN con tren dia de phuc hoi duoc.
    ///
    /// Bo test nay canh dung ranh gioi do. Cac bo test chien dich khac dung catalog TONG HOP nhieu
    /// stage — chung van hop le, vi ban than co che nhieu stage khong bi go bo, chi la khong con
    /// stage nao ngoai Stage 1 duoc bay ra cho nguoi choi. Chi bo test nay doc ASSET THAT.
    public class M4ProductionFlowTests
    {
        private const string CatalogPath = "Assets/_Project/Data/Campaign/CampaignCatalog.asset";
        private const string ScenesDir = "Assets/_Project/Scenes";

        private static readonly string[] RetiredScenes =
            { "Map_Level2", "Map_Level3", "Map_Level4", "Map_Level5" };

        private static CampaignCatalog ProductionCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"Thieu catalog production o {CatalogPath}.");
            return catalog;
        }

        // --- Mot the gioi duy nhat ------------------------------------------------------------------

        [Test]
        public void ProductionCatalog_ExposesExactlyOneEndlessWorld()
        {
            CampaignCatalog catalog = ProductionCatalog();

            Assert.AreEqual(1, catalog.Count,
                "San pham chi con MOT the gioi vo tan. Catalog dang bay ra " + catalog.Count + " stage.");
            Assert.AreEqual("Map_Level1", catalog.Get(0).sceneName);
            Assert.AreEqual("level.1", catalog.Get(0).levelId);
        }

        [Test]
        public void ProductionCatalog_NeverNamesARetiredMap()
        {
            foreach (CampaignLevel level in ProductionCatalog().Levels)
                CollectionAssert.DoesNotContain(RetiredScenes, level.sceneName,
                    $"Catalog van dan toi map da ngung: {level.sceneName}");
        }

        [Test]
        public void BuildSettings_ShipOnlyBootstrapMenuAndMapOne()
        {
            string[] enabled = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => Path.GetFileNameWithoutExtension(s.path))
                .ToArray();

            CollectionAssert.AreEquivalent(new[] { "Bootstrap", "Menu", "Map_Level1" }, enabled,
                "Build settings phai chi con ba scene: Bootstrap, Menu, Map_Level1.");

            foreach (string retired in RetiredScenes)
                CollectionAssert.DoesNotContain(enabled, retired);
        }

        [Test]
        public void RetiredMapScenes_StayOnDiskAndRecoverable()
        {
            // "Go khoi runtime" chu KHONG phai "xoa". Noi dung cua Map 2-5 phai phuc hoi duoc.
            foreach (string retired in RetiredScenes)
                Assert.IsTrue(File.Exists($"{ScenesDir}/{retired}.unity"),
                    $"{retired}.unity da bien mat — no phai o lai dia duoi dang noi dung ngung dung.");
        }

        [Test]
        public void RetiredMapWaveData_StaysAuthored()
        {
            // Cong cu author van ghi WaveData cho ca nam stage; chi catalog moi loc. Nho vay dua mot
            // stage tro lai chi la them mot dong `production = true`.
            foreach (string wave in new[] { "WD_Level2", "WD_Level3", "WD_Level4", "WD_Level5" })
                Assert.IsNotNull(
                    AssetDatabase.LoadAssetAtPath<WaveData>($"Assets/_Project/Data/Waves/{wave}.asset"),
                    $"{wave} bien mat — noi dung stage da ngung khong con phuc hoi duoc.");
        }

        // --- Luong vao game --------------------------------------------------------------------------

        [Test]
        public void GameFlow_DefaultsToTheOneProductionWorld()
        {
            Assert.AreEqual("Map_Level1", GameFlow.DefaultGameplayScene);
            Assert.AreEqual("level.1", GameFlow.DefaultLevelId);
        }

        [Test]
        public void GameFlow_PendingSceneFollowsTheProductionCatalog()
        {
            CampaignLevel only = ProductionCatalog().Get(0);
            GameFlow.SelectLevel(only);
            try
            {
                Assert.AreEqual("Map_Level1", GameFlow.PendingGameplayScene,
                    "Nut CHOI phai nap the gioi production.");
                Assert.AreEqual("level.1", GameFlow.PendingLevelId);
            }
            finally
            {
                GameFlow.SelectLevel(null);
            }
        }

        [Test]
        public void GameFlow_WithNoSelection_StillLandsOnTheProductionWorld()
        {
            // Bam Play thang tu Editor khong di qua hub, nen duong nay phai luon hop le.
            GameFlow.SelectLevel(null);
            Assert.AreEqual("Map_Level1", GameFlow.PendingGameplayScene);
            Assert.AreEqual("level.1", GameFlow.PendingLevelId);
        }

        [Test]
        public void SelectorOpensOnTheOnlyStage_AndNeverOnNoSelection()
        {
            CampaignCatalog catalog = ProductionCatalog();

            // Du profile con nho mot stage da ngung, bo chon phai rot ve Stage 1 chu khong ve rong.
            Assert.AreEqual(0, CampaignSelection.ResolveInitialIndex(catalog, "level.4", 0),
                "Id da luu tro toi stage khong con — bo chon phai rot ve Stage 1.");
            Assert.AreEqual(0, CampaignSelection.ResolveInitialIndex(catalog, "level.1", 0));
            Assert.AreEqual(0, CampaignSelection.ResolveInitialIndex(catalog, "", 0));
            Assert.AreEqual(0, CampaignSelection.HighestPlayableIndex(catalog, 0),
                "Stage 1 luon mo — neu no khoa thi hub khong con nut nao bam duoc.");
        }

        // --- Vu khi: khong con reload ----------------------------------------------------------------

        [Test]
        public void ProductionWeapons_HaveNoMagazineOrReloadContract()
        {
            // Thiet ke da chot: ban lien tuc, khong bang dan, khong reload. Neu mot truong bang dan
            // quay lai WeaponData thi HUD se lai co ly do de ve vong dan, va bai test nay bat truoc.
            System.Type t = typeof(WeaponData);
            foreach (string gone in new[] { "magazineSize", "reloadDuration", "ammo", "clipSize" })
                Assert.IsNull(t.GetField(gone),
                    $"WeaponData lai co truong '{gone}' — thiet ke ban lien tuc khong con dung.");
        }

        [Test]
        public void HudController_ExposesNoReloadWidget()
        {
            System.Type t = typeof(HudController);
            var fields = t.GetFields(System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.NonPublic |
                                     System.Reflection.BindingFlags.Public)
                          .Select(f => f.Name.ToLowerInvariant())
                          .ToArray();

            foreach (string banned in new[] { "reloadbar", "reloadfill", "reloadprogress", "reloadpanel", "magazinelabel" })
                CollectionAssert.DoesNotContain(fields, banned,
                    $"HUD lai co widget reload '{banned}' — vu khi production khong reload.");
        }
    }
}
