using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    // Grip Tuner: chinh vi tri sung tren tay ngay trong Play Mode roi CAPTURE luu vao WeaponData asset
    // (persist qua khi thoat play). Instance sung la con cua recoilPivot => local transform cua no chinh
    // la gripLocalPosition/Euler. Recoil kick pivot chu khong kick instance nen doc lai luon sach.
    [CustomEditor(typeof(Weapon))]
    public class WeaponEditor : UnityEditor.Editor
    {
        private Vector3 _pos;
        private Vector3 _euler;
        private Vector3 _scale;
        private bool _synced;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var weapon = (Weapon)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Grip Tuner", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Vao Play Mode de chinh. Sung se spawn tren tay; keo field ben duoi (hoac dung Move/Rotate " +
                    "tool tren instance trong Hierarchy) roi bam CAPTURE de luu vao WeaponData asset.",
                    MessageType.Info);
                _synced = false;
                return;
            }

            var data = weapon.Current;
            var inst = weapon.EditorInstanceTransform;
            if (data == null || inst == null)
            {
                EditorGUILayout.HelpBox("Chua co sung equip trong scene.", MessageType.Warning);
                _synced = false;
                return;
            }

            // Sync field tu instance dang song (lan dau vao Play, hoac sau khi user keo bang Scene tool).
            if (!_synced)
            {
                _pos = inst.localPosition;
                _euler = inst.localRotation.eulerAngles;
                _scale = inst.localScale;
                _synced = true;
            }

            EditorGUILayout.HelpBox($"Weapon: {data.weaponName}", MessageType.None);

            EditorGUI.BeginChangeCheck();
            _pos = EditorGUILayout.Vector3Field("Grip Local Position", _pos);
            _euler = EditorGUILayout.Vector3Field("Grip Local Euler", _euler);
            _scale = EditorGUILayout.Vector3Field("Grip Local Scale", _scale);
            if (EditorGUI.EndChangeCheck())
                weapon.EditorApplyGrip(_pos, _euler, _scale); // live preview

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pull tu Instance"))
                {
                    // User da keo sung bang Move/Rotate/Scale tool -> hut gia tri hien tai vao field.
                    _pos = inst.localPosition;
                    _euler = inst.localRotation.eulerAngles;
                    _scale = inst.localScale;
                }

                if (GUILayout.Button("CAPTURE -> WeaponData", GUILayout.Height(24)))
                {
                    Undo.RecordObject(data, "Capture Weapon Grip");
                    data.gripLocalPosition = _pos;
                    data.gripLocalEuler = _euler;
                    data.gripLocalScale = _scale;
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[GripTuner] Saved to {data.name}: pos={_pos} euler={_euler} scale={_scale}", data);
                }
            }

            EditorGUILayout.HelpBox(
                "Meo: mo Hierarchy -> chon instance sung (con cua RecoilPivot) -> dung Move/Rotate tool keo " +
                "cho vua tay -> bam 'Pull tu Instance' -> 'CAPTURE'. Gia tri luu thang vao asset.",
                MessageType.None);

            DrawPoseMetrics(weapon);

            // Repaint lien tuc de field khong bi lag khi keo bang Scene tool.
            Repaint();
        }

        // Live pose metrics (Play Mode): reach ratios, hand-bone->grip errors, muzzle-vs-aim angle,
        // plus quick IK weight toggles. Measures REAL bones on the REAL Avatar - not targets.
        private void DrawPoseMetrics(Weapon weapon)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Pose Metrics (live)", EditorStyles.boldLabel);

            var animator = weapon.GetComponentInParent<Animator>();
            var grips = weapon.CurrentGrips;
            if (animator == null || !animator.isHuman || grips == null)
            {
                EditorGUILayout.HelpBox("Can Humanoid Animator + equipped weapon.", MessageType.None);
                return;
            }

            var rU = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var rL = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var rH = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var lU = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var lL = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var lH = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            float rMax = Vector3.Distance(rU.position, rL.position) + Vector3.Distance(rL.position, rH.position);
            float lMax = Vector3.Distance(lU.position, lL.position) + Vector3.Distance(lL.position, lH.position);

            if (grips.RightHandGrip != null)
            {
                float ratio = Vector3.Distance(rU.position, grips.RightHandGrip.position) / Mathf.Max(0.001f, rMax);
                float posErr = Vector3.Distance(rH.position, grips.RightHandGrip.position);
                float rotErr = Quaternion.Angle(rH.rotation, grips.RightHandGrip.rotation);
                EditorGUILayout.LabelField($"R arm: len={rMax:F3}  reach={ratio:F2}  handErr={posErr * 1000f:F1}mm  rotErr={rotErr:F1}°");
            }
            if (grips.LeftHandGrip != null && weapon.Current != null && weapon.Current.twoHanded)
            {
                float ratio = Vector3.Distance(lU.position, grips.LeftHandGrip.position) / Mathf.Max(0.001f, lMax);
                float posErr = Vector3.Distance(lH.position, grips.LeftHandGrip.position);
                float rotErr = Quaternion.Angle(lH.rotation, grips.LeftHandGrip.rotation);
                EditorGUILayout.LabelField($"L arm: len={lMax:F3}  reach={ratio:F2}  handErr={posErr * 1000f:F1}mm  rotErr={rotErr:F1}°");
            }
            if (grips.MuzzlePoint != null)
            {
                float muz = Vector3.Angle(grips.MuzzlePoint.forward, weapon.transform.forward);
                EditorGUILayout.LabelField($"Muzzle vs root-forward: {muz:F1}°");
            }

            var ik = weapon.GetComponent<WeaponIKController>();
            if (ik != null)
            {
                var so = new SerializedObject(ik);
                var rIK = so.FindProperty("rightHandIK").objectReferenceValue as UnityEngine.Animations.Rigging.TwoBoneIKConstraint;
                var lIK = so.FindProperty("leftHandIK").objectReferenceValue as UnityEngine.Animations.Rigging.TwoBoneIKConstraint;
                var aim = so.FindProperty("aimConstraint").objectReferenceValue as UnityEngine.Animations.Rigging.MultiAimConstraint;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (rIK != null && GUILayout.Button($"R IK {(rIK.weight > 0 ? "ON" : "off")}")) rIK.weight = rIK.weight > 0 ? 0f : 1f;
                    if (lIK != null && GUILayout.Button($"L IK {(lIK.weight > 0 ? "ON" : "off")}")) lIK.weight = lIK.weight > 0 ? 0f : 1f;
                    if (aim != null && GUILayout.Button($"ChestAim {(aim.weight > 0 ? "ON" : "off")}")) aim.weight = aim.weight > 0 ? 0f : 1f;
                }
            }
        }
    }
}
