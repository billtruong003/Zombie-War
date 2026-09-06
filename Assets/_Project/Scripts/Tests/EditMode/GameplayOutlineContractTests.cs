using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieWar.Editor;

namespace ZombieWar.Tests
{
    public class GameplayOutlineContractTests
    {
        const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";
        const string EnemyFolder = "Assets/_Project/Prefabs/Enemies";
        const string WeaponFolder = "Assets/_Project/Prefabs/Weapons";
        const string BombPrefab = "Assets/_Project/Prefabs/Gameplay/Bomb.prefab";
        const string ToonShader = "StylizedToonWorldKit/Toon/Toon Lit";

        [Test]
        public void SelectionBits_SeparateGenericWeaponsFromDeformedCharacters()
        {
            Assert.AreEqual(2u, GameplayOutlineLayerTool.WeaponRenderingBit);
            Assert.AreEqual(4u, GameplayOutlineLayerTool.EnemyRenderingBit);
            Assert.AreEqual(8u, GameplayOutlineLayerTool.PlayerRenderingBit);
            Assert.AreEqual(14u, GameplayOutlineLayerTool.SelectionRenderingMask);
        }

        [Test]
        public void EnvironmentOutlineBit_IsBitFour_AndDoesNotCollideWithCharacterBits()
        {
            // M4.6CD.1 them vien cho canh vat ran. Bit 0-3 da co chu (mac dinh / vu khi / quai / nguoi
            // choi), nen bit 4 la bit thap nhat con trong — da kiem trong phien gameplay that.
            Assert.AreEqual(16u, GameplayOutlineLayerTool.EnvironmentOutlineRenderingBit);
            Assert.AreEqual(0u,
                GameplayOutlineLayerTool.EnvironmentOutlineRenderingBit & GameplayOutlineLayerTool.SelectionRenderingMask,
                "Bit vien moi truong chong len mat na chon cua nhan vat.");
            Assert.AreEqual(30u, GameplayOutlineLayerTool.ProductionSelectionMask);

            // Hai hang so nam o hai assembly khac nhau; lech nhau thi vien im lang khong hien.
            Assert.AreEqual(GameplayOutlineLayerTool.EnvironmentOutlineRenderingBit,
                ZombieWar.WorldStreaming.ChunkInstance.EnvironmentOutlineRenderingBit);
        }

        [Test]
        public void ProductionVolumeProfile_SelectsBothCharacterAndEnvironment()
        {
            // Doc thang YAML cua asset thay vi nap `VolumeProfile`: ca hai asmdef test deu KHONG tham
            // chieu duoc URP lan assembly `ZombieWar.Rendering`, nen day la duong duy nhat kiem duoc
            // gia tri that ma khong phai noi long kien truc assembly chi de chieu mot bai test.
            const string ProfilePath = "Assets/Settings/SampleSceneProfile.asset";
            Assert.IsTrue(System.IO.File.Exists(ProfilePath), "Thieu profile outline cua production.");

            string yaml = System.IO.File.ReadAllText(ProfilePath);
            int marker = yaml.IndexOf("selectionLayer:", System.StringComparison.Ordinal);
            Assert.Greater(marker, 0, "Profile khong co truong selectionLayer.");

            var match = System.Text.RegularExpressions.Regex.Match(
                yaml.Substring(marker), @"m_Bits:\s*(\d+)");
            Assert.IsTrue(match.Success, "Khong doc duoc m_Bits cua selectionLayer.");

            uint mask = uint.Parse(match.Groups[1].Value);
            Assert.AreEqual(GameplayOutlineLayerTool.ProductionSelectionMask, mask,
                $"Mat na chon cua production la {mask}, phai la 30 (nhan vat 14 + moi truong 16).");
        }

        [Test]
        public void PlayerPrefabBody_UsesDedicatedGenericPlayerBit()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            Assert.IsNotNull(player);

            Renderer[] bodies = player.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == ToonShader)
                .ToArray();

