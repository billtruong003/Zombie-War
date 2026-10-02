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
        const string ToonShader = "StylizedToonWorldKit/Toon/Toon Lit";

        [Test]
        public void OutlineLayers_AreNamedRenderingLayers_WithDistinctBits()
        {
            // Bits are looked up by name; every outline layer must exist and own its own bit,
            // and none may be bit 0 (the default every renderer carries).
            var seen = 0u;
            foreach (string name in OutlineLayers.Selected)
            {
                int index = RenderingLayerMask.NameToRenderingLayer(name);
                Assert.Greater(index, 0, $"Rendering layer '{name}' is missing (Tags and Layers ▸ Rendering Layers).");
                uint bit = 1u << index;
                Assert.AreEqual(0u, seen & bit, $"'{name}' shares a bit with another outline layer.");
                seen |= bit;
            }
            Assert.AreEqual(OutlineLayers.SelectionMask, seen);
            Assert.AreEqual(OutlineLayers.WeaponBit | OutlineLayers.EnemyBit | OutlineLayers.PlayerBit,
                GameplayOutlineLayerTool.SelectionRenderingMask);
        }

        [Test]
        public void EnvironmentOutlineBit_MatchesAcrossAssemblies()
        {
            Assert.AreEqual(0u,
                GameplayOutlineLayerTool.EnvironmentOutlineRenderingBit & GameplayOutlineLayerTool.SelectionRenderingMask,
                "Bit vien moi truong chong len mat na chon cua nhan vat.");
            Assert.AreEqual(GameplayOutlineLayerTool.EnvironmentOutlineRenderingBit,
                ZombieWar.WorldStreaming.ChunkInstance.EnvironmentOutlineRenderingBit);
        }

        [Test]
        public void ProductionVolumeProfile_SelectsBothCharacterAndEnvironment()
        {
            // Doc thang YAML cua asset: ca hai asmdef test deu KHONG tham chieu URP.
            const string ProfilePath = GameplayOutlineLayerTool.ProductionProfilePath;
            Assert.IsTrue(System.IO.File.Exists(ProfilePath), "Thieu profile outline cua production.");

            string yaml = System.IO.File.ReadAllText(ProfilePath);
            int marker = yaml.IndexOf("selectionLayer:", System.StringComparison.Ordinal);
            Assert.Greater(marker, 0, "Profile khong co truong selectionLayer.");

            var match = System.Text.RegularExpressions.Regex.Match(
                yaml.Substring(marker), @"m_Bits:\s*(\d+)");
            Assert.IsTrue(match.Success, "Khong doc duoc m_Bits cua selectionLayer.");

            uint mask = uint.Parse(match.Groups[1].Value);
            Assert.AreEqual(GameplayOutlineLayerTool.ProductionSelectionMask, mask,
                $"Mat na chon cua production la {mask}, phai la {GameplayOutlineLayerTool.ProductionSelectionMask}.");
        }

        [Test]
        public void ProductionVolumeProfile_KeepsThePlayerBit_AfterLoading()
        {
            // Regression 2026-10-02: the mask was a GameObject LayerMask, and Unity stripped the
            // player's bit (an unnamed GameObject layer) on load — 30 on disk, 22 in memory.
            AssetDatabase.ImportAsset(GameplayOutlineLayerTool.ProductionProfilePath, ImportAssetOptions.ForceUpdate);
            Object outline = AssetDatabase.LoadAllAssetsAtPath(GameplayOutlineLayerTool.ProductionProfilePath)
                .FirstOrDefault(o => o != null && o.GetType().Name == "OutlineVolume");
            Assert.IsNotNull(outline, "Profile has no OutlineVolume.");
            var bits = new SerializedObject(outline).FindProperty("selectionLayer.m_Value.m_Bits");
            Assert.IsNotNull(bits, "selectionLayer is no longer a rendering-layer mask.");
            Assert.AreNotEqual(0u, bits.uintValue & OutlineLayers.PlayerBit, $"Loaded mask {bits.uintValue} lost the player.");
            Assert.AreEqual(GameplayOutlineLayerTool.ProductionSelectionMask, bits.uintValue);
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
