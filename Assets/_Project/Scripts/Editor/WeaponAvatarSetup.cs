using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace _Project.EditorTools
{
    /// <summary>
    /// One-shot wiring for the player weapon presentation:
    ///  - Upper-Body Avatar Mask (so an aim layer only drives spine/arms/hands, legs keep strafing)
    ///  - Adds an "UpperBody" override layer to PlayerAnimator playing the Malbers pistol aim pose
    ///  - Reparents WeaponSocket into the RightHand bone with a palm offset
    ///  - Repoints WD_Pistol muzzle/impact FX to Epic Toon FX prefabs
    /// Idempotent: safe to re-run.
    /// </summary>
    public static class WeaponAvatarSetup
    {
        const string MaskPath   = "Assets/_Project/Animations/UpperBody.mask";
        const string CtrlPath   = "Assets/_Project/Animations/PlayerAnimator.controller";
        const string AimFbx     = "Assets/ThirdParty/MalbersHumanAnims/Weapons/Pistol/H_Weapon_Pistol_AimFire.FBX";
        const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";
        const string PistolWeaponId = "weapon.sidearm.pistol_a";

        const string MuzzleFx = "Assets/ThirdParty/Epic Toon FX/Prefabs/Combat/Muzzleflash/BulletMuzzle/BulletMuzzleFire.prefab";
        const string ImpactFx = "Assets/ThirdParty/Epic Toon FX/Prefabs/Combat/Explosions/BulletExplosion/BulletExplosionFire.prefab";

        [MenuItem("ZombieWar/Weapon/1. Build Upper-Body Aim Layer + Mask")]
        public static void BuildAimLayer()
        {
            var log = new StringBuilder("=== Build Upper-Body Aim Layer ===\n");

            // --- 1) Avatar mask ---
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, MaskPath); }
            var parts = new (AvatarMaskBodyPart p, bool on)[]{
                (AvatarMaskBodyPart.Root,false),(AvatarMaskBodyPart.Body,true),(AvatarMaskBodyPart.Head,true),
                (AvatarMaskBodyPart.LeftLeg,false),(AvatarMaskBodyPart.RightLeg,false),
                (AvatarMaskBodyPart.LeftArm,true),(AvatarMaskBodyPart.RightArm,true),
                (AvatarMaskBodyPart.LeftFingers,true),(AvatarMaskBodyPart.RightFingers,true),
                (AvatarMaskBodyPart.LeftFootIK,false),(AvatarMaskBodyPart.RightFootIK,false),
                (AvatarMaskBodyPart.LeftHandIK,true),(AvatarMaskBodyPart.RightHandIK,true),
            };
            foreach (var e in parts) mask.SetHumanoidBodyPartActive(e.p, e.on);
            EditorUtility.SetDirty(mask);
            log.AppendLine("Mask OK: " + MaskPath);

            // --- 2) Aim clip ---
            var aimClip = AssetDatabase.LoadAllAssetsAtPath(AimFbx)
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            log.AppendLine("Aim clip: " + (aimClip != null ? aimClip.name : "NULL (check FBX humanoid import)"));

            // --- 3) Animator layer ---
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            if (ctrl == null) { Debug.LogError("No controller at " + CtrlPath); return; }
            log.AppendLine("Base layers: " + string.Join(", ", ctrl.layers.Select(l => l.name)));

            for (int i = ctrl.layers.Length - 1; i >= 0; i--)
                if (ctrl.layers[i].name == "UpperBody") ctrl.RemoveLayer(i);

            ctrl.AddLayer("UpperBody");
            var layers = ctrl.layers;
            var ub = layers[layers.Length - 1];
            ub.avatarMask = mask;
            ub.defaultWeight = 1f;
            ub.blendingMode = AnimatorLayerBlendingMode.Override;
            var st = ub.stateMachine.AddState("Aim");
            st.motion = aimClip;
            ub.stateMachine.defaultState = st;
            layers[layers.Length - 1] = ub;
            ctrl.layers = layers;
            EditorUtility.SetDirty(ctrl);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            log.AppendLine("UpperBody layer added (weight 1, Override, masked).");
            Debug.Log(log.ToString());
        }

        [MenuItem("ZombieWar/Weapon/2. Repoint Starter Pistol FX -> Epic Toon FX")]
        public static void RepointFx()
        {
            var wd = FindByWeaponId(PistolWeaponId);
            if (wd == null) { Debug.LogError($"No WeaponData with weaponId '{PistolWeaponId}' found."); return; }
            var so = new SerializedObject(wd);
            var muzzle = AssetDatabase.LoadAssetAtPath<GameObject>(MuzzleFx);
            var impact = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactFx);
            so.FindProperty("muzzleFlashPrefab").objectReferenceValue = muzzle;
            so.FindProperty("impactPrefab").objectReferenceValue = impact;
            // smokeTrailPrefab now unused (tracer -> LineRenderer in code); clear it
            var stp = so.FindProperty("smokeTrailPrefab");
            if (stp != null) stp.objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(wd);
            AssetDatabase.SaveAssets();
            Debug.Log($"{wd.name} FX repointed. muzzle={(muzzle!=null)} impact={(impact!=null)}");
        }

        /// Resolve canonical WeaponData by stable weaponId (survives asset rename/move) instead of a
        /// hardcoded path — see WeaponRosterMigration.
        static ZombieWar.WeaponData FindByWeaponId(string weaponId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponData"))
            {
                var wd = AssetDatabase.LoadAssetAtPath<ZombieWar.WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
                if (wd != null && wd.WeaponId == weaponId) return wd;
            }
            return null;
        }
    }
}
