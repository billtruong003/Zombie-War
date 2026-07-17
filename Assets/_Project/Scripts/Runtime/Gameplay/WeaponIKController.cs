using UnityEngine;
using UnityEngine.Animations.Rigging;
using BillGameCore;

namespace ZombieWar
{
    // Data-driven weapon hand IK (convention B: weapon lives on an independent socket under the
    // chest, NOT under a hand bone, so both hands can IK to it without a circular dependency).
    //
    // Rig evaluation order (top-to-bottom under the Rig GameObject) MUST be:
    //   1) MultiAimConstraint  -> yaws the chest toward AimDirection, carrying the weapon socket.
    //   2) TwoBoneIKConstraint (right hand) -> rear grip.
    //   3) TwoBoneIKConstraint (left hand)  -> front grip (foregrip) if present.
    //
    // On weapon swap we re-point the IK targets to the new weapon's WeaponGripPoints:
    //   - 2 grips (rifle)  -> right hand = rear grip, left hand = front grip (weight 1).
    //   - 1 grip  (pistol) -> right hand = grip; left hand disabled (weight 0), unless the pistol
    //                         prefab also authored a leftHandGrip for a two-handed cup.
    [RequireComponent(typeof(Weapon))]
    public class WeaponIKController : MonoBehaviour
    {
        [Header("Rig constraints (assigned by PlayerRigBuilder / Editor)")]
        [SerializeField] private TwoBoneIKConstraint rightHandIK;
        [SerializeField] private TwoBoneIKConstraint leftHandIK;
        [SerializeField] private MultiAimConstraint aimConstraint;

        [Header("Driven targets (children of the Rig)")]
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform aimTarget;

        [Header("Aim")]
        [Tooltip("How far in front of the chest the Multi-Aim source point is projected.")]
        [SerializeField] private float aimProjectDistance = 8f;
        [SerializeField] private Transform aimOrigin; // chest/socket pivot; falls back to this.transform

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
        }

        // Re-point the hand IK targets so the grip stays correct per weapon geometry.
        private void HandleWeaponEquipped(WeaponGripPoints grips)
        {
            // One-handed (pistol/SMG) vs two-handed (rifle/shotgun/sniper/LMG).
            bool twoHanded = _weapon != null && _weapon.Current != null && _weapon.Current.twoHanded;

            // Drive the UpperBody aim blend: 0 = pistol one-handed pose, 1 = rifle two-handed pose.
            if (_animator != null) _animator.SetFloat(AimTypeHash, twoHanded ? 1f : 0f);

            if (grips == null)
            {
                SetWeight(rightHandIK, 0f);
                SetWeight(leftHandIK, 0f);
                return;
            }

            // Rear grip -> right hand (the "primary"). Always present for a valid weapon.
            if (grips.RightHandGrip != null && rightHandTarget != null)
            {
                Attach(rightHandTarget, grips.RightHandGrip);
                SetWeight(rightHandIK, 1f);
            }
            else
            {
                SetWeight(rightHandIK, 0f);
            }

            // Front grip -> left hand (the "support"). Off for one-handed weapons (SMG/pistol) via
            // WeaponData.twoHanded, or when the model simply has no left grip marker.
            if (twoHanded && grips.LeftHandGrip != null && leftHandTarget != null)
            {
                Attach(leftHandTarget, grips.LeftHandGrip);
                SetWeight(leftHandIK, 1f);
            }
            else
            {
                SetWeight(leftHandIK, 0f);
            }
        }

        // Parent the (rig-bound) target onto the grip so it tracks the weapon automatically once the
        // Multi-Aim has carried the socket. The Transform reference stays valid for the rig binding.
        private static void Attach(Transform target, Transform grip)
        {
            target.SetParent(grip, false);
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
        }

        private static void SetWeight(TwoBoneIKConstraint c, float w)
        {
            if (c != null) c.weight = w;
        }

        private void Update()
        {
            if (aimTarget == null) return;

            // Project the Multi-Aim source in front of the body along the current aim direction so the
            // chest (and the socketed weapon) yaws to face the nearest zombie / move direction.
            var player = PlayerMovement.Instance;
            Vector3 dir = player != null ? player.AimDirection : aimOrigin.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = aimOrigin.forward;

            Vector3 origin = aimOrigin.position;
            aimTarget.position = origin + dir.normalized * aimProjectDistance;
        }
    }
}
