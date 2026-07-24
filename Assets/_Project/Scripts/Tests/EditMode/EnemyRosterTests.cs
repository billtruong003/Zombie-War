using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>
    /// Asset-truth tests for the baked enemy roster. These guard the contract the whole VAT
    /// architecture rests on - a production enemy is a MeshRenderer driven by VAT_Animator, never an
    /// Animator or SkinnedMeshRenderer - so a regression in the baker fails here rather than in a
    /// play session.
    /// </summary>
    public class EnemyRosterTests
    {
        const string PrefabDir = "Assets/_Project/Prefabs/Enemies";
        const string DataDir = "Assets/_Project/Data/Zombies";

        static IEnumerable<GameObject> BakedEnemies()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("ENM_")) continue;   // skip the legacy Zombie_VAT baseline
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) yield return go;
            }
        }

        static IEnumerable<ZombieData> RosterData() =>
            AssetDatabase.FindAssets("t:ZombieData", new[] { DataDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ZombieData>)
                .Where(d => d != null && !string.IsNullOrEmpty(d.enemyId));

        [Test]
        public void Roster_HasFifteenBakedEnemies()
        {
            Assert.AreEqual(15, BakedEnemies().Count(),
                "expected the 15 Cute-pack monsters; HUGO is documented as blocked (vertex count)");
        }

        [Test]
        public void EveryEnemyId_IsStableAndUnique()
        {
            var ids = RosterData().Select(d => d.enemyId).ToList();

            CollectionAssert.AllItemsAreUnique(ids);
            foreach (var id in ids)
                Assert.IsTrue(id.StartsWith("enemy."), $"'{id}' does not follow the enemy.<pack>.<name> convention");
        }

        [Test]
        public void NoProductionEnemy_ContainsAnimatorOrSkinnedMesh()
        {
            foreach (var go in BakedEnemies())
            {
                Assert.AreEqual(0, go.GetComponentsInChildren<Animator>(true).Length,
                    $"{go.name} ships an Animator - forbidden by the VAT architecture");
                Assert.AreEqual(0, go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,
                    $"{go.name} ships a SkinnedMeshRenderer - forbidden by the VAT architecture");
            }
        }

        [Test]
        public void EveryEnemy_HasExactlyOneVatVisual()
        {
            foreach (var go in BakedEnemies())
            {
                var vats = go.GetComponentsInChildren<VAT_Animator>(true);

                Assert.AreEqual(1, vats.Length, $"{go.name} must have exactly one VAT_Animator");
                Assert.IsNotNull(vats[0].animationData, $"{go.name} has no VAT_AnimationData");
                Assert.IsTrue(vats[0].animationData.IsValid(), $"{go.name} has invalid VAT data");

                // Exactly one BODY renderer (on Visual, next to the VAT_Animator). The only other
                // renderer permitted is the flat blob shadow.
                Assert.IsNotNull(vats[0].GetComponent<MeshRenderer>(),
                    $"{go.name}: VAT_Animator must sit on the MeshRenderer it drives");

                var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
                Assert.AreEqual(2, renderers.Length,
                    $"{go.name} must have exactly two MeshRenderers: the VAT body and the blob shadow");
            }
        }

        [Test]
        public void EveryVisual_IsUprightAtOrigin()
        {
            foreach (var go in BakedEnemies())
            {
                var visual = go.transform.Find("Visual");
                Assert.IsNotNull(visual, $"{go.name} has no Visual child");
                Assert.AreEqual(Vector3.zero, visual.localPosition, $"{go.name} Visual must sit at the origin");
                // Source art is Z-up, so the Visual is rotated -90 about X to stand it up.
                Assert.AreEqual(0f, Mathf.DeltaAngle(visual.localEulerAngles.x, -90f), 0.01f,
                    $"{go.name} Visual must be rotated -90 on X");
            }
        }

        [Test]
        public void EveryEnemy_HasTexturedToonMaterial()
        {
            foreach (var go in BakedEnemies())
            {
                var mat = go.GetComponentInChildren<VAT_Animator>(true)
                            .GetComponent<MeshRenderer>().sharedMaterial;
                Assert.IsNotNull(mat, $"{go.name} has no material");
                Assert.AreEqual("ZombieWar/VAT/EnemyToon", mat.shader.name, $"{go.name} uses the wrong shader");
                Assert.IsNotNull(mat.GetTexture("_MainTex"), $"{go.name} would render untextured");
                Assert.IsNotNull(mat.GetTexture("_NormalTexture"),
                    $"{go.name} has no animated-normal map - specular would be frozen to the bind pose");
                Assert.AreEqual(1.5f, mat.GetFloat("_SpecSteps"), 0.001f, $"{go.name} has the wrong specular steps");
            }
        }

        [Test]
        public void VatToonShader_HasLightRigContractAndDepthNormals()
        {
            var shader = Shader.Find("ZombieWar/VAT/EnemyToon");
            Assert.IsNotNull(shader, "VAT toon shader missing");

            // Toon-light contract: shadow band + ambient fallback must exist so the shader can
            // light enemies from the ToonLightRig, the URP main light, or a defined ambient.
            foreach (var prop in new[] { "_ShadowTint", "_ShadowThreshold", "_ShadowSoftness", "_AmbientFallback" })
                Assert.IsTrue(shader.FindPropertyIndex(prop) >= 0, $"shader lost property {prop}");

            // Screen-space outline/AO read _CameraNormalsTexture - without this pass VAT enemies
            // would be invisible to every normal-based screen-space effect.
            var mat = new Material(shader);
            Assert.IsTrue(mat.FindPass("DepthNormals") >= 0, "shader lost its DepthNormals pass");
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void NormalMap_MatchesPositionMapFrameLayout()
        {
            foreach (var go in BakedEnemies())
            {
                var vat = go.GetComponentInChildren<VAT_Animator>(true);
                var mat = vat.GetComponent<MeshRenderer>().sharedMaterial;
                var pos = vat.animationData.positionTexture;
                var nrm = (Texture2D)mat.GetTexture("_NormalTexture");

                // Row N of the normal map must describe the same frame as row N of the position map.
                Assert.AreEqual(pos.width, nrm.width, $"{go.name} normal/position width mismatch");
                Assert.AreEqual(pos.height, nrm.height, $"{go.name} normal/position frame-count mismatch");
            }
        }

        [Test]
        public void EveryEnemy_HasColliderAndAgentSized()
        {
            foreach (var go in BakedEnemies())
            {
                var agent = go.GetComponent<NavMeshAgent>();
                var col = go.GetComponent<CapsuleCollider>();

                Assert.IsNotNull(agent, $"{go.name} has no NavMeshAgent");
                Assert.IsNotNull(col, $"{go.name} has no CapsuleCollider");
                Assert.AreEqual(0.35f, agent.radius, 0.001f, $"{go.name} agent radius must be the authored 0.35");
                Assert.AreEqual(0.35f, col.radius, 0.001f, $"{go.name} collider radius must be the authored 0.35");
                Assert.Greater(agent.height, 0.4f, $"{go.name} agent height was not measured");
                // Idle standing height, not the all-frame animation bounds a jump would inflate.
                Assert.Less(agent.height, 5f, $"{go.name} height looks inflated by a jump/pounce frame");
            }
        }

        [Test]
        public void EveryEnemy_ResolvesItsRequiredClips()
        {
            foreach (var data in RosterData())
            {
                var vat = data.vatData;
                Assert.IsNotNull(vat, $"{data.enemyId} has no VAT data");

                foreach (var clip in new[] { data.idleClip, data.moveClip, data.attackClip, data.hitClip, data.deathClip })
                {
                    Assert.IsFalse(string.IsNullOrEmpty(clip), $"{data.enemyId} has an unassigned required clip");
                    Assert.IsTrue(vat.TryGetClipInfo(clip, out _),
                        $"{data.enemyId} references clip '{clip}' that was never baked");
                }
            }
        }

        [Test]
        public void LoopSemantics_AreCorrectPerClipRole()
        {
            foreach (var data in RosterData())
            {
                var vat = data.vatData;

                // Locomotion must loop, or the enemy freezes on the last frame while still walking.
                vat.TryGetClipInfo(data.idleClip, out var idle);
                vat.TryGetClipInfo(data.moveClip, out var move);
                Assert.AreEqual(WrapMode.Loop, idle.wrapMode, $"{data.enemyId} idle must loop");
                Assert.AreEqual(WrapMode.Loop, move.wrapMode, $"{data.enemyId} move must loop");

                // One-shot actions must clamp, or death would restart forever.
                vat.TryGetClipInfo(data.deathClip, out var death);
                vat.TryGetClipInfo(data.attackClip, out var attack);
                Assert.AreEqual(WrapMode.ClampForever, death.wrapMode, $"{data.enemyId} death must not loop");
                Assert.AreEqual(WrapMode.ClampForever, attack.wrapMode, $"{data.enemyId} attack must not loop");
            }
        }

        [Test]
        public void BurrowersHaveTheirBurrowClips()
        {
            foreach (var data in RosterData().Where(d => d.archetype == ZombieArchetype.Burrower
                                                      || d.enemyId == "enemy.cute.mole_rat_king"))
            {
                foreach (var clip in new[] { data.burrowInClip, data.burrowLoopClip, data.burrowOutClip })
                {
                    Assert.IsFalse(string.IsNullOrEmpty(clip), $"{data.enemyId} is a burrower with no burrow clip");
                    Assert.IsTrue(data.vatData.TryGetClipInfo(clip, out _),
                        $"{data.enemyId} references unbaked burrow clip '{clip}'");
                }

                // The underground travel loop must actually loop or the dig would visibly stall.
                data.vatData.TryGetClipInfo(data.burrowLoopClip, out var loop);
                Assert.AreEqual(WrapMode.Loop, loop.wrapMode, $"{data.enemyId} underground loop must loop");
            }
        }

        [Test]
        public void AttackWindupAndRewards_AreAuthored()
        {
            foreach (var data in RosterData())
            {
                Assert.Greater(data.attackWindup, 0f,
                    $"{data.enemyId} has no attack wind-up - damage would land on the first frame");
                Assert.Greater(data.coinReward, 0, $"{data.enemyId} rewards no coin");
                Assert.Greater(data.xpReward, 0, $"{data.enemyId} rewards no xp");
            }
        }

        [Test]
        public void SpecialUsers_HaveTheirSpecialClipBaked()
        {
            foreach (var go in BakedEnemies())
            {
                var zb = go.GetComponent<ZombieBase>();
                // Pouncers and chargers refuse to act without a special clip, so an unbaked one
                // would silently disable the entire ability rather than error.
                if (zb is ZombiePouncer || zb is ZombieCharger)
                {
                    var data = zb.Data;
                    Assert.IsFalse(string.IsNullOrEmpty(data.specialClip),
                        $"{go.name} is a {zb.GetType().Name} with no special clip - its ability would never fire");
                    Assert.IsTrue(data.vatData.TryGetClipInfo(data.specialClip, out _),
                        $"{go.name} references unbaked special clip '{data.specialClip}'");
                }
            }
        }

        [Test]
        public void EveryEnemy_SharesOneBlobShadowMaterialAndPlacement()
        {
            Material shared = null;
            foreach (var go in BakedEnemies())
            {
                var blob = go.transform.Find("ShadowBlob");
                Assert.IsNotNull(blob, $"{go.name} has no ShadowBlob");

                // Same local placement on every enemy, so the shadow always sits under the feet.
                Assert.AreEqual(0f, blob.localPosition.x, 0.001f);
                Assert.AreEqual(0f, blob.localPosition.z, 0.001f);
                Assert.Greater(blob.localPosition.y, 0f, "blob must sit above the ground to avoid z-fighting");
                Assert.AreEqual(90f, blob.localEulerAngles.x, 0.01f, "blob must lie flat");

                var r = blob.GetComponent<MeshRenderer>();
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, r.shadowCastingMode,
                    "a fake shadow must not itself cast one");
                Assert.IsFalse(r.receiveShadows);

                // ONE material across the whole roster - that is what keeps the blobs batching.
                shared ??= r.sharedMaterial;
                Assert.AreSame(shared, r.sharedMaterial,
                    $"{go.name} uses a different blob material; all enemies must share one");
            }
            Assert.IsNotNull(shared, "no enemies were checked");
            Assert.IsTrue(shared.enableInstancing, "the shared blob material must be GPU-instanced");
        }

        [Test]
        public void EveryEnemy_UsesTheSharedLookConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<VatLookConfig>(
                "Assets/_Project/Data/Art/VatLookConfig.asset");
            Assert.IsNotNull(cfg, "VatLookConfig asset is missing");

            foreach (var go in BakedEnemies())
            {
                var mat = go.GetComponentInChildren<VAT_Animator>(true)
                            .GetComponent<MeshRenderer>().sharedMaterial;

                Assert.AreEqual(cfg.specSteps, mat.GetFloat("_SpecSteps"), 0.001f, $"{go.name} spec steps drifted");
                Assert.AreEqual(cfg.specSize, mat.GetFloat("_SpecSize"), 0.001f, $"{go.name} spec size drifted");
                Assert.AreEqual(cfg.specIntensity, mat.GetFloat("_SpecIntensity"), 0.001f, $"{go.name} spec intensity drifted");
                var tiling = mat.GetVector("_DissolveNoiseTiling");
                Assert.AreEqual(cfg.dissolveNoiseTiling.x, tiling.x, 0.001f, $"{go.name} noise tiling X drifted");
                Assert.AreEqual(cfg.dissolveNoiseTiling.y, tiling.y, 0.001f, $"{go.name} noise tiling Y drifted");

                // The toggle must match whether a texture is actually bound, or dissolve pops.
                bool usesTex = mat.GetFloat("_UseNoiseTex") > 0.5f;
                Assert.AreEqual(cfg.dissolveNoise != null, usesTex,
                    $"{go.name} noise-texture toggle disagrees with the bound texture");
                if (usesTex)
                    Assert.AreSame(cfg.dissolveNoise, mat.GetTexture("_DissolveNoiseTex"),
                        $"{go.name} uses a different dissolve noise texture");
            }
        }

        [Test]
        public void EveryEnemy_UsesTheSharedTimings()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<VatLookConfig>(
                "Assets/_Project/Data/Art/VatLookConfig.asset");
            Assert.IsNotNull(cfg);

            foreach (var go in BakedEnemies())
            {
                var so = new SerializedObject(go.GetComponent<ZombieBase>());
                float dissolve = so.FindProperty("dissolveDuration").floatValue;
                float flash = so.FindProperty("hitFlashDuration").floatValue;
                float poolDelay = so.FindProperty("returnToPoolDelay").floatValue;

                Assert.AreEqual(cfg.dissolveDuration, dissolve, 0.001f, $"{go.name} dissolve duration drifted");
                Assert.AreEqual(cfg.hitFlashDuration, flash, 0.001f, $"{go.name} hit flash duration drifted");

                // The corpse must not be recycled before it has finished fading out.
                Assert.GreaterOrEqual(poolDelay, dissolve,
                    $"{go.name} returns to the pool before its dissolve finishes");
            }
        }

        [Test]
        public void RosterCoversEveryBehaviourArchetype()
        {
            var types = BakedEnemies()
                .Select(go => go.GetComponent<ZombieBase>().GetType().Name)
                .Distinct().ToList();

            // Inheritance, not 15 subclasses: 6 behaviours cover the whole roster.
            CollectionAssert.Contains(types, nameof(ZombieWalker));
            CollectionAssert.Contains(types, nameof(ZombiePouncer));
            CollectionAssert.Contains(types, nameof(ZombieRanged));
            CollectionAssert.Contains(types, nameof(ZombieBurrower));
            CollectionAssert.Contains(types, nameof(ZombieBoss));
            CollectionAssert.Contains(types, nameof(ZombieCharger));
        }
    }
}
