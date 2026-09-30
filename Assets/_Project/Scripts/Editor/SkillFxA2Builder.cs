using UnityEditor;
using UnityEngine;
using ZombieWar.Skills.Powers;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Phase A2 FX pass (2026-09-30): the toon shockwave material, and the Nova variants that give
    /// Soul Burst and Emergency Detonation the same family of effect as Frost Nova — flat, sized to
    /// the hitbox, with the screen-wide smoke of the pack prefabs switched off. Idempotent.
    /// </summary>
    public static class SkillFxA2Builder
    {
        const string Etfx = "Assets/ThirdParty/Epic Toon FX/Prefabs/";
        const string FxDir = "Assets/_Project/Prefabs/FX/";
        const string MatDir = "Assets/_Project/Art/Materials/FX/";
        const string PackTex = "Assets/_Project/Art/Textures/FX/tex_fx_toon_pack.png";

        [MenuItem("HordeCall/Skills/Build A2 FX (novas + shockwave)")]
        public static string Build()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SkillFxLibrary>(SkillFxLibraryMigration.LibraryPath);
            if (lib == null) return "missing library";

            // The packed mask texture is data (four masks), not colour: linear, tiling for the noise.
            if (AssetImporter.GetAtPath(PackTex) is TextureImporter ti)
            {
                ti.sRGBTexture = false;
                ti.alphaIsTransparency = false;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.mipmapEnabled = true;
                ti.maxTextureSize = 256;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }

            var shader = Shader.Find("ZombieWar/FX/ToonErode");
            if (shader == null) return "ToonErode shader not found";
            System.IO.Directory.CreateDirectory(MatDir);
            string matPath = MatDir + "M_FX_ToonShockwave.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = shader;
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(PackTex));
            mat.SetVector("_ShapeMask", new Vector4(0f, 0f, 1f, 0f));   // the ring channel
            mat.SetFloat("_NoiseScale", 2f);
            mat.SetVector("_NoiseScroll", new Vector4(0.05f, 0.35f, 0f, 0f));
            mat.SetFloat("_NoiseAmount", 0.55f);
            mat.SetFloat("_Distort", 0.06f);
            mat.SetFloat("_Erode", 0.05f);
            mat.SetFloat("_EdgeWidth", 0.04f);   // thin: the element colour must lead, white only rims it
            mat.SetColor("_EdgeColor", new Color(1f, 1f, 1f, 1f));
            mat.SetFloat("_Intensity", 1.1f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);

            var soul = Variant(Etfx + "Combat/Nova/Standard/NovaGreen.prefab", FxDir + "NovaSoul_M8.prefab");
            var emergency = Variant(Etfx + "Combat/Nova/Fire/NovaFireRed.prefab", FxDir + "NovaEmergency_M8.prefab",
                                    "SmokeNova", "Smoke");

            lib.shared.shockwaveMaterial = mat;
            lib.selfBurst.soulNovaFx = soul;
            lib.selfBurst.emergencyNovaFx = emergency;
            lib.selfBurst.emergencyFx = Fx("Combat/Explosions/FireballSharpExplosion/ExplosionFireballSharpFire");
            lib.ordnance.decalFx = lib.airstrike.decalFx;
            // Fire Trail: the cartoon ground-fire field reads as one burning strip (the round flames
            // read as a row of candles).
            lib.fireTrail.patchFx = Fx("Environment/Fire/Cartoon/Field/FireFieldRed");
            lib.fireTrail.nativeRadius = 1f;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return "built M_FX_ToonShockwave, NovaSoul_M8, NovaEmergency_M8, fire field";
        }

        /// <summary>Phase A3: effects for the gun modifiers, poison, the Launcher cards and Guardian Angel,
        /// all from the Epic Toon FX pack the rest of the skills use.</summary>
        [MenuItem("HordeCall/Skills/Build A3 FX (gun mods)")]
        public static string BuildA3()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SkillFxLibrary>(SkillFxLibraryMigration.LibraryPath);
            if (lib == null) return "missing library";
            lib.gunMods.ricochetTracer = lib.drone.tracer;
            lib.gunMods.ricochetSparkFx = Fx("Combat/Explosions (Misc)/SparkExplosion");
            lib.gunMods.explosiveFx = Fx("Combat/Explosions/SmallExplosion/SmallExplosionFire");
            lib.gunMods.critPopFx = Fx("Combat/Explosions/SparkleExplosion/SparkleExplosionYellow");   // the pack's text pops render as solid quads at small scale
            lib.gunMods.siphonWispFx = Fx("Combat/Missiles/Soul/SoulMissileCrimson");
            lib.gunMods.siphonHealFx = Fx("Interactive/Healing/HealOnceBurst");
            lib.poison.tickFx = Fx("Combat/Explosions (Misc)/PoisonExplosionSoft");
            lib.launcher.bombletFx = Fx("Combat/Explosions/SmallExplosion/SmallExplosionFire");
            lib.launcher.bombletNativeRadius = 1f;
            lib.launcher.napalmFx = lib.fireTrail.patchFx;
            lib.launcher.napalmNativeRadius = 1.4f;   // smaller than the hit area: a launcher fires every second and the fields overlap
            lib.guardian.healFx = Fx("Interactive/Healing/HealBig");
            lib.guardian.novaFx = lib.shared.evolveFx;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return "A3 FX bound";
        }

        /// <summary>Phase A5: effects and models for the five v2 powers.</summary>
        [MenuItem("HordeCall/Skills/Build A5 FX (v2 powers)")]
        public static string BuildA5()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SkillFxLibrary>(SkillFxLibraryMigration.LibraryPath);
            if (lib == null) return "missing library";
            lib.toxic.canisterFx = Fx("Combat/Missiles/Gas/GasMissileGreen");
            lib.toxic.burstFx = Fx("Combat/Explosions (Misc)/PoisonExplosion2");
            lib.toxic.cloudFx = Fx("Environment/Smoke/StinkyCloud");
            lib.gravity.vortexFx = Fx("Combat/Magic/Charge/MagicChargeBlue");
            lib.gravity.vortexNativeRadius = 9f;   // the charge-up spreads its rays far past its core
            lib.gravity.popFx = Fx("Combat/Explosions/NovaSmallExplosion/ExplosionNovaSmallPink");
            lib.thorns.hitFx = Fx("Combat/Explosions/SpikyExplosion/SpikyExplosionPink");
            lib.turret.model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Skills/SK_Turret.prefab");
            lib.turret.tracer = lib.drone.tracer;
            lib.turret.muzzleFx = Fx("Combat/Muzzleflash/FireballMuzzle/MuzzleFireballFire");
            lib.turret.hitFx = Fx("Combat/Explosions/BulletExplosion/BulletExplosionFire");
            lib.turret.dropFx = Fx("Environment/Dust/DustDirtyPoof");
            lib.meteor.fallFx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Skills/SK_MeteorFall.prefab")?.GetComponent<ParticleSystem>();
            lib.meteor.impactFx = Fx("Combat/Explosions/FireballRoundExplosion/ExplosionFireballFire");
            lib.meteor.craterFx = lib.airstrike.decalFx;
            lib.meteor.burnFx = lib.fireTrail.patchFx;
            lib.meteor.burnNativeRadius = 1.3f;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return "A5 FX bound";
        }

        /// <summary>Phase A6: effects and models for the eight v3 powers.</summary>
        [MenuItem("HordeCall/Skills/Build A6 FX (v3 powers)")]
        public static string BuildA6()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SkillFxLibrary>(SkillFxLibraryMigration.LibraryPath);
            if (lib == null) return "missing library";
            const string P = "Assets/_Project/Prefabs/Skills/";
            lib.stormCloud.model = AssetDatabase.LoadAssetAtPath<GameObject>(P + "SK_StormCloud.prefab");
            lib.stormCloud.strikeFx = lib.chain.skyStrikeFx;
            lib.stormCloud.modelScale = 1.1f;   // at 2.2 it hid a third of the screen from the top-down camera
            lib.stormCloud.height = 3.4f;
            lib.iceShards.shardFx = AssetDatabase.LoadAssetAtPath<GameObject>(P + "SK_IceShard.prefab")?.GetComponent<ParticleSystem>();
            lib.iceShards.hitFx = Fx("Combat/Explosions/SnowExplosion/SnowExplosion");
            lib.iceShards.castFx = Fx("Combat/Explosions/FrostExplosion/FrostExplosion");
            lib.flameBurst.jetFx = Fx("Combat/Flamethrower/Cartoon/FlamethrowerToonyFire");
            lib.flameBurst.jetNativeRange = 2.2f;
            lib.flameBurst.burnFx = Fx("Combat/Explosions/SmallExplosion/SmallExplosionFire");
            lib.axe.apex = 2f;
            lib.landmine.model = AssetDatabase.LoadAssetAtPath<GameObject>(P + "SK_Mine.prefab");
            lib.landmine.blastFx = Fx("Combat/Explosions/FireballSharpExplosion/ExplosionFireballSharpFire");
            lib.axe.model = AssetDatabase.LoadAssetAtPath<GameObject>(P + "SK_Axe.prefab");
            lib.axe.hitFx = Fx("Combat/Sword/Hit/SwordHit/SwordHitYellow");
            lib.warDog.body = AssetDatabase.LoadAssetAtPath<GameObject>(P + "SK_WarDogBody.prefab");
            lib.warDog.biteFx = Fx("Combat/Brawling/RoundHit/RoundHitRed");
            lib.stomp.dustFx = Fx("Environment/Dust/DustDirtyPoof");
            lib.stomp.crackFx = Fx("Combat/Explosions (Misc)/HitDustExplosion");
            lib.timeWarp.circleFx = Fx("Combat/Magic/Circle/MagicCircleBlue");
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return "A6 FX bound";
        }

        /// <summary>Phase A7: the chest pickup (KayKit's gem chest in the pack's glow rays, with the
        /// pack's appear burst playing whenever the pool hands it out) and the Fortress rocket.</summary>
        [MenuItem("HordeCall/Skills/Build A7 (chest pickup)")]
        public static string BuildA7()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SkillFxLibrary>(SkillFxLibraryMigration.LibraryPath);
            if (lib == null) return "missing library";
            lib.turret.rocketBlastFx = Fx("Combat/Explosions/SmallExplosion/SmallExplosionFire");
            EditorUtility.SetDirty(lib);

            const string chestModel = "Assets/KayKit/Packs/Bits/KayKit - Resource Bits (for Unity)/Prefabs/Gems_Chest.prefab";
            const string path = "Assets/_Project/Resources/Pools/pickup_chest.prefab";
            var root = new GameObject("pickup_chest");
            try
            {
                var pickup = root.AddComponent<ZombieWar.Pickup>();
                var so = new SerializedObject(pickup);
                so.FindProperty("effect").intValue = (int)ZombieWar.PickupEffect.Chest;
                so.FindProperty("spinSpeed").floatValue = 35f;
                so.FindProperty("bobHeight").floatValue = 0.06f;
                so.FindProperty("settleTime").floatValue = 0.6f;
                so.ApplyModifiedPropertiesWithoutUndo();

                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(chestModel), root.transform);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Chest";
                model.transform.localPosition = Vector3.zero;
                model.transform.localScale = Vector3.one * 0.7f;   // ~1.1 m: reads as a prize next to a 1.8 m player
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                Child(root, Fx("Interactive/Loot/TreasureChestGlowRays"), "Glow", 0.8f, true);
                Child(root, Fx("Interactive/Loot/ChestAppear/ChestAppearYellow"), "Appear", 1f, true);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            return "A7 chest pickup + Fortress rocket bound";
        }

        /// <summary>Phase A8: the Magnet, Bomb and Freeze Clock items (models from
        /// Review/M8/skill_models.blend on the shared palette, in the pack's item glow and sparkle),
        /// and what each plays when it is taken.</summary>
        [MenuItem("HordeCall/Skills/Build A8 (mechanic items)")]
        public static string BuildA8()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SkillFxLibrary>(SkillFxLibraryMigration.LibraryPath);
            if (lib == null) return "missing library";
            var pal = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Skills/M_SK_Palette.mat");
            if (pal == null) return "missing M_SK_Palette (run Build Skill Models)";

            lib.items.magnetFx = Fx("Interactive/Loot/ItemSparkleBurst/ItemSparkleBurstPink");
            lib.items.bombFx = Fx("Combat/Explosions/FireballSharpExplosion/ExplosionFireballSharpFire");
            lib.items.bombHitFx = Fx("Combat/Explosions/SmallExplosion/SmallExplosionFire");
            lib.items.freezeFx = Fx("Combat/Explosions/FrostExplosion/FrostExplosion");
            lib.items.freezeHitFx = Fx("Combat/Explosions/SnowExplosion/SnowExplosion");
            EditorUtility.SetDirty(lib);

            string built = "";
            // Magnet and clock are upright shapes: from the top-down camera they read as a bar, so
            // they lean back 60 degrees and keep their face to the sky while they spin.
            foreach (var (model, key, effect, colour, tilt) in new[]
            {
                ("PK_Magnet", "pickup_magnet", ZombieWar.PickupEffect.Magnet, "Pink", -60f),
                ("PK_Bomb", "pickup_bomb", ZombieWar.PickupEffect.Bomb, "Yellow", 0f),
                ("PK_Stopwatch", "pickup_freeze", ZombieWar.PickupEffect.Freeze, "Blue", -60f),
            })
            {
                string fbx = "Assets/_Project/Art/Models/Pickups/" + model + ".fbx";
                if (AssetImporter.GetAtPath(fbx) is ModelImporter mi)
                {
                    mi.materialImportMode = ModelImporterMaterialImportMode.None;
                    mi.importNormals = ModelImporterNormals.Import;
                    mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None;
                    mi.SaveAndReimport();
                }
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (src == null) return "missing " + fbx;

                var root = new GameObject(key);
                try
                {
                    var pickup = root.AddComponent<ZombieWar.Pickup>();
                    var so = new SerializedObject(pickup);
                    so.FindProperty("effect").intValue = (int)effect;
                    so.FindProperty("spinSpeed").floatValue = 90f;
                    so.FindProperty("bobHeight").floatValue = 0.15f;
                    so.FindProperty("bobSpeed").floatValue = 2.4f;
                    so.FindProperty("settleTime").floatValue = 0.4f;
                    so.FindProperty("lifetime").floatValue = 45f;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    var m = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
                    PrefabUtility.UnpackPrefabInstance(m, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    m.name = "Model";
                    // Models sit on their base: tilt about the model's centre (not its base), so the
                    // spin does not swing it round in a circle.
                    const float scale = 1.7f;
                    var tiltRot = Quaternion.Euler(tilt, 0f, 0f);
                    float centre = src.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.center.y * scale;
                    m.transform.localRotation = tiltRot;
                    m.transform.localPosition = tilt != 0f ? new Vector3(0f, 0.55f, 0f) - tiltRot * new Vector3(0f, centre, 0f)
                                                           : new Vector3(0f, 0.15f, 0f);
                    m.transform.localScale = Vector3.one * scale;   // ~0.9 m: an item, clearly smaller than the chest
                    foreach (var r in m.GetComponentsInChildren<Renderer>(true))
                    {
                        r.sharedMaterials = new[] { pal };
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                    Child(root, Fx("Interactive/Loot/GlowOrb/GlowOrb" + colour), "Glow", 1f, true);
                    Child(root, Fx("Interactive/Loot/ItemSparkle/ItemSparkle" + colour), "Sparkle", 0.8f, true);
                    PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Project/Resources/Pools/" + key + ".prefab");
                    built += key + " ";
                }
                finally { Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            return "A8 built " + built;
        }

        static void Child(GameObject root, ParticleSystem fx, string name, float scale, bool playOnEnable)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fx.gameObject, root.transform);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * scale;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { var m = ps.main; m.playOnAwake = playOnEnable; }
        }

        /// <summary>
        /// VFX audit (2026-09-30): every transient ring, telegraph and cone comes from Epic Toon FX
        /// instead of our own line/disc shaders. Flat variants of the pack's novas (one per colour
        /// family), the magic circle for blast telegraphs, the sword wave for the Shockwave Belt, and
        /// Epic Toon's lightning material on the chain arc. Native radii are measured from the
        /// simulated particles so each effect can be sized to the radius a power checks. Idempotent.
        /// </summary>
        [MenuItem("HordeCall/Skills/Build VFX audit (Epic Toon rings)")]
        public static string BuildEpicToonRings()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SkillFxLibrary>(SkillFxLibraryMigration.LibraryPath);
            if (lib == null) return "missing library";
            var sh = lib.shared;
            var log = new System.Text.StringBuilder();

            Set(sh.novaFire, Variant(Etfx + "Combat/Nova/Standard/NovaFire.prefab", FxDir + "NovaFire_M8.prefab"), log);
            Set(sh.novaBlue, Variant(Etfx + "Combat/Nova/Standard/NovaBlue.prefab", FxDir + "NovaBlue_M8.prefab"), log);
            Set(sh.novaGreen, Variant(Etfx + "Combat/Nova/Standard/NovaGreen.prefab", FxDir + "NovaGreen_M8.prefab"), log);
            Set(sh.novaPink, Variant(Etfx + "Combat/Nova/Standard/NovaPink.prefab", FxDir + "NovaPink_M8.prefab"), log);
            Set(sh.novaYellow, Variant(Etfx + "Combat/Nova/Lightning/NovaLightningYellow.prefab", FxDir + "NovaYellow_M8.prefab"), log);
            Set(sh.novaFrost, lib.frost.fx != null ? lib.frost.fx : Variant(Etfx + "Combat/Nova/Frost/NovaFrost.prefab", FxDir + "NovaFrost_M8.prefab"), log);
            Set(sh.telegraph, Fx("Combat/Magic/Circle Simple/MagicCircleSimpleYellow"), log);
            Set(sh.cone, Fx("Combat/Sword/Wave/SwordWaveYellow"), log);
            sh.dustFx = Fx("Environment/Dust/DustDirtyPoof");
            sh.healBurstFx = Fx("Interactive/Healing/HealOnceBurst");
            EditorUtility.SetDirty(lib);

            // The chain arc: Epic Toon's own lightning material (the texture is the bolt).
            const string playerPath = "Assets/_Project/Prefabs/Player.prefab";
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            var director = player != null ? player.GetComponentInChildren<ZombieWar.Skills.SkillFxDirector>(true) : null;
            if (director != null)
            {
                var so = new SerializedObject(director);
                so.FindProperty("arcMaterial").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/ThirdParty/Epic Toon FX/Materials/Misc/Lightning/lightning1_ADD.mat");
                so.FindProperty("arcWidth").floatValue = 1.5f;   // the bolt fills the middle third of the texture; 1.5 reads across a crowd
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(player);
                log.Append("arc: lightning1_ADD");
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static void Set(SkillFxLibrary.Nova n, ParticleSystem fx, System.Text.StringBuilder log)
        {
            n.fx = fx;
            n.nativeRadius = MeasureRadius(fx);
            log.Append(fx.name).Append(" r=").Append(n.nativeRadius.ToString("0.00")).Append("; ");
        }

        /// The furthest a particle's edge reaches from the effect's centre on the ground plane over its
        /// whole life, at scale 1, as it plays in the game (with its authored rotation).
        static float MeasureRadius(ParticleSystem prefab)
        {
            var go = Object.Instantiate(prefab.gameObject);
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, prefab.transform.localRotation);
                var root = go.GetComponent<ParticleSystem>();
                var systems = go.GetComponentsInChildren<ParticleSystem>(true);
                var buf = new ParticleSystem.Particle[512];
                float best = 0f;
                for (float t = 0.05f; t <= 2f; t += 0.05f)
                {
                    root.Simulate(t, true, true, true);
                    foreach (var ps in systems)
                    {
                        if (!ps.gameObject.activeInHierarchy) continue;
                        bool local = ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;
                        int n = ps.GetParticles(buf);
                        for (int i = 0; i < n; i++)
                        {
                            Vector3 p = local ? ps.transform.TransformPoint(buf[i].position) : buf[i].position;
                            float half = buf[i].GetCurrentSize3D(ps).x * 0.5f * ps.transform.lossyScale.x;
                            best = Mathf.Max(best, new Vector2(p.x, p.z).magnitude + half);
                        }
                    }
                }
                return Mathf.Max(0.5f, best);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// A prefab variant lying flat (the pack's novas are authored upright) with the named children
        /// switched off.
        static ParticleSystem Variant(string source, string path, params string[] hide)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (src == null) throw new System.Exception("missing " + source);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            try
            {
                go.name = System.IO.Path.GetFileNameWithoutExtension(path);
                go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (System.Array.IndexOf(hide, t.name) >= 0) t.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { Object.DestroyImmediate(go); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ParticleSystem>();
        }

        static ParticleSystem Fx(string rel)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Etfx + rel + ".prefab");
            if (go == null) throw new System.Exception("missing FX " + rel);
            return go.GetComponent<ParticleSystem>();
        }
    }
}
