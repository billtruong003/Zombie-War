using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEditor;
using ZombieWar;

namespace ZombieWar.EditorTools
{
    // Builds the full weapon-aim rig on a humanoid player root (convention B):
    //   Chest (Multi-Aim, yaw toward aim target)
    //     └─ WeaponSocket (independent of hands -> no circular IK dependency)
    //   Right/Left arm chains driven by Two-Bone IK to the equipped weapon's grip points.
    //
    // Bones are resolved via the Humanoid Avatar (Animator.GetBoneTransform), so this works for any
    // Layer Lab character regardless of the QuickRigCharacter2_ bone prefix.
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

            // --- Weapon socket: child of chest, in front of the body at chest height ---
            Transform socket = FindOrCreate(chest, "WeaponSocket");
            socket.localPosition = new Vector3(0f, 0f, 0.35f);
            socket.localRotation = Quaternion.identity;

            // --- Rig root ---
            var rigBuilder = playerRoot.GetComponent<RigBuilder>();
            if (rigBuilder == null) rigBuilder = playerRoot.AddComponent<RigBuilder>();

            var rigGo = FindOrCreateChild(playerRoot.transform, "WeaponRig");
            var rig = rigGo.GetComponent<Rig>();
            if (rig == null) rig = rigGo.gameObject.AddComponent<Rig>();

            rigBuilder.layers.Clear();
            rigBuilder.layers.Add(new RigLayer(rig, true));

            // --- Aim target (moved at runtime by WeaponIKController) ---
            Transform aimTarget = FindOrCreateChild(rigGo, "AimTarget");
            aimTarget.position = chest.position + playerRoot.transform.forward * 8f;

            // --- Multi-Aim on chest (evaluated FIRST) ---
            var aimGo = FindOrCreateChild(rigGo, "ChestAim");
            var aim = aimGo.GetComponent<MultiAimConstraint>();
            if (aim == null) aim = aimGo.gameObject.AddComponent<MultiAimConstraint>();
            var aimData = aim.data;
            aimData.constrainedObject = chest;
            aimData.aimAxis = MultiAimConstraintData.Axis.Z;
            aimData.upAxis = MultiAimConstraintData.Axis.Y;
            aimData.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;
            var sources = new WeightedTransformArray();
            sources.Add(new WeightedTransform(aimTarget, 1f));
            aimData.sourceObjects = sources;
            aimData.constrainedXAxis = true;
            aimData.constrainedYAxis = true;
            aimData.constrainedZAxis = true;
            aim.data = aimData;

            // --- Two-Bone IK: right hand (rear grip), evaluated AFTER aim ---
            Transform rTarget = FindOrCreateChild(rigGo, "RightHandTarget");
            Transform rHint = FindOrCreateChild(rigGo, "RightElbowHint");
            rHint.position = rLower != null ? rLower.position + playerRoot.transform.forward * -0.3f + Vector3.down * 0.3f : rHand.position;
            var rIK = BuildTwoBone(rigGo, "RightHandIK", rUpper, rLower, rHand, rTarget, rHint);

            // --- Two-Bone IK: left hand (front grip / foregrip) ---
            Transform lTarget = FindOrCreateChild(rigGo, "LeftHandTarget");
            Transform lHint = FindOrCreateChild(rigGo, "LeftElbowHint");
            lHint.position = lLower != null ? lLower.position + playerRoot.transform.forward * -0.3f + Vector3.down * 0.3f : lHand.position;
            var lIK = BuildTwoBone(rigGo, "LeftHandIK", lUpper, lLower, lHand, lTarget, lHint);

            // --- Wire Weapon.weaponSocket + WeaponIKController refs ---
            var weapon = playerRoot.GetComponent<Weapon>();
            if (weapon != null) SetRef(weapon, "weaponSocket", socket);

            var ik = playerRoot.GetComponent<WeaponIKController>();
            if (ik == null) ik = playerRoot.AddComponent<WeaponIKController>();
            SetRef(ik, "rightHandIK", rIK);
            SetRef(ik, "leftHandIK", lIK);
            SetRef(ik, "aimConstraint", aim);
            SetRef(ik, "rightHandTarget", rTarget);
            SetRef(ik, "leftHandTarget", lTarget);
            SetRef(ik, "aimTarget", aimTarget);
            SetRef(ik, "aimOrigin", chest);

            Debug.Log("[PlayerRigBuilder] Weapon rig built on " + playerRoot.name);
        }

        private static TwoBoneIKConstraint BuildTwoBone(Transform parent, string name,
            Transform root, Transform mid, Transform tip, Transform target, Transform hint)
        {
            var go = FindOrCreateChild(parent, name);
            var c = go.GetComponent<TwoBoneIKConstraint>();
            if (c == null) c = go.gameObject.AddComponent<TwoBoneIKConstraint>();
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

        private static Transform FindOrCreate(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }
            return t;
        }

        private static Transform FindOrCreateChild(Transform parent, string name) => FindOrCreate(parent, name);

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
