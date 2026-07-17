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
                _synced = true;
            }

            EditorGUILayout.HelpBox($"Weapon: {data.weaponName}", MessageType.None);

            EditorGUI.BeginChangeCheck();
            _pos = EditorGUILayout.Vector3Field("Grip Local Position", _pos);
            _euler = EditorGUILayout.Vector3Field("Grip Local Euler", _euler);
            if (EditorGUI.EndChangeCheck())
                weapon.EditorApplyGrip(_pos, _euler); // live preview

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pull tu Instance"))
                {
                    // User da keo sung bang Move/Rotate tool -> hut gia tri hien tai vao field.
                    _pos = inst.localPosition;
                    _euler = inst.localRotation.eulerAngles;
                }

                if (GUILayout.Button("CAPTURE -> WeaponData", GUILayout.Height(24)))
                {
                    Undo.RecordObject(data, "Capture Weapon Grip");
                    data.gripLocalPosition = _pos;
                    data.gripLocalEuler = _euler;
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[GripTuner] Saved to {data.name}: pos={_pos} euler={_euler}", data);
                }
            }

            EditorGUILayout.HelpBox(
                "Meo: mo Hierarchy -> chon instance sung (con cua RecoilPivot) -> dung Move/Rotate tool keo " +
                "cho vua tay -> bam 'Pull tu Instance' -> 'CAPTURE'. Gia tri luu thang vao asset.",
                MessageType.None);

            // Repaint lien tuc de field khong bi lag khi keo bang Scene tool.
            Repaint();
        }
    }
}
