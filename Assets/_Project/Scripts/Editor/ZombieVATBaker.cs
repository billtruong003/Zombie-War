using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using ZombieWar;

namespace ZombieWar.Editor
{
    /// <summary>
    /// One-click enemy VAT pipeline. For each configured enemy it:
    ///   1. Resolves role-tagged AnimationClips (idle/move/attack/hit/death) from their FBX files.
    ///   2. Builds a throwaway AnimatorController holding those clips so the shipped baker can enumerate them.
    ///   3. Instantiates the skinned T-pose source and bakes it to VAT (mesh + position texture + material)
    ///      via <see cref="VAT_BakerEditorWindow.BakeObjects"/>.
    ///   4. Assembles a lightweight MeshRenderer prefab driven by <see cref="VAT_Animator"/> with the correct
    ///      ZombieBase-derived component (data + bodyRenderer wired).
    ///   5. Authors/updates the <see cref="ZombieData"/> asset (stats + VAT clip names that match the bake).
    ///
    /// Menu: Tools/ZombieWar/Bake Enemies (VAT).
    /// </summary>
    public static class ZombieVATBaker
    {
        const string VatOutputDir = "Assets/_Project/Art/VAT";
        const string PrefabDir = "Assets/_Project/Prefabs/Enemies";
        const string DataDir = "Assets/_Project/Data/Zombies";
        const string TempControllerDir = "Assets/_Project/Art/VAT/_TempControllers";

        class Role { public string idle, move, attack, hit, death; }

        class EnemyBakeConfig
        {
            public string sourceName;    // baseName -> {sourceName}_VAT_Data.asset
            public string prefabName;
            public string dataAssetName; // ZD_*
            public string modelFbx;      // skinned T-pose source
            public string animFolder;
            public Role role;
            public Type componentType;   // ZombieWalker / ZombieBoss / ...
            public float maxHealth, damage, moveSpeed, attackRange, attackCooldown;
            public float agentRadius, agentHeight;
        }

        static readonly EnemyBakeConfig[] Configs =
        {
            new EnemyBakeConfig
            {
                sourceName = "Zombie", prefabName = "Zombie_VAT", dataAssetName = "ZD_Zombie",
                modelFbx   = "Assets/GAMWILL/GAMWILL Zombie fun character free sample/Mesh/Zombie_T-Pose.fbx",
                animFolder = "Assets/GAMWILL/GAMWILL Zombie fun character free sample/Animation",
                role = new Role { idle = "Zombie_Idle", move = "Zombie_Walk", attack = "Zombie_Attack", hit = "Zombie_Damage", death = "Zombie_dead" },
                componentType = typeof(ZombieWalker),
                maxHealth = 100f, damage = 10f, moveSpeed = 3.2f, attackRange = 1.6f, attackCooldown = 1.2f,
                agentRadius = 0.35f, agentHeight = 1.8f,
            },
            new EnemyBakeConfig
            {
                sourceName = "HUGO", prefabName = "Hugo_Boss_VAT", dataAssetName = "ZD_Hugo_Boss",
                modelFbx   = "Assets/GAMWILL Character Pack Monster  Bionic Cartoon Zombie Gorilla/Mesh/HUGO_T_Pose.fbx",
                animFolder = "Assets/GAMWILL Character Pack Monster  Bionic Cartoon Zombie Gorilla/Animation",
                role = new Role { idle = "Idle_1", move = "Run", attack = "Run_Attack", hit = "Jump", death = "Death" },
                componentType = typeof(ZombieBoss),
                maxHealth = 2000f, damage = 45f, moveSpeed = 4.5f, attackRange = 3.0f, attackCooldown = 2.0f,
                agentRadius = 1.2f, agentHeight = 3.2f,
            },
        };

