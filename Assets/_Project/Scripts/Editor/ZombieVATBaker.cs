using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using ZombieWar;

namespace ZombieWar.Editor
{
    /// <summary>
    /// One-click enemy VAT pipeline. For each configured enemy it:
    ///   1. Resolves role-tagged AnimationClips from the vendor FBX folder.
    ///   2. Builds a throwaway AnimatorController holding those clips so the shipped baker can enumerate them.
    ///   3. Instantiates the skinned T-pose source and bakes it to VAT (mesh + position texture + material)
    ///      via <see cref="VAT_BakerEditorWindow.BakeObjects"/>.
    ///   4. Assembles a lightweight MeshRenderer prefab driven by <see cref="VAT_Animator"/> with the correct
    ///      ZombieBase-derived component (data + bodyRenderer wired).
    ///   5. Authors/updates the <see cref="ZombieData"/> asset (stats + VAT clip names that match the bake).
    ///
    /// Three things the shipped VAT baker gets wrong for this project, corrected here after it runs:
    ///   * every imported clip arrives as <see cref="WrapMode.Default"/>, which VAT_Animator clamps - so
    ///     idle/move would freeze on their last frame. <see cref="ApplyLoopSemantics"/> rewrites wrap modes.
    ///   * the baked material is created bare, leaving _MainTex unassigned (untextured enemy).
    ///     <see cref="ApplySourceTexture"/> copies the vendor albedo across.
    ///   * collider/agent sizes were hand-guessed. They are now measured from the real baked bounds.
    ///
    /// Menu: Tools/ZombieWar/Bake Enemies (VAT).
    /// </summary>
    public static class ZombieVATBaker
    {
        const string VatRootDir = "Assets/_Project/Art/VAT";
        const string VatEnemyDir = "Assets/_Project/Art/VAT/Enemies";
        const string PrefabDir = "Assets/_Project/Prefabs/Enemies";
        const string DataDir = "Assets/_Project/Data/Zombies";
        const string TempControllerDir = "Assets/_Project/Art/VAT/_TempControllers";
        const string AuditDoc = "Docs/ENEMY_ROSTER_AUDIT.md";
        const string EnemyShaderName = "ZombieWar/VAT/EnemyToon";

        const string CutePack = "Assets/Monsters Ultimate Pack 03 Cute Series";

        /// <summary>One selected source clip. <paramref name="loop"/> drives the baked WrapMode:
        /// locomotion/idle/underground hold loop, one-shot actions clamp on their last frame.</summary>
        class Pick
        {
            public string suffix;
            public bool loop;
            public Pick(string suffix, bool loop = false) { this.suffix = suffix; this.loop = loop; }
        }

        class EnemyBakeDef
        {
            public string enemyId;       // enemy.cute.dog_pup
            public string displayName;   // "Dog Pup"
            public string bakeName;      // "DogPup" - drives all generated file names
            public string sourceDir;     // folder holding <model>.FBX and <model>@<clip>.FBX
            public string modelName;     // "Dog Pup"

            public Pick idle, move, attack, hit, death;
            public Pick special;                       // pounce / dash / slam
            public Pick burrowIn, burrowLoop, burrowOut;

            public Type componentType;
            public ZombieArchetype archetype;
            public bool isElite;
            /// <summary>Per-species overrides for the behaviour component's serialized fields, so
            /// two pouncers can feel different without a subclass each. Applied via SerializedObject
            /// so the values stay visible and editable in the Inspector afterwards.</summary>
            public (string prop, float value)[] tuning;

            public float maxHealth, damage, moveSpeed, attackRange, attackCooldown;
            public float attackWindup, specialWindup;
            public int coinReward, xpReward;
        }

        static string CuteDir(string group) => $"{CutePack}/{group} Cute Series/FBX";

