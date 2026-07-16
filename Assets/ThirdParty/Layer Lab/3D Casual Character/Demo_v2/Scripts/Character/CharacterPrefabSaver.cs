using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.IO;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class CharacterPrefabSaver : MonoBehaviour
    {
#if UNITY_EDITOR
        public GameObject characterRoot; // 캐릭터 루트 오브젝트
        public string saveFolderPath = "Assets/SavedPrefabs";

        public void SaveAsPrefab()
        {
            if (!Directory.Exists(saveFolderPath))
                Directory.CreateDirectory(saveFolderPath);

            var prefabPath = saveFolderPath + "/Character_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".prefab";

            // 복사본 생성 후 비활성화된 아이템 제거
            var characterCopy = Instantiate(characterRoot);
            characterCopy.transform.rotation = Quaternion.identity;
            
            var objects = characterCopy.transform.GetComponentsInChildren<Transform>(true);

            for (int i = objects.Length - 1; i >= 0; i--)
            {
                if (!objects[i].gameObject.activeSelf)
                {
                    DestroyImmediate(objects[i].gameObject);
                }
            }

            // 특정 스크립트 제거
            var scriptsToRemove = characterCopy.GetComponentsInChildren<CharacterPart>(true);
            foreach (var script in scriptsToRemove)
            {
                if (script.transform.childCount <= 0)
                {
                    DestroyImmediate(script.gameObject);
                }
                else
                {
                    DestroyImmediate(script);   
                }
                
                
            }



            // 프리팹 저장
            PrefabUtility.SaveAsPrefabAsset(characterCopy, prefabPath);
            DestroyImmediate(characterCopy);

            // 저장된 프리팹을 Project 창에서 선택 및 강조 표시
            AssetDatabase.Refresh();
            Object savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);

            Debug.Log("Prefab Saved: " + prefabPath);
        }
#endif
    }
}