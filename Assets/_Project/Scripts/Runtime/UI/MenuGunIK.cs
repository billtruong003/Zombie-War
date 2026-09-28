using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>Puts the menu character's hands on the showcase gun's grips (humanoid IK; the
    /// controller layer has IK Pass on). Weight 0 leaves the idle untouched.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class MenuGunIK : MonoBehaviour
    {
        public Transform right, left;
        public float weight;

        Animator _animator;

        void Awake() => _animator = GetComponent<Animator>();

        void OnAnimatorIK(int layer)
        {
            Set(AvatarIKGoal.RightHand, right);
            Set(AvatarIKGoal.LeftHand, left);
        }

        void Set(AvatarIKGoal goal, Transform t)
        {
            float w = t != null ? weight : 0f;
            _animator.SetIKPositionWeight(goal, w);
            if (t != null) _animator.SetIKPosition(goal, t.position);
        }
    }
}