        static readonly EnemyBakeDef[] Configs =
        {
            // ---- Walkers -------------------------------------------------------------------------
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.dog_pup", displayName = "Dog Pup", bakeName = "DogPup",
                sourceDir = CuteDir("Dog Pup"), modelName = "Dog Pup",
                idle = new Pick("Idle", true), move = new Pick("Walk Forward In Place", true),
                attack = new Pick("Bite Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                componentType = typeof(ZombieWalker), archetype = ZombieArchetype.Walker,
                maxHealth = 40f, damage = 6f, moveSpeed = 2.4f, attackRange = 1.3f, attackCooldown = 1.2f,
                attackWindup = 0.35f, coinReward = 1, xpReward = 1,
            },
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.cat_meow", displayName = "Cat Meow", bakeName = "CatMeow",
                sourceDir = CuteDir("Cat Meow"), modelName = "Cat Meow",
                idle = new Pick("Idle", true), move = new Pick("Walk Forward In Place", true),
                attack = new Pick("Bite Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                componentType = typeof(ZombieWalker), archetype = ZombieArchetype.Walker,
                maxHealth = 45f, damage = 7f, moveSpeed = 2.6f, attackRange = 1.3f, attackCooldown = 1.2f,
                attackWindup = 0.35f, coinReward = 1, xpReward = 1,
            },
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.skeleton", displayName = "Skeleton", bakeName = "Skeleton",
                sourceDir = CuteDir("Skeleton"), modelName = "Skeleton",
                idle = new Pick("Idle", true), move = new Pick("Creep Walk Forward In Place", true),
                attack = new Pick("Left Slash Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                special = new Pick("Creep Dash Forward In Place", true),
                componentType = typeof(ZombieWalker), archetype = ZombieArchetype.Walker,
                maxHealth = 60f, damage = 9f, moveSpeed = 2.5f, attackRange = 1.5f, attackCooldown = 1.3f,
                attackWindup = 0.4f, coinReward = 2, xpReward = 2,
            },

            // ---- Runners / Pouncers --------------------------------------------------------------
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.dog_bark", displayName = "Dog Bark", bakeName = "DogBark",
                sourceDir = CuteDir("Dog Bark"), modelName = "Dog Bark",
                idle = new Pick("Idle", true), move = new Pick("Run Forward In Place", true),
                attack = new Pick("Bite Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                special = new Pick("Pounce Attack In Place"),
                componentType = typeof(ZombiePouncer), archetype = ZombieArchetype.Runner,
                maxHealth = 55f, damage = 10f, moveSpeed = 4.6f, attackRange = 1.4f, attackCooldown = 1.1f,
                attackWindup = 0.3f, specialWindup = 0.4f, coinReward = 2, xpReward = 2,
                // The introductory pouncer: shortest hop, longest recovery - easiest to punish.
                tuning = new[] { ("pounceCooldown", 6f), ("leapSpeed", 10f), ("leapDuration", 0.4f),
                                 ("recoverDuration", 0.7f), ("pounceMaxRange", 7f) },
            },
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.cat_bolt", displayName = "Cat Bolt", bakeName = "CatBolt",
                sourceDir = CuteDir("Cat Bolt"), modelName = "Cat Bolt",
                idle = new Pick("Idle", true), move = new Pick("Run Forward In Place", true),
                attack = new Pick("Bite Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                special = new Pick("Pounce Bite Attack In Place"),
                componentType = typeof(ZombiePouncer), archetype = ZombieArchetype.Runner,
                maxHealth = 50f, damage = 11f, moveSpeed = 5.2f, attackRange = 1.4f, attackCooldown = 1.0f,
                attackWindup = 0.3f, specialWindup = 0.25f, coinReward = 2, xpReward = 3,
                // The glass cannon: shortest tell and fastest leap, but the frailest of the pack.
                tuning = new[] { ("pounceCooldown", 4f), ("leapSpeed", 16f), ("leapDuration", 0.4f),
                                 ("recoverDuration", 0.45f), ("pounceMaxRange", 9f), ("crouchDuration", 0.25f) },
            },
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.cat_lightning", displayName = "Cat Lightning", bakeName = "CatLightning",
                sourceDir = CuteDir("Cat Lightning"), modelName = "Cat Lightning",
                idle = new Pick("Idle", true), move = new Pick("Run Forward In Place", true),
                attack = new Pick("Bite Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                special = new Pick("Pounce Bite Attack In Place"),
                componentType = typeof(ZombiePouncer), archetype = ZombieArchetype.Runner,
                maxHealth = 60f, damage = 12f, moveSpeed = 5.0f, attackRange = 1.4f, attackCooldown = 1.0f,
                attackWindup = 0.3f, specialWindup = 0.35f, coinReward = 3, xpReward = 3,
                // The harasser: longest reach, so it opens on the player from outside the pack.
                tuning = new[] { ("pounceCooldown", 4.5f), ("leapSpeed", 14f), ("leapDuration", 0.55f),
                                 ("recoverDuration", 0.5f), ("pounceMaxRange", 11f), ("pounceMinRange", 4f) },
            },
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.dog_bowwow", displayName = "Dog Bowwow", bakeName = "DogBowwow",
                sourceDir = CuteDir("Dog Bowwow"), modelName = "Dog Bowwow",
                idle = new Pick("Idle", true), move = new Pick("Run Forward In Place", true),
                attack = new Pick("Bite Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                special = new Pick("Pounce Smash Attack In Place"),
                componentType = typeof(ZombiePouncer), archetype = ZombieArchetype.Runner,
                maxHealth = 110f, damage = 16f, moveSpeed = 4.2f, attackRange = 1.8f, attackCooldown = 1.4f,
                attackWindup = 0.35f, specialWindup = 0.5f, coinReward = 4, xpReward = 4,
                // The bruiser: slow, obvious wind-up but a wide, heavy landing that punishes crowding.
                tuning = new[] { ("pounceCooldown", 7f), ("leapSpeed", 11f), ("leapDuration", 0.6f),
                                 ("recoverDuration", 0.9f), ("pounceRadius", 2.8f),
                                 ("pounceDamageMultiplier", 1.9f), ("crouchDuration", 0.5f) },
            },

            // ---- Ranged / Casters ----------------------------------------------------------------
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.cacti", displayName = "Cacti", bakeName = "Cacti",
                sourceDir = CuteDir("Cacti"), modelName = "Cacti",
                idle = new Pick("Idle", true), move = new Pick("Walk Forward In Place", true),
                attack = new Pick("Projectile Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                special = new Pick("Spawn"),
                componentType = typeof(ZombieRanged), archetype = ZombieArchetype.Ranged,
                maxHealth = 40f, damage = 8f, moveSpeed = 1.8f, attackRange = 9f, attackCooldown = 2.2f,
                attackWindup = 0.45f, coinReward = 2, xpReward = 2,
                // Short-range spitter: has to commit closer than the others, so it is the one the
                // player can actually rush down.
                tuning = new[] { ("fireRange", 9f), ("projectileSpeed", 12f) },
            },
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.cactus", displayName = "Cactus", bakeName = "Cactus",
                sourceDir = CuteDir("Cactus"), modelName = "Cactus",
                idle = new Pick("Idle", true), move = new Pick("Walk Forward In Place", true),
                attack = new Pick("Projectile Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                special = new Pick("Slash Attack"),
                componentType = typeof(ZombieRanged), archetype = ZombieArchetype.Ranged,
                maxHealth = 70f, damage = 11f, moveSpeed = 1.9f, attackRange = 10f, attackCooldown = 2.3f,
                attackWindup = 0.45f, coinReward = 3, xpReward = 3,
                // Bulkier mid-range turret: slow, heavy shots that reward strafing.
                tuning = new[] { ("fireRange", 11f), ("projectileSpeed", 15f) },
            },
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.skeleton_mage", displayName = "Skeleton Mage", bakeName = "SkeletonMage",
                sourceDir = CuteDir("Skeleton Mage"), modelName = "Skeleton Mage",
                idle = new Pick("Idle", true), move = new Pick("Fly Forward In Place", true),
                attack = new Pick("Projectile Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                special = new Pick("Cast Spell"),
                componentType = typeof(ZombieRanged), archetype = ZombieArchetype.Ranged,
                maxHealth = 80f, damage = 14f, moveSpeed = 2.6f, attackRange = 12f, attackCooldown = 2.4f,
                attackWindup = 0.5f, specialWindup = 0.5f, coinReward = 4, xpReward = 4,
                // The artillery: outranges everything and floats (Fly locomotion), so it must be
                // deliberately hunted rather than bumped into.
                tuning = new[] { ("fireRange", 14f), ("projectileSpeed", 18f) },
            },

            // ---- Burrow / Ambush -----------------------------------------------------------------
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.burrow", displayName = "Burrow", bakeName = "Burrow",
                sourceDir = CuteDir("Burrow"), modelName = "Burrow",
                idle = new Pick("Idle", true), move = new Pick("Walk Forward In Place", true),
                attack = new Pick("Head Attack In Place"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                burrowIn = new Pick("Idle to Underground"),
                burrowLoop = new Pick("Underground", true),
                burrowOut = new Pick("Underground to Idle"),
                componentType = typeof(ZombieBurrower), archetype = ZombieArchetype.Burrower,
                maxHealth = 65f, damage = 12f, moveSpeed = 2.8f, attackRange = 1.6f, attackCooldown = 1.4f,
                attackWindup = 0.4f, coinReward = 3, xpReward = 3,
                // The teaching burrower: dives rarely and telegraphs generously.
                tuning = new[] { ("surfaceDuration", 7f), ("emergeTelegraph", 0.9f),
                                 ("undergroundSpeedMultiplier", 2.2f), ("emergeRadius", 2f) },
            },
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.mole_rat", displayName = "Mole Rat", bakeName = "MoleRat",
                sourceDir = CuteDir("Mole Rat"), modelName = "Mole Rat",
                idle = new Pick("Idle", true), move = new Pick("Walk Forward In Place", true),
                attack = new Pick("Slash Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                burrowIn = new Pick("Idle To Underground"),
                burrowLoop = new Pick("Underground", true),
                burrowOut = new Pick("Underground To Idle"),
                componentType = typeof(ZombieBurrower), archetype = ZombieArchetype.Burrower,
                maxHealth = 75f, damage = 13f, moveSpeed = 3.0f, attackRange = 1.6f, attackCooldown = 1.3f,
                attackWindup = 0.4f, coinReward = 3, xpReward = 3,
                // The aggressive burrower: dives often, surfaces fast and close.
                tuning = new[] { ("surfaceDuration", 4.5f), ("emergeTelegraph", 0.55f),
                                 ("undergroundSpeedMultiplier", 3f), ("emergeMinDistance", 2f),
                                 ("emergeRadius", 2.4f) },
            },

            // ---- Heavy / Elite -------------------------------------------------------------------
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.skeleton_giant", displayName = "Skeleton Giant", bakeName = "SkeletonGiant",
                sourceDir = CuteDir("Skeleton Giant"), modelName = "Skeleton Giant",
                idle = new Pick("Idle", true), move = new Pick("Walk Forward In Place", true),
                attack = new Pick("Slash Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                special = new Pick("Jump Smash Attack In Place"),
                componentType = typeof(ZombieBoss), archetype = ZombieArchetype.Heavy, isElite = true,
                maxHealth = 900f, damage = 26f, moveSpeed = 2.2f, attackRange = 2.8f, attackCooldown = 2.0f,
                attackWindup = 0.5f, specialWindup = 0.6f, coinReward = 30, xpReward = 25,
                // Pure area-denial heavy: huge slow slam, no mobility tool. Kite it and it is safe;
                // stand next to it and it is the hardest hitter in the roster.
                tuning = new[] { ("specialRadius", 5f), ("specialCooldown", 5.5f),
                                 ("specialDamageMultiplier", 2.2f) },
            },
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.mole_rat_king", displayName = "Mole Rat King", bakeName = "MoleRatKing",
                sourceDir = CuteDir("Mole Rat King"), modelName = "Mole Rat King",
                idle = new Pick("Idle", true), move = new Pick("Walk Forward In Place", true),
                attack = new Pick("Bite Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                special = new Pick("Head Attack In Place"),
                burrowIn = new Pick("Idle To Underground"),
                burrowLoop = new Pick("Underground", true),
                burrowOut = new Pick("Underground To Idle"),
                componentType = typeof(ZombieBurrower), archetype = ZombieArchetype.Boss, isElite = true,
                maxHealth = 1100f, damage = 24f, moveSpeed = 3.2f, attackRange = 2.2f, attackCooldown = 1.8f,
                attackWindup = 0.4f, specialWindup = 0.45f, coinReward = 35, xpReward = 30,
                // Boss burrower: relentless dive cadence and a much wider eruption, so the arena
                // itself becomes the threat rather than its melee.
                tuning = new[] { ("surfaceDuration", 5f), ("emergeTelegraph", 0.75f),
                                 ("undergroundSpeedMultiplier", 3.2f), ("emergeRadius", 4f),
                                 ("emergeDamageMultiplier", 1.8f), ("minDiveDistance", 4f) },
            },
            new EnemyBakeDef
            {
                enemyId = "enemy.cute.cactus_boss", displayName = "Cactus Boss", bakeName = "CactusBoss",
                sourceDir = CuteDir("Cactus Boss"), modelName = "Cactus Boss",
                idle = new Pick("Idle", true), move = new Pick("Walk Forward In Place", true),
                attack = new Pick("Jump Smack Attack"), hit = new Pick("Take Damage"), death = new Pick("Die"),
                special = new Pick("Dash Forward Attack In Place"),
                componentType = typeof(ZombieCharger), archetype = ZombieArchetype.Boss, isElite = true,
                maxHealth = 1400f, damage = 30f, moveSpeed = 2.6f, attackRange = 3.2f, attackCooldown = 2.2f,
                attackWindup = 0.5f, specialWindup = 0.7f, coinReward = 40, xpReward = 35,
                // The finale: slam covers close range, charge covers mid, so there is no safe band -
                // only the recovery windows after each.
                tuning = new[] { ("chargeCooldown", 7f), ("chargeSpeed", 15f), ("chargeDuration", 1.2f),
                                 ("chargeWidth", 3f), ("recoverDuration", 1.3f),
                                 ("specialRadius", 4.5f), ("specialCooldown", 7f) },
            },
        };

