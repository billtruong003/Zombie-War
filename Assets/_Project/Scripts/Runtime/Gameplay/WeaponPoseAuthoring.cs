using UnityEngine;

namespace ZombieWar
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Weapon), typeof(WeaponIKController))]
    public sealed class WeaponPoseAuthoring : MonoBehaviour
    {
        [Tooltip("Editor-only preview. While enabled in Play Mode, moving the equipped weapon " +
                 "continuously re-snaps both IK targets to its grip markers.")]
        [SerializeField] private bool liveSyncTargetsToGrips;

        public bool LiveSyncTargetsToGrips
        {
            get => liveSyncTargetsToGrips;
            set => liveSyncTargetsToGrips = value;
        }

#if UNITY_EDITOR
        private WeaponIKController _ik;

        private void Update()
        {
            if (!Application.isPlaying || !liveSyncTargetsToGrips) return;
            if (_ik == null) _ik = GetComponent<WeaponIKController>();
            _ik.EditorSyncTargetsToCurrentGrips();
        }
#endif
    }
}
