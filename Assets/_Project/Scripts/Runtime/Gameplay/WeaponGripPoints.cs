using UnityEngine;

namespace ZombieWar
{
    // Lives on each weapon model prefab. The Animation Rigging Two-Bone IK constraints (set up in
    // the Editor, see Docs checklist) target rightHandGrip/leftHandGrip so hand pose stays correct
    // per weapon regardless of the model's own geometry.
    public class WeaponGripPoints : MonoBehaviour
    {
        [SerializeField] private Transform rightHandGrip;
        [SerializeField] private Transform leftHandGrip;
        [SerializeField] private Transform muzzlePoint;

        public Transform RightHandGrip => rightHandGrip;
        public Transform LeftHandGrip => leftHandGrip;
        public Transform MuzzlePoint => muzzlePoint;
    }
}