        struct BakeReport
        {
            public string enemyId, displayName, status, note;
            public int vertexCount, frameCount, clipCount;
            public long textureBytes;
            public float standHeight;
            public Vector3 boundsSize;
            public string clipSummary;
        }

        [MenuItem("Tools/ZombieWar/Bake Enemies (VAT)")]
        public static void BakeAll()
        {
            EnsureFolder(VatEnemyDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(DataDir);
            EnsureFolder(TempControllerDir);

            var reports = new List<BakeReport>();
            try
            {
                for (int i = 0; i < Configs.Length; i++)
                {
                    var cfg = Configs[i];
                    EditorUtility.DisplayProgressBar("ZombieWar VAT Bake",
                        $"[{i + 1}/{Configs.Length}] {cfg.displayName}", (float)i / Configs.Length);
                    try { reports.Add(BakeOne(cfg)); }
                    catch (Exception e)
                    {
                        Debug.LogError($"[VATBake] {cfg.displayName} failed: {e}");
                        reports.Add(new BakeReport { enemyId = cfg.enemyId, displayName = cfg.displayName, status = "FAILED", note = e.Message });
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.DeleteAsset(TempControllerDir);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // One shared look across the whole roster, applied last so it wins over bake defaults.
            VatLookApplier.Apply(VatLookApplier.LoadOrCreateConfig());

            WriteAuditDoc(reports);

            int ok = reports.Count(r => r.status == "OK");
            long totalBytes = reports.Sum(r => r.textureBytes);
            // No modal dialog here on purpose: this menu item is also driven headlessly from
            // automation, where a blocking dialog would hang the editor.
            Debug.Log($"[VATBake] {ok}/{Configs.Length} baked. VAT texture footprint: {totalBytes / (1024f * 1024f):F1} MB. Audit -> {AuditDoc}");
        }

        static BakeReport BakeOne(EnemyBakeDef cfg)
        {
            var report = new BakeReport { enemyId = cfg.enemyId, displayName = cfg.displayName, status = "FAILED" };

            var modelPath = ResolveModelPath(cfg);
            if (modelPath == null)
            {
                report.note = $"model FBX not found in {cfg.sourceDir}";
                Debug.LogError($"[VATBake] {cfg.displayName}: {report.note}");
                return report;
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            // 1. Resolve every selected clip. Vendor file names are inconsistent (case, stray leading
            //    spaces, .FBX vs .fbx), so match on a normalized suffix rather than an exact path.
            var picks = SelectedPicks(cfg).ToList();
            var resolved = new List<(Pick pick, AnimationClip clip)>();
            foreach (var p in picks)
            {
                var clip = ResolveClip(cfg, p.suffix);
                if (clip == null)
                {
                    report.note = $"missing clip '{cfg.modelName}@{p.suffix}'";
                    Debug.LogError($"[VATBake] {cfg.displayName}: {report.note}");
                    return report;
                }
                if (resolved.Any(r => r.clip == clip)) continue;
                resolved.Add((p, clip));
            }

            // 2. Temp AnimatorController so the shipped baker can enumerate the clips.
            var ctrlPath = $"{TempControllerDir}/{cfg.bakeName}_bake.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = controller.layers[0].stateMachine;
            foreach (var r in resolved) sm.AddState(r.clip.name).motion = r.clip;
            AssetDatabase.SaveAssets();

            // 3. Bake. Output folder is per-enemy so re-runs never collide across species.
            string outDir = $"{VatEnemyDir}/{cfg.enemyId}";
            EnsureFolder(outDir);
            string dataPath = $"{outDir}/{cfg.bakeName}_VAT_Data.asset";
            // Generated artifact - clear it so the baker's CreateAsset writes a clean set of sub-assets
            // instead of stacking a second mesh/texture/material onto the previous bake.
            AssetDatabase.DeleteAsset(dataPath);

            var src = (GameObject)PrefabUtility.InstantiatePrefab(model);
            src.name = cfg.bakeName;
            var anim = src.GetComponentInChildren<Animator>() ?? src.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            int baked;
            try { baked = VAT_BakerEditorWindow.BakeObjects(new[] { src }, outDir); }
            finally { UnityEngine.Object.DestroyImmediate(src); }
            if (baked == 0)
            {
                report.note = "VAT baker produced no output";
                Debug.LogError($"[VATBake] {cfg.displayName}: {report.note}");
                return report;
            }

            var vatData = AssetDatabase.LoadAssetAtPath<VAT_AnimationData>(dataPath);
            if (vatData == null)
            {
                report.note = $"baked data missing at {dataPath}";
                Debug.LogError($"[VATBake] {cfg.displayName}: {report.note}");
                return report;
            }
            var vatMat = AssetDatabase.LoadAllAssetsAtPath(dataPath).OfType<Material>().FirstOrDefault();
            if (vatMat == null)
            {
                report.note = "no VAT material sub-asset";
                Debug.LogError($"[VATBake] {cfg.displayName}: {report.note}");
                return report;
            }

            ApplyLoopSemantics(vatData, resolved);
            ApplyEnemyShader(vatMat);
            ApplySourceTexture(model, vatMat, cfg);
            long normalBytes = BakeNormalTexture(model, vatData, resolved, vatMat, dataPath);
            EditorUtility.SetDirty(vatData);
            EditorUtility.SetDirty(vatMat);

            // 4. Author / update the ZombieData.
            string dataAssetPath = $"{DataDir}/ZD_{cfg.bakeName}.asset";
            var zData = AssetDatabase.LoadAssetAtPath<ZombieData>(dataAssetPath);
            if (zData == null)
            {
                zData = ScriptableObject.CreateInstance<ZombieData>();
                AssetDatabase.CreateAsset(zData, dataAssetPath);
            }
            zData.enemyId = cfg.enemyId;
            zData.zombieName = cfg.displayName;
            zData.vatData = vatData;
            zData.archetype = cfg.archetype;
            zData.isElite = cfg.isElite;
            zData.maxHealth = cfg.maxHealth;
            zData.damage = cfg.damage;
            zData.moveSpeed = cfg.moveSpeed;
            zData.attackRange = cfg.attackRange;
            zData.attackCooldown = cfg.attackCooldown;
            zData.attackWindup = cfg.attackWindup;
            zData.specialWindup = cfg.specialWindup;
            zData.coinReward = cfg.coinReward;
            zData.xpReward = cfg.xpReward;
            zData.idleClip = ResolveClip(cfg, cfg.idle.suffix).name;
            zData.moveClip = ResolveClip(cfg, cfg.move.suffix).name;
            zData.attackClip = ResolveClip(cfg, cfg.attack.suffix).name;
            zData.hitClip = ResolveClip(cfg, cfg.hit.suffix).name;
            zData.deathClip = ResolveClip(cfg, cfg.death.suffix).name;
            zData.specialClip = ClipNameOrEmpty(cfg, cfg.special);
            zData.burrowInClip = ClipNameOrEmpty(cfg, cfg.burrowIn);
            zData.burrowLoopClip = ClipNameOrEmpty(cfg, cfg.burrowLoop);
            zData.burrowOutClip = ClipNameOrEmpty(cfg, cfg.burrowOut);

            // 5. Build the prefab.
            //    The source art is authored Z-up, so the Visual child is rotated -90 about X to stand
            //    it upright; its local position stays at origin. Because of that rotation the mesh's
            //    +Z becomes world +Y, so the standing height is measured along Z.
            //    Height comes from the IDLE pose only - the baked VAT bounds are the union of every
            //    frame, so a jump or pounce clip would otherwise inflate the capsule far past the
            //    creature's actual silhouette.
            float height = Mathf.Max(0.4f, MeasureIdleHeight(model, ResolveClip(cfg, cfg.idle.suffix)));
            const float radius = 0.35f;

            var go = new GameObject($"ENM_{cfg.bakeName}_VAT");
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.speed = cfg.moveSpeed;
            agent.stoppingDistance = cfg.attackRange * 0.8f;

            var col = go.AddComponent<CapsuleCollider>();
            col.radius = radius;
            col.height = height;
            col.center = new Vector3(0f, height * 0.5f, 0f);

            // Visual = mesh + VAT driver on its own child, so art offsets never skew the root's
            // collider/agent orientation. VAT_Animator requires a MeshRenderer on its own GameObject.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            visual.AddComponent<MeshFilter>().sharedMesh = vatData.bakedMesh;
            var mr = visual.AddComponent<MeshRenderer>();
            mr.sharedMaterial = vatMat;
            var vatAnim = visual.AddComponent<VAT_Animator>();
            vatAnim.animationData = vatData;
            vatAnim.defaultClipIndex = 0;

            // Fake contact shadow: a flat quad under the root, all enemies sharing ONE material and
            // the same local placement. A real shadow-caster pass per enemy is far too expensive for
            // a mobile horde, and a blob reads better at this camera angle anyway. Sized from the
            // measured capsule so a boss's blob is not pup-sized.
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shadow.name = "ShadowBlob";
            UnityEngine.Object.DestroyImmediate(shadow.GetComponent<Collider>());
            shadow.transform.SetParent(go.transform, false);
            shadow.transform.localPosition = new Vector3(0f, 0.02f, 0f);   // just above the ground
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            float blob = Mathf.Max(0.8f, radius * 4.5f);
            shadow.transform.localScale = new Vector3(blob, blob, 1f);
            var shadowRenderer = shadow.GetComponent<MeshRenderer>();
            shadowRenderer.sharedMaterial = VatLookApplier.EnsureShadowMaterial();
            shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;

            var zb = go.AddComponent(cfg.componentType);
            var so = new SerializedObject(zb);
            SetRef(so, "data", zData);
            SetRef(so, "bodyRenderer", mr);
            SetRef(so, "shadowRenderer", shadowRenderer);
            if (cfg.tuning != null)
            {
                foreach (var (prop, value) in cfg.tuning)
                {
                    var p = so.FindProperty(prop);
                    if (p == null)
                    {
                        Debug.LogWarning($"[VATBake] {cfg.displayName}: tuning field '{prop}' not found on " +
                                         $"{cfg.componentType.Name} - skipped.");
                        continue;
                    }
                    p.floatValue = value;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            string prefabPath = $"{PrefabDir}/ENM_{cfg.bakeName}_VAT.prefab";
            zData.prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);

            EditorUtility.SetDirty(zData);
            AssetDatabase.SaveAssets();

            int totalFrames = vatData.animationClips.Sum(c => c.frameCount);
            int verts = vatData.bakedMesh != null ? vatData.bakedMesh.vertexCount : 0;
            report.status = "OK";
            report.vertexCount = verts;
            report.frameCount = totalFrames;
            report.clipCount = vatData.animationClips.Count;
            // Position map is RGBAHalf (8 B/texel); the animated-normal map is RGB24 (3 B/texel).
            report.textureBytes = (long)verts * totalFrames * 8L + normalBytes;
            report.standHeight = height;
            report.boundsSize = vatData.positionMaxBounds - vatData.positionMinBounds;
            report.clipSummary = string.Join(", ", vatData.animationClips.Select(c => $"{c.name}[{c.wrapMode}]").ToArray());

            Debug.Log($"[VATBake] OK {cfg.displayName} ({cfg.enemyId}): {verts}v x {totalFrames}f = " +
                      $"{report.textureBytes / (1024f * 1024f):F2} MB -> {prefabPath}", zData);
            return report;
        }

        static IEnumerable<Pick> SelectedPicks(EnemyBakeDef c)
        {
            yield return c.idle;
            yield return c.move;
            yield return c.attack;
            yield return c.hit;
            yield return c.death;
            if (c.special != null) yield return c.special;
            if (c.burrowIn != null) yield return c.burrowIn;
            if (c.burrowLoop != null) yield return c.burrowLoop;
            if (c.burrowOut != null) yield return c.burrowOut;
        }

        static string ClipNameOrEmpty(EnemyBakeDef cfg, Pick p)
            => p == null ? "" : (ResolveClip(cfg, p.suffix)?.name ?? "");

        /// <summary>VAT_Animator only wraps clips flagged Loop; everything else clamps on its last
        /// frame. Imported FBX clips all arrive as WrapMode.Default, so without this idle and walk
        /// would visibly freeze. Rewrites the baked ClipInfo list from the authored Pick flags.</summary>
        static void ApplyLoopSemantics(VAT_AnimationData vatData, List<(Pick pick, AnimationClip clip)> resolved)
        {
            foreach (var info in vatData.animationClips)
            {
                var match = resolved.FirstOrDefault(r => r.clip.name == info.name);
                info.wrapMode = match.pick != null && match.pick.loop ? WrapMode.Loop : WrapMode.ClampForever;
            }
        }

        /// <summary>The shipped baker assigns the plain Optimized_VAT shader, which has no toon
        /// lighting, no hit flash and - critically - no _Dissolve, so ZombieBase's death dissolve was
        /// silently doing nothing. Swap in the project's enemy shader which implements all three.</summary>
        static void ApplyEnemyShader(Material vatMat)
        {
            var shader = Shader.Find(EnemyShaderName);
            if (shader == null)
            {
                Debug.LogError($"[VATBake] Shader '{EnemyShaderName}' not found - keeping " +
                               $"'{vatMat.shader.name}'. Dissolve and hit flash will not work.");
                return;
            }
            vatMat.shader = shader;
            vatMat.enableInstancing = true;
            // Authored look, applied uniformly so the whole roster shades identically.
            vatMat.SetFloat("_SpecSteps", 1.5f);
        }

        /// <summary>The shipped baker creates the VAT material bare, so _MainTex stays unbound and the
        /// enemy renders untextured. Copy the vendor albedo across (vendor material itself untouched).</summary>
        static void ApplySourceTexture(GameObject model, Material vatMat, EnemyBakeDef cfg)
        {
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var srcMat = smr != null ? smr.sharedMaterial : null;

            Texture albedo = null;
            if (srcMat != null)
            {
                foreach (var prop in new[] { "_BaseMap", "_MainTex" })
                {
                    if (srcMat.HasProperty(prop) && srcMat.GetTexture(prop) != null)
                    {
                        albedo = srcMat.GetTexture(prop);
                        break;
                    }
                }
            }

            // Some creatures (Mole Rat) ship an FBX with no embedded material, so Unity binds the
            // URP package's default Lit.mat - untextured grey. The pack still ships the artwork in a
            // sibling Textures/ folder, so fall back to that rather than shipping an untextured enemy.
            if (albedo == null)
            {
                var packRoot = System.IO.Path.GetDirectoryName(cfg.sourceDir).Replace('\\', '/');
                var found = AssetDatabase.FindAssets("t:Texture2D", new[] { packRoot })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(path => Normalize(System.IO.Path.GetFileNameWithoutExtension(path)) == Normalize(cfg.modelName) ? 0 : 1)
                    .ThenBy(path => path)
                    .FirstOrDefault();
                if (found != null)
                {
                    albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(found);
                    Debug.Log($"[VATBake] {cfg.displayName}: source material had no albedo; using pack texture '{found}'.");
                }
            }

            if (albedo != null) vatMat.SetTexture("_MainTex", albedo);
            else Debug.LogWarning($"[VATBake] {cfg.displayName}: no albedo texture found; VAT material will render flat.");
        }

        /// <summary>
        /// Bakes a per-frame NORMAL texture matching the position texture's frame layout exactly.
        ///
        /// Why this is required: the shipped VAT baker stores only positions, so the mesh keeps the
        /// STATIC normals of its bind pose. The vertices then get pushed around by the position
        /// texture while their normals stay put - lighting no longer follows the animation. That is
        /// invisible on a plain lit shader but very obvious under toon shading, because the cel
        /// terminator is a hard edge driven by N·L: it would sit frozen on the model while the body
        /// moves underneath it. Sampling an animated normal fixes the shading at its source.
        ///
        /// Stored as RGB24 rather than RGBAHalf: normals only need ~8-bit angular precision here
        /// (well under a degree of error) and this keeps the extra memory to 3/8 of the position map.
        /// </summary>
        static long BakeNormalTexture(GameObject model, VAT_AnimationData vatData,
                                      List<(Pick pick, AnimationClip clip)> resolved,
                                      Material vatMat, string dataPath)
        {
            if (vatData.bakedMesh == null || vatData.positionTexture == null) return 0;

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            temp.transform.localScale = Vector3.one;
            var work = new Mesh();
            try
            {
                var smr = temp.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr == null) return 0;

                int vertexCount = vatData.bakedMesh.vertexCount;
                int totalFrames = vatData.positionTexture.height;
                var tex = new Texture2D(vertexCount, totalFrames, TextureFormat.RGB24, false)
                {
                    name = $"{vatMat.name}_VAT_NormalTexture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };

                var row = new Color[vertexCount];
                foreach (var info in vatData.animationClips)
                {
                    // Match by name: the baker enumerated clips off the controller, whose order need
                    // not match ours, but names are unique within a bake.
                    var clip = resolved.FirstOrDefault(r => r.clip.name == info.name).clip;
                    if (clip == null) continue;

                    for (int frame = 0; frame < info.frameCount; frame++)
                    {
                        // Identical sampling maths to VAT_BakerEditorWindow.BakeAnimationsToTexture,
                        // so normal row N always corresponds to position row N.
                        float sampleTime = (frame / (float)(info.frameCount - 1)) * info.duration;
                        clip.SampleAnimation(temp, sampleTime);
                        smr.BakeMesh(work, true);

                        var normals = work.normals;
                        int count = Mathf.Min(vertexCount, normals.Length);
                        for (int i = 0; i < count; i++)
                        {
                            // Pack the signed unit vector into an unsigned 0..1 texture.
                            var n = normals[i];
                            row[i] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                        }
                        int y = info.startFrame + frame;
                        if (y >= 0 && y < totalFrames) tex.SetPixels(0, y, vertexCount, 1, row);
                    }
                }

                tex.Apply(false, true);
                AssetDatabase.AddObjectToAsset(tex, dataPath);
                vatMat.SetTexture("_NormalTexture", tex);
                vatMat.SetFloat("_UseAnimatedNormals", 1f);
                AssetDatabase.SaveAssets();
                return (long)vertexCount * totalFrames * 3L;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(work);
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        /// <summary>Standing height of the creature, sampled across the idle clip only.
        /// The art is Z-up and the Visual child is rotated -90 about X, so the mesh's Z axis is what
        /// ends up vertical in the world - height is therefore the maximum Z reached during idle,
        /// measured from the origin (the prefab's ground plane).</summary>
        static float MeasureIdleHeight(GameObject model, AnimationClip idle)
        {
            if (idle == null) return 0f;

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            temp.transform.localScale = Vector3.one;
            var mesh = new Mesh();
            try
            {
                var smr = temp.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr == null) return 0f;

                float top = 0f;
                const int samples = 12;
                for (int i = 0; i < samples; i++)
                {
                    idle.SampleAnimation(temp, idle.length * i / (samples - 1f));
                    smr.BakeMesh(mesh, true);
                    top = Mathf.Max(top, mesh.bounds.max.z);
                }
                return top;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        static string ResolveModelPath(EnemyBakeDef cfg)
        {
            foreach (var ext in new[] { "FBX", "fbx" })
            {
                var p = $"{cfg.sourceDir}/{cfg.modelName}.{ext}";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(p) != null) return p;
            }
            return null;
        }

        static string Normalize(string s) =>
            new string(s.Where(ch => !char.IsWhiteSpace(ch)).ToArray()).ToLowerInvariant();

        /// <summary>Finds "<model>@<suffix>" in the source folder tolerating vendor inconsistencies -
        /// stray leading spaces ("Mole Rat@ Underground"), casing, and .FBX vs .fbx.</summary>
        static AnimationClip ResolveClip(EnemyBakeDef cfg, string suffix)
        {
            string want = Normalize(suffix);
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { cfg.sourceDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                int at = file.IndexOf('@');
                if (at < 0) continue;
                if (Normalize(file.Substring(at + 1)) != want) continue;

                return AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            }
            return null;
        }

        static void WriteAuditDoc(List<BakeReport> reports)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Enemy roster audit");
            sb.AppendLine();
            sb.AppendLine("> Generated by `Tools/ZombieWar/Bake Enemies (VAT)`. Do not hand-edit the table -");
            sb.AppendLine("> re-run the baker instead. Tuning numbers are provisional.");
            sb.AppendLine();
            sb.AppendLine("Capsule/agent radius is a fixed 0.35 m; height is the creature's measured idle");
            sb.AppendLine("standing height (not the all-frame animation bounds, which jumps inflate).");
            sb.AppendLine();
            sb.AppendLine("| Enemy ID | Name | Status | Verts | Frames | Clips | VAT texture | Stand height (m) | Anim bounds (m) |");
            sb.AppendLine("|---|---|---|---:|---:|---:|---:|---:|---|");
            foreach (var r in reports)
            {
                sb.AppendLine($"| `{r.enemyId}` | {r.displayName} | {r.status} | {r.vertexCount} | {r.frameCount} | " +
                              $"{r.clipCount} | {r.textureBytes / (1024f * 1024f):F2} MB | {r.standHeight:F2} | " +
                              $"{r.boundsSize.x:F2} x {r.boundsSize.y:F2} x {r.boundsSize.z:F2} |");
            }
            sb.AppendLine();
            sb.AppendLine($"**Total VAT texture footprint:** {reports.Sum(r => r.textureBytes) / (1024f * 1024f):F1} MB " +
                          "uncompressed, counting BOTH maps per enemy: the position map (RGBAHalf, " +
                          "8 B/texel) and the animated-normal map (RGB24, 3 B/texel).");
            sb.AppendLine();
            sb.AppendLine("The normal map is what makes toon shading follow the animation - without it the mesh");
            sb.AppendLine("keeps its bind-pose normals and the cel terminator stays frozen while the body moves.");
            sb.AppendLine("If this budget needs cutting later, the cheapest wins are dropping unused clips and");
            sb.AppendLine("lowering the 30 fps bake rate - both shrink frame count, which scales both maps.");
            sb.AppendLine();
            sb.AppendLine("## Baked clips and wrap modes");
            sb.AppendLine();
            foreach (var r in reports.Where(r => r.status == "OK"))
                sb.AppendLine($"- **{r.displayName}** (`{r.enemyId}`): {r.clipSummary}");
            var failed = reports.Where(r => r.status != "OK").ToList();
            if (failed.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Blocked");
                sb.AppendLine();
                foreach (var r in failed) sb.AppendLine($"- **{r.displayName}** (`{r.enemyId}`): {r.note}");
            }
            sb.AppendLine();
            sb.AppendLine("## Blocked by source data");
            sb.AppendLine();
            sb.AppendLine("- **HUGO Gorilla** (`enemy.gamwill.hugo`): the source mesh `Hugo` has **16,567 vertices**.");
            sb.AppendLine("  The VAT position texture is `Texture2D(vertexCount, totalFrames)`, so its width would");
            sb.AppendLine("  exceed `SystemInfo.maxTextureSize` (**16,384**) and the bake cannot be created. The mesh");
            sb.AppendLine("  also carries 3 submeshes/3 `Standard` (non-URP) materials, which the single-submesh VAT");
            sb.AppendLine("  output cannot represent. Baking HUGO requires mesh decimation plus URP material");
            sb.AppendLine("  conversion; it is intentionally excluded from the roster. Cactus Boss and Skeleton Giant");
            sb.AppendLine("  serve as the late-campaign bosses instead.");

            System.IO.File.WriteAllText(AuditDoc, sb.ToString());
        }

        static void SetRef(SerializedObject so, string prop, UnityEngine.Object val)
        {
            var p = so.FindProperty(prop);
            if (p == null)
            {
                Debug.LogWarning($"[VATBake] SerializedProperty '{prop}' not found on {so.targetObject.GetType().Name}");
                return;
            }
            p.objectReferenceValue = val;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