        [MenuItem("Tools/ZombieWar/Bake Enemies (VAT)")]
        public static void BakeAll()
        {
            EnsureFolder(VatOutputDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(DataDir);
            EnsureFolder(TempControllerDir);

            int ok = 0;
            try
            {
                for (int i = 0; i < Configs.Length; i++)
                {
                    var cfg = Configs[i];
                    EditorUtility.DisplayProgressBar("ZombieWar VAT Bake",
                        $"[{i + 1}/{Configs.Length}] {cfg.sourceName}", (float)i / Configs.Length);
                    try { if (BakeOne(cfg)) ok++; }
                    catch (Exception e) { Debug.LogError($"[VATBake] {cfg.sourceName} failed: {e}"); }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.DeleteAsset(TempControllerDir);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog("ZombieWar VAT Bake",
                $"Done. {ok}/{Configs.Length} enemies baked.\n\nData:    {DataDir}\nPrefabs: {PrefabDir}\nVAT:     {VatOutputDir}", "OK");
        }

        static bool BakeOne(EnemyBakeConfig cfg)
        {
            // 1. Source T-pose model ------------------------------------------------------------
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(cfg.modelFbx);
            if (model == null) { Debug.LogError($"[VATBake] Model not found: {cfg.modelFbx}"); return false; }

            // 2. Resolve role clips (each role -> first real AnimationClip inside its FBX) --------
            AnimationClip Load(string role, string file)
            {
                if (string.IsNullOrEmpty(file)) return null;
                var path = $"{cfg.animFolder}/{file}.fbx";
                var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip == null) Debug.LogError($"[VATBake] No AnimationClip in {path} (role={role})");
                return clip;
            }

            var idle = Load("idle", cfg.role.idle);
            var move = Load("move", cfg.role.move);
            var attack = Load("attack", cfg.role.attack);
            var hit = Load("hit", cfg.role.hit);
            var death = Load("death", cfg.role.death);
            if (idle == null || move == null || attack == null || death == null)
            {
                Debug.LogError($"[VATBake] {cfg.sourceName}: missing required clip(s); aborting.");
                return false;
            }

            // Distinct clips define the VAT frame layout. Lookup is by name at runtime, so order is free.
            var ordered = new List<AnimationClip>();
            void Add(AnimationClip c) { if (c != null && !ordered.Contains(c)) ordered.Add(c); }
            Add(idle); Add(move); Add(attack); Add(hit); Add(death);

            // 3. Temp AnimatorController so the baker can enumerate the clips ---------------------
            var ctrlPath = $"{TempControllerDir}/{cfg.sourceName}_bake.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = controller.layers[0].stateMachine;
            foreach (var c in ordered) sm.AddState(c.name).motion = c;
            AssetDatabase.SaveAssets();

            // 4. Instantiate source, attach Animator + controller, name = bake baseName ----------
            var src = (GameObject)PrefabUtility.InstantiatePrefab(model);
            src.name = cfg.sourceName;
            var anim = src.GetComponentInChildren<Animator>() ?? src.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            // 5. Bake to VAT via the shipped baker ----------------------------------------------
            int baked;
            try { baked = VAT_BakerEditorWindow.BakeObjects(new[] { src }, VatOutputDir); }
            finally { UnityEngine.Object.DestroyImmediate(src); }
            if (baked == 0) { Debug.LogError($"[VATBake] Baker produced nothing for {cfg.sourceName}"); return false; }

            // 6. Load baked data + its sub-asset material ---------------------------------------
            var dataPath = $"{VatOutputDir}/{cfg.sourceName}_VAT_Data.asset";
            var vatData = AssetDatabase.LoadAssetAtPath<VAT_AnimationData>(dataPath);
            if (vatData == null) { Debug.LogError($"[VATBake] Missing baked data at {dataPath}"); return false; }
            var vatMat = AssetDatabase.LoadAllAssetsAtPath(dataPath).OfType<Material>().FirstOrDefault();
            if (vatMat == null) { Debug.LogError($"[VATBake] No VAT material sub-asset in {dataPath}"); return false; }

            // 7. Author / update the ZombieData -------------------------------------------------
            var dataAssetPath = $"{DataDir}/{cfg.dataAssetName}.asset";
            var zData = AssetDatabase.LoadAssetAtPath<ZombieData>(dataAssetPath);
            if (zData == null)
            {
                zData = ScriptableObject.CreateInstance<ZombieData>();
                AssetDatabase.CreateAsset(zData, dataAssetPath);
            }
            zData.zombieName = cfg.sourceName;
            zData.vatData = vatData;
            zData.maxHealth = cfg.maxHealth;
            zData.damage = cfg.damage;
            zData.moveSpeed = cfg.moveSpeed;
            zData.attackRange = cfg.attackRange;
            zData.attackCooldown = cfg.attackCooldown;
            zData.idleClip = idle.name;
            zData.moveClip = move.name;
            zData.attackClip = attack.name;
            zData.hitClip = (hit != null ? hit : idle).name;
            zData.deathClip = death.name;

            // 8. Build the VAT render prefab ----------------------------------------------------
            // Root = logic + physics (NavMeshAgent, collider, ZombieBase). It stays axis-aligned so the
            // capsule collider and agent are never skewed by the model's facing.
            var go = new GameObject(cfg.prefabName);
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = cfg.agentRadius;
            agent.height = cfg.agentHeight;
            agent.speed = cfg.moveSpeed;
            agent.stoppingDistance = cfg.attackRange * 0.8f;

            var col = go.AddComponent<CapsuleCollider>();
            col.radius = cfg.agentRadius;
            col.height = cfg.agentHeight;
            col.center = new Vector3(0f, cfg.agentHeight * 0.5f, 0f);

            // Visual = mesh + VAT driver, parented under the root so it can be rotated/offset freely
            // (VAT_Animator requires a MeshRenderer on its own GameObject).
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = vatData.bakedMesh;
            var mr = visual.AddComponent<MeshRenderer>();
            mr.sharedMaterial = vatMat;
            var vatAnim = visual.AddComponent<VAT_Animator>();
            vatAnim.animationData = vatData;
            vatAnim.defaultClipIndex = 0;

            // Adding the ZombieBase-derived component auto-adds Health; it fetches VAT_Animator via
            // GetComponentInChildren, so the driver being on the child is fine.
            var zb = go.AddComponent(cfg.componentType);

            var so = new SerializedObject(zb);
            SetRef(so, "data", zData);
            SetRef(so, "bodyRenderer", mr);
            so.ApplyModifiedPropertiesWithoutUndo();

            zData.prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{cfg.prefabName}.prefab");
            UnityEngine.Object.DestroyImmediate(go);

            EditorUtility.SetDirty(zData);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VATBake] ✔ {cfg.sourceName}: clips[{string.Join(", ", ordered.Select(c => c.name))}] " +
                      $"-> {dataAssetPath} + {cfg.prefabName}.prefab", zData);
            return true;
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
