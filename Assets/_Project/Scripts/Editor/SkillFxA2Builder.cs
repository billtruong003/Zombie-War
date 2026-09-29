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
