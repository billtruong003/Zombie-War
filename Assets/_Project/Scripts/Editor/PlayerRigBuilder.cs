using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEditor;
using ZombieWar;

namespace ZombieWar.EditorTools
{
    // Builds the full weapon-aim rig on a humanoid player root. Final architecture (verified
    // against com.unity.animation.rigging@1.4.1 source - constraints evaluate in hierarchy order
    // under the Rig, and RigSyncSceneToStreamJob composes bound transforms' world poses through
    // the STREAM hierarchy, so an earlier constraint's output feeds later ones in the same frame):
    //
    //   WeaponRig (Rig)
    //     ChestAim    MultiAimConstraint:    chest    <- AimTarget            [1]
    //     GunFollow   MultiParentConstraint: GunMount <- chest (offsets)      [2]
    //     RightHandIK TwoBoneIK:             right arm -> RightHandTarget     [3]
    //     LeftHandIK  TwoBoneIK:             left arm  -> LeftHandTarget      [4]
    //     GunMount
    //       RecoilPivot                (Weapon kicks this in Update - recoil)
    //         RightHandTarget          (persistent; WeaponIKController snaps local pose per equip)
    //         LeftHandTarget           (persistent; idem)
    //         <weapon model spawns here at runtime - NOT rig-referenced, safe to swap freely>
    //     AimTarget / RightElbowHint / LeftElbowHint
    //
    // GunMount must NEVER be a descendant of a hand bone: the hands IK toward the weapon's grips,
    // so a hand-descendant mount is a circular dependency (the historical bug). The legacy
    // RightHand/WeaponSocket object is left untouched for old tooling but is NOT used at runtime.
    //
    // Constraint-bound transforms are bound once at RigBuilder.Build() (play-mode OnEnable) -
    // everything referenced by constraints is authored here, ahead of time, and never reparented.
    //
    // Idempotent: every rig object/component is found-or-created by identity; re-running creates
    // no duplicates and re-produces the same serialized state.
    public static class PlayerRigBuilder
    {
        public static void BuildWeaponRig(GameObject playerRoot)
        {
            var animator = playerRoot.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("[PlayerRigBuilder] Player root needs a Humanoid Animator.");
                return;
            }

            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest)
                              ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform rUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform rLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform lUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform lLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);

            if (chest == null || rHand == null || lHand == null)
            {
                Debug.LogError("[PlayerRigBuilder] Missing required bones (chest/hands).");
                return;
            }

            var summary = new List<string>();

            // --- Rig root ---
            // CRITICAL: every constraint object/target must live under animator.avatarRoot, not
            // merely under the Animator's GameObject. ReadWriteTransformHandle.Bind (and property
            // stream binding) hard-fails with "not a child of the Animator hierarchy" otherwise -
            // this player's Avatar is lifted from the CharacterModel FBX, so avatarRoot is
            // Player/CharacterModel, NOT Player. Verified against TransformHandle.cs:153 in
            // com.unity.animation.rigging@1.4.1. This was the true root cause of the historical
            // "Could not resolve WeaponRig/ChestAim" + PropertyStreamHandle exceptions.
            Transform avatarRoot = animator.avatarRoot != null ? animator.avatarRoot : playerRoot.transform;

            var rigBuilder = playerRoot.GetComponent<RigBuilder>();
            if (rigBuilder == null) { rigBuilder = Undo.AddComponent<RigBuilder>(playerRoot); summary.Add("created RigBuilder"); }
            else summary.Add("reused RigBuilder");

            // Migration: earlier revisions authored WeaponRig directly under the player root
            // (outside avatarRoot). Editor-time reparent is safe; the graph binds at play start.
            var strayRig = playerRoot.transform.Find("WeaponRig");
            if (strayRig != null && avatarRoot != playerRoot.transform)
            {
                Undo.SetTransformParent(strayRig, avatarRoot, "Move WeaponRig under avatarRoot");
                summary.Add("moved WeaponRig under avatarRoot (" + GetPath(avatarRoot) + ")");
            }

            var rigGo = FindOrCreateChild(avatarRoot, "WeaponRig", summary);
            var rig = rigGo.GetComponent<Rig>();
            if (rig == null) { rig = Undo.AddComponent<Rig>(rigGo.gameObject); summary.Add("created Rig on WeaponRig"); }

            if (rigBuilder.layers.Count != 1 || rigBuilder.layers[0].rig != rig)
            {
                rigBuilder.layers.Clear();
                rigBuilder.layers.Add(new RigLayer(rig, true));
                summary.Add("(re)set RigBuilder.layers to [WeaponRig]");
            }
            else summary.Add("RigBuilder.layers already [WeaponRig]");

            // --- GunMount + RecoilPivot + persistent hand targets (children of RecoilPivot) ---
            var gunMount = FindOrCreateChild(rigGo, "GunMount", summary, created =>
            {
                // Default seed ONLY when creating a missing mount: mid-shoulders, slightly below,
                // ~0.26m forward - inside both chibi arms' ~0.33m reach. This is a documented
                // initial default; the AUTHORED pose lives in the prefab and an existing GunMount
                // is never touched on re-runs (idempotency).
                Vector3 mid = (rUpper.position + lUpper.position) * 0.5f;
                created.SetPositionAndRotation(
                    mid + playerRoot.transform.forward * 0.26f
                        - playerRoot.transform.up * 0.05f
                        + playerRoot.transform.right * 0.05f,
                    playerRoot.transform.rotation);
            });
            var recoilPivot = FindOrCreateChild(gunMount, "RecoilPivot", summary);
            // RecoilPivot sits BETWEEN the stream-driven GunMount and the stream-synced hand
            // targets, but is itself only moved by game code (the recoil spring). Without a
            // RigTransform marker its scene motion would be invisible to the stream (the sync job
            // only pushes constraint-referenced + RigTransform-marked transforms), so the hands
            // would ignore recoil. Verified against RigUtils.GetSyncableRigTransforms in 1.4.1.
            if (recoilPivot.GetComponent<RigTransform>() == null)
            {
                Undo.AddComponent<RigTransform>(recoilPivot.gameObject);
                summary.Add("added RigTransform marker to RecoilPivot");
            }
            var rTarget = FindOrCreateChild(recoilPivot, "RightHandTarget", summary);
            var lTarget = FindOrCreateChild(recoilPivot, "LeftHandTarget", summary);

            // Migration: earlier revisions authored the hand targets directly under WeaponRig.
            // Editor-time reparent (before any graph exists) is safe; runtime reparent never is.
            AdoptStrayTarget(rigGo, recoilPivot, ref rTarget, "RightHandTarget", summary);
            AdoptStrayTarget(rigGo, recoilPivot, ref lTarget, "LeftHandTarget", summary);

            // --- Aim target (moved at runtime by WeaponIKController) + elbow hints ---
            Transform aimTarget = FindOrCreateChild(rigGo, "AimTarget", summary);
            aimTarget.position = chest.position + playerRoot.transform.forward * 8f;

            // Hints are AUTHORED pose data: seed a sane default only on creation, then preserve
            // whatever the artist tuned (re-running the builder must never stomp authored poses).
            Transform rHint = FindOrCreateChild(rigGo, "RightElbowHint", summary, created =>
                created.position = rUpper.position + playerRoot.transform.right * 0.16f
                                 - playerRoot.transform.up * 0.18f - playerRoot.transform.forward * 0.06f);
            Transform lHint = FindOrCreateChild(rigGo, "LeftElbowHint", summary, created =>
                created.position = lUpper.position - playerRoot.transform.right * 0.16f
                                 - playerRoot.transform.up * 0.18f - playerRoot.transform.forward * 0.06f);

            // --- [1] Multi-Aim on chest ---
            // Axes are MEASURED from this Avatar's actual chest bone (QuickRig bind pose), not
            // assumed: with the rig disabled, root-forward expressed in chest-local space is
            // dominantly +Y (0.926) and world-up is dominantly -X (-0.993). So anatomical forward
            // = local +Y, anatomical up = local -X, and anatomical YAW is rotation about local X —
            // hence only the X channel is constrained (yaw-only upper-body aim; the player ROOT
            // already yaws fully toward AimDirection in PlayerMovement, ChestAim adds residual
            // lead/lag only). maintainOffset preserves the bind orientation so the constraint never
            // twists the torso toward its own axes (the old aimAxis=Z config did exactly that:
            // backward lean + sky-facing head). Limits clamp the residual turn.
            var aimGo = FindOrCreateChild(rigGo, "ChestAim", summary);
            var aim = aimGo.GetComponent<MultiAimConstraint>();
            if (aim == null) { aim = Undo.AddComponent<MultiAimConstraint>(aimGo.gameObject); summary.Add("created MultiAimConstraint on ChestAim"); }
            var aimData = aim.data;
            aimData.constrainedObject = chest;
            aimData.aimAxis = MultiAimConstraintData.Axis.Y;
            aimData.upAxis = MultiAimConstraintData.Axis.X_NEG;
            aimData.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;
            var aimSources = new WeightedTransformArray();
            aimSources.Add(new WeightedTransform(aimTarget, 1f));
            aimData.sourceObjects = aimSources;
            aimData.maintainOffset = true;
            aimData.limits = new Vector2(-60f, 60f);
            aimData.constrainedXAxis = true;
            aimData.constrainedYAxis = false;
            aimData.constrainedZAxis = false;
            aim.data = aimData;
            // WEIGHT 0 - deliberate. The player ROOT already yaws fully to AimDirection
            // (PlayerMovement.MoveRotation) and the AimType animation authors the upper-body
            // stance, so a procedural chest-aim adds nothing here - and measured runtime proof
            // showed maintainOffset captures against the BIND pose at graph build while the aim
            // animation holds the spine far from bind, so ANY nonzero weight drags the torso
            // sideways (face-backward bug). Keep the constraint authored for future use (e.g.
            // vertical aim), but it must stay 0 unless that capture problem is redesigned.
            aim.weight = 0f;

            // --- [2] GunFollow: GunMount rides the chest in-stream (aim carries the gun) ---
            var followGo = FindOrCreateChild(rigGo, "GunFollow", summary);
            var follow = followGo.GetComponent<MultiParentConstraint>();
            if (follow == null) { follow = Undo.AddComponent<MultiParentConstraint>(followGo.gameObject); summary.Add("created MultiParentConstraint on GunFollow"); }
            var followData = follow.data;
            followData.constrainedObject = gunMount;
            var followSources = new WeightedTransformArray();
            followSources.Add(new WeightedTransform(chest, 1f));
            followData.sourceObjects = followSources;
            // POSITION-ONLY follow. Rotation must stay root-aligned: the player root already yaws
            // to AimDirection, so an unrotated mount keeps the barrel on the firing line at all
            // times. Rotation-follow is unusable here - maintainRotationOffset captures the offset
            // against the chest's BIND pose at graph build, but the aim animation holds the chest
            // ~90 deg away from bind (Maya bone axes), which yawed the whole gun sideways.
            followData.maintainPositionOffset = true;   // keep GunMount's authored pose relative to chest
            followData.maintainRotationOffset = false;
            followData.constrainedPositionXAxis = true;
            followData.constrainedPositionYAxis = true;
            followData.constrainedPositionZAxis = true;
            followData.constrainedRotationXAxis = false;
            followData.constrainedRotationYAxis = false;
            followData.constrainedRotationZAxis = false;
            follow.data = followData;

            // --- [3][4] Two-Bone IK per arm toward the persistent targets ---
            var rIK = BuildTwoBone(rigGo, "RightHandIK", rUpper, rLower, rHand, rTarget, rHint, summary);
            var lIK = BuildTwoBone(rigGo, "LeftHandIK", lUpper, lLower, lHand, lTarget, lHint, summary);

            // Constraint evaluation = hierarchy order under the Rig. Enforce it deterministically.
            int order = 0;
            aimGo.SetSiblingIndex(order++);
            followGo.SetSiblingIndex(order++);
            rIK.transform.SetSiblingIndex(order++);
            lIK.transform.SetSiblingIndex(order++);

            // --- Wire Weapon + WeaponIKController refs ---
            var weapon = playerRoot.GetComponent<Weapon>();
            if (weapon != null)
            {
                SetRef(weapon, "weaponMount", gunMount);
                SetRef(weapon, "recoilPivot", recoilPivot);
            }

            var ik = playerRoot.GetComponent<WeaponIKController>();
            if (ik == null) { ik = Undo.AddComponent<WeaponIKController>(playerRoot); summary.Add("created WeaponIKController"); }
            else summary.Add("reused WeaponIKController");
            SetRef(ik, "rightHandIK", rIK);
            SetRef(ik, "leftHandIK", lIK);
            SetRef(ik, "aimConstraint", aim);
            SetRef(ik, "rightHandTarget", rTarget);
            SetRef(ik, "leftHandTarget", lTarget);
            SetRef(ik, "aimTarget", aimTarget);
            SetRef(ik, "aimOrigin", chest);

            Debug.Log("[PlayerRigBuilder] Weapon rig built on " + playerRoot.name +
                " | gunMount=" + GetPath(gunMount) +
                "\n  " + string.Join("\n  ", summary));
        }

        // Re-homes a target that an earlier rig revision authored directly under WeaponRig. If both
        // the old (rig-level) and new (RecoilPivot-level) objects exist, the stray duplicate is
        // deleted and the pivot-level one wins.
        private static void AdoptStrayTarget(Transform rigGo, Transform recoilPivot, ref Transform target, string name, List<string> summary)
        {
            var stray = rigGo.Find(name);
            if (stray == null || stray == target) return;
            if (target != null && target.parent == recoilPivot)
            {
                Undo.DestroyObjectImmediate(stray.gameObject);
                summary.Add("deleted stray duplicate " + name + " under WeaponRig");
                return;
            }
            Undo.SetTransformParent(stray, recoilPivot, "Adopt " + name);
            stray.localPosition = Vector3.zero;
            stray.localRotation = Quaternion.identity;
            target = stray;
            summary.Add("moved " + name + " under RecoilPivot");
        }

        private static TwoBoneIKConstraint BuildTwoBone(Transform parent, string name,
            Transform root, Transform mid, Transform tip, Transform target, Transform hint, List<string> summary)
        {
            var go = FindOrCreateChild(parent, name, summary);
            var c = go.GetComponent<TwoBoneIKConstraint>();
            if (c == null) { c = Undo.AddComponent<TwoBoneIKConstraint>(go.gameObject); summary.Add("created TwoBoneIKConstraint on " + name); }
            var d = c.data;
            d.root = root;
            d.mid = mid;
            d.tip = tip;
            d.target = target;
            d.hint = hint;
            d.targetPositionWeight = 1f;
            d.targetRotationWeight = 1f;
            d.hintWeight = 1f;
            c.data = d;
            return c;
        }

        private static Transform FindOrCreateChild(Transform parent, string name, List<string> summary,
            System.Action<Transform> onCreated = null)
        {
            var t = parent.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
                t = go.transform;
                t.SetParent(parent, false);
                onCreated?.Invoke(t);
                summary.Add("created " + name);
            }
            return t;
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            for (var cur = t.parent; cur != null; cur = cur.parent) path = cur.name + "/" + path;
            return path;
        }

        // Assign a private [SerializeField] object reference from editor code.
        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning("[PlayerRigBuilder] Field not found: " + field + " on " + target.GetType().Name);
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
