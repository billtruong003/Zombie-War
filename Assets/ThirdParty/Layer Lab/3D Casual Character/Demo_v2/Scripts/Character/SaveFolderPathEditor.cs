using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Layer_lab._3D_Casual_Character.Demo2
{
    #if UNITY_EDITOR
    [CustomEditor(typeof(CharacterPrefabSaver))]
    public class SaveFolderPathEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); // 기본 UI 그리기

            var script = (CharacterPrefabSaver)target;

            if (GUILayout.Button("폴더 선택"))
            {
                var selectedPath = EditorUtility.OpenFolderPanel("프리팹 저장 폴더 선택", script.saveFolderPath, "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    var relativePath = "Assets" + selectedPath.Replace(Application.dataPath, "");
                    script.saveFolderPath = $"{relativePath}/";
                    EditorUtility.SetDirty(script); 
                }
            }
        }
    }
    #endif
}
