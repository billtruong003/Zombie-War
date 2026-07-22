using UnityEditor;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// Gắn CostumePreviewIdleDirector lên preview character (MenuCharacterPreviewStage) với clip
    /// idle thật của Malbers — base + variations. Idempotent. KHÔNG đụng PlayerAnimator gameplay
    /// (director dùng PlayableGraph riêng).
    /// </summary>
    public static class CostumePreviewIdleInstaller
    {
        const string StagePath = "Assets/_Project/UI/Prefabs/Preview/MenuCharacterPreviewStage.prefab";

        [MenuItem("ZombieWar/UI/Authoring/Ensure Preview Idle Director")]
        public static void Ensure()
        {
            var root = PrefabUtility.LoadPrefabContents(StagePath);
            try
            {
                var animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null) { Debug.LogError("[PreviewIdle] Không thấy Animator trong preview stage."); return; }

                var dir = animator.GetComponent<CostumePreviewIdleDirector>();
                bool created = dir == null;
                if (created) dir = animator.gameObject.AddComponent<CostumePreviewIdleDirector>();

                var baseIdle = Load("Assets/ThirdParty/MalbersHumanAnims/Locomotion/Idle.anim");
                var v1 = Load("Assets/ThirdParty/MalbersHumanAnims/Idle/Idle_Look.fbx");
                var v2 = Load("Assets/ThirdParty/MalbersHumanAnims/Idle/Idle Yawn.fbx");
                var v3 = Load("Assets/ThirdParty/MalbersHumanAnims/Idle/Idle v2.fbx");
                if (baseIdle == null) { Debug.LogError("[PreviewIdle] Thiếu base Idle clip."); return; }

                var so = new SerializedObject(dir);
                so.FindProperty("baseIdle").objectReferenceValue = baseIdle;
                var vars = so.FindProperty("variations");
                var valid = new System.Collections.Generic.List<AnimationClip>();
                foreach (var c in new[] { v1, v2, v3 }) if (c != null) valid.Add(c);
                vars.arraySize = valid.Count;
                for (int i = 0; i < valid.Count; i++) vars.GetArrayElementAtIndex(i).objectReferenceValue = valid[i];
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, StagePath);
                Debug.Log($"[PreviewIdle] {(created ? "Đã tạo" : "Đã reuse")} director: base={baseIdle.name}, {valid.Count} variation.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static AnimationClip Load(string path)
        {
            var direct = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (direct != null) return direct;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) return c;
            return null;
        }
    }
}