            Assert.AreEqual(5, bodies.Length, "Player body-part renderer contract changed");
            foreach (Renderer body in bodies)
            {
                Assert.IsInstanceOf<SkinnedMeshRenderer>(body);
                Assert.AreNotEqual(0u, body.renderingLayerMask & GameplayOutlineLayerTool.PlayerRenderingBit,
                    $"{body.name} lost the skinned-player selection bit");
                Assert.AreEqual(0u, body.renderingLayerMask &
                    (GameplayOutlineLayerTool.WeaponRenderingBit | GameplayOutlineLayerTool.EnemyRenderingBit));
            }
        }

        [Test]
        public void VatEnemies_UseEnemyBitAndExcludeBlobShadows()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyFolder }))
            {
                GameObject enemy = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (enemy == null || enemy.GetComponent<ZombieWar.ZombieBase>() == null) continue;

                foreach (Renderer renderer in enemy.GetComponentsInChildren<Renderer>(true))
                {
                    bool blob = renderer.sharedMaterial != null &&
                                renderer.sharedMaterial.shader.name == "Universal Render Pipeline/Unlit";
                    uint selected = renderer.renderingLayerMask & GameplayOutlineLayerTool.SelectionRenderingMask;
                    if (blob)
                        Assert.AreEqual(0u, selected, $"{enemy.name}/{renderer.name} blob shadow is selected");
                    else
                    {
                        Assert.AreNotEqual(0u, selected & GameplayOutlineLayerTool.EnemyRenderingBit,
                            $"{enemy.name}/{renderer.name} lost the VAT enemy bit");
                        Assert.GreaterOrEqual(renderer.sharedMaterial.FindPass("OutlineSelectionMask"), 0,
                            $"{enemy.name}/{renderer.name} lost its VAT-aware mask pass");
                    }
                }
            }
        }

        [Test]
        public void EquippedWeapons_RemainOnGenericMaskOnly()
        {
            int prefabs = 0;
            int renderers = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { WeaponFolder }))
            {
                GameObject weapon = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (weapon == null) continue;
                prefabs++;
                foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>(true))
                {
                    renderers++;
                    Assert.AreNotEqual(0u, renderer.renderingLayerMask & GameplayOutlineLayerTool.WeaponRenderingBit);
                    Assert.AreEqual(0u, renderer.renderingLayerMask &
                        (GameplayOutlineLayerTool.PlayerRenderingBit | GameplayOutlineLayerTool.EnemyRenderingBit));
                }
            }

            // These were frozen at 25 prefabs / 178 renderers — a snapshot of the arsenal at the time.
            // M7.1 onboarded 29 more bodies, and freezing the count would mean every future weapon
            // breaks an unrelated rendering test, which is exactly the "weapon 26 costs a consumer
            // edit" problem the catalog exists to remove.
            //
            // The real contract is the per-renderer mask assertions above, and they are unchanged and
            // still strict. What is asserted here is COVERAGE: the sweep must have actually walked the
            // arsenal, so a silently-empty scan cannot pass.
            int weaponDataCount = AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/_Project/Data/Weapons" }).Length;
            Assert.AreEqual(weaponDataCount, prefabs,
                "every WeaponData's prefab must be present in the weapon prefab folder and swept");
            Assert.Greater(renderers, 0, "the sweep found no renderers at all");
            Assert.GreaterOrEqual(renderers, prefabs, "each weapon prefab must contribute at least one renderer");
        }

        [Test]
        public void BombRenderers_StayOnDefaultRenderingLayerOnly()
        {
            GameObject bomb = AssetDatabase.LoadAssetAtPath<GameObject>(BombPrefab);
            Assert.IsNotNull(bomb);
            Renderer[] renderers = bomb.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            foreach (Renderer renderer in renderers)
                Assert.AreEqual(1u, renderer.renderingLayerMask, $"Bomb/{renderer.name} gained a selection bit");
        }

        [Test]
        public void Migration_IsIdempotentAndPhysicsSafe()
        {
            string result = GameplayOutlineLayerTool.Run();
            StringAssert.Contains("prefabsChanged=0", result);
            StringAssert.Contains("renderersTagged=0", result);
            StringAssert.Contains("weaponPrefabsChanged=0", result);
            StringAssert.Contains("weaponRenderersTagged=0", result);

            string verification = GameplayOutlineLayerTool.Verify();
            StringAssert.Contains("notSelected=0", verification);
            StringAssert.Contains("shadowBlobsWronglySelected=0", verification);
            StringAssert.Contains("physicsLayerDrift=0", verification);
            StringAssert.Contains("legacyAllBits=0", verification);
        }
    }
}
