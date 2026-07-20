using UnityEngine;
using UnityEngine.Animations.Rigging;
using BillGameCore;

namespace ZombieWar
{
    // Data-driven weapon hand IK. Final architecture (researched against the installed
    // com.unity.animation.rigging@1.4.1 source - see Docs/PlayerRigSocketIncident.md):
    //
    //   WeaponRig (Rig)
    //     ChestAim   (MultiAimConstraint:   chest    <- AimTarget)        evaluated 1st
    //     GunFollow  (MultiParentConstraint: GunMount <- chest, offsets)  evaluated 2nd
    //     RightHandIK (TwoBoneIK -> RightHandTarget)                      evaluated 3rd
    //     LeftHandIK  (TwoBoneIK -> LeftHandTarget)                       evaluated 4th
    //     GunMount / RecoilPivot / RightHandTarget + LeftHandTarget (children of RecoilPivot)
    //
    // The weapon model is instantiated under RecoilPivot at runtime. It is NOT referenced by any
    // constraint, so spawning/destroying it never invalidates the rig graph. The hand targets are
    // persistent children of RecoilPivot: on equip we compute each grip's pose in RecoilPivot space
    // ONCE (pure math - never SetParent) and write it to the target's local transform. Because
    // RigSyncSceneToStreamJob copies bound transforms' LOCAL TRS into the stream and world poses
    // are composed through the STREAM hierarchy, the targets automatically ride GunFollow's output
    // (aim + recoil) in the SAME frame - zero lag, and no circular dependency: the arms depend on
    // the gun, the gun depends only on the chest.
    //
    // HARD RULE (root cause of a past per-frame PropertyStreamHandle exception): constraint-bound
    // transforms are bound ONCE at RigBuilder.Build() (Animator.BindStreamTransform). NEVER
    // reparent rightHandTarget/leftHandTarget (or any rig object) at runtime.
    [RequireComponent(typeof(Weapon))]
    public class WeaponIKController : MonoBehaviour
    {
        [Header("Rig constraints (assigned by PlayerRigBuilder / Editor)")]
        [SerializeField] private TwoBoneIKConstraint rightHandIK;
        [SerializeField] private TwoBoneIKConstraint leftHandIK;
        [SerializeField] private MultiAimConstraint aimConstraint;

        [Header("Persistent rig transforms (children of WeaponRig - NEVER reparented)")]
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform aimTarget;

        [Header("Aim")]
        [Tooltip("How far in front of the chest the Multi-Aim source point is projected.")]
        [SerializeField] private float aimProjectDistance = 8f;
        [SerializeField] private Transform aimOrigin; // chest pivot; falls back to this.transform

        private Weapon _weapon;
        private Animator _animator;
        private static readonly int AimTypeHash = Animator.StringToHash("AimType");

        private void Awake()
        {
            _weapon = GetComponent<Weapon>();
            _animator = GetComponentInParent<Animator>();
            if (aimOrigin == null) aimOrigin = transform;
        }

        private void OnEnable()
        {
            if (_weapon == null) return;
            _weapon.OnWeaponEquipped += HandleWeaponEquipped;
            // Sync immediately so AimType/hand-IK match the already-equipped weapon even if the first
            // EquipWeapon fired before this component subscribed (spawn-order independence).
            HandleWeaponEquipped(_weapon.CurrentGrips);
        }

        private void OnDisable()
        {
            if (_weapon != null) _weapon.OnWeaponEquipped -= HandleWeaponEquipped;
            SetWeight(rightHandIK, 0f);
            SetWeight(leftHandIK, 0f);
        }

        // Aim the persistent targets at the new weapon's grips. The grips are rigid relative to
        // RecoilPivot (the model doesn't animate internally), so one snapshot per equip suffices -
        // afterwards the targets track aim/recoil through the stream hierarchy on their own.
        private void HandleWeaponEquipped(WeaponGripPoints grips)
        {
            // One-handed (pistol/SMG) vs two-handed (rifle/shotgun/sniper/LMG).
            bool twoHanded = _weapon != null && _weapon.Current != null && _weapon.Current.twoHanded;

            // Drive the UpperBody aim blend: 0 = pistol one-handed pose, 1 = rifle two-handed pose.
            if (_animator != null) _animator.SetFloat(AimTypeHash, twoHanded ? 1f : 0f);

            bool rightOk = grips != null && grips.RightHandGrip != null
                && SnapTargetPositionToGrip(rightHandTarget, grips.RightHandGrip);
            SetWeight(rightHandIK, rightOk ? 1f : 0f);

            // Front grip -> left hand (the "support"). Off for one-handed weapons via
            // WeaponData.twoHanded, or when the model simply has no left grip marker.
            bool leftOk = twoHanded && grips != null && grips.LeftHandGrip != null
                && SnapTargetPositionToGrip(leftHandTarget, grips.LeftHandGrip);
            SetWeight(leftHandIK, leftOk ? 1f : 0f);
        }

        // Grip markers define only WHERE a hand contacts the weapon. Wrist orientation belongs to
        // the IK rig's persistent hand target and must not inherit arbitrary model/prefab axes.
        private static bool SnapTargetPositionToGrip(Transform target, Transform grip)
        {
            if (target == null || target.parent == null) return false;
            var parent = target.parent; // RecoilPivot
            target.localPosition = parent.InverseTransformPoint(grip.position);
            return true;
        }

        private static void SetWeight(TwoBoneIKConstraint c, float w)
        {
            if (c != null) c.weight = w;
        }

#if UNITY_EDITOR
        // Editor authoring hooks. Runtime gameplay still snapshots once on equip because the
        // weapon model and targets share RecoilPivot. WeaponPoseAuthoring may request repeated
        // sync while an artist moves the live weapon model in Play Mode.
        public Transform EditorRightHandTarget => rightHandTarget;
        public Transform EditorLeftHandTarget => leftHandTarget;

        public void EditorSyncTargetsToCurrentGrips()
        {
            if (_weapon == null) _weapon = GetComponent<Weapon>();
            HandleWeaponEquipped(_weapon != null ? _weapon.CurrentGrips : null);
        }
#endif

        private void Update()
        {
            if (aimTarget == null) return;

            // Project the Multi-Aim source in front of the body along the current aim direction so
            // the chest (and, via GunFollow, the mounted weapon) yaws toward the nearest zombie.
            // Runs in Update = before the Animator/rig graph evaluates -> consumed the same frame.
            var player = PlayerMovement.Instance;
            Vector3 dir = player != null ? player.AimDirection : aimOrigin.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = aimOrigin.forward;

            Vector3 origin = aimOrigin.position;
            aimTarget.position = origin + dir.normalized * aimProjectDistance;
        }
    }
}
