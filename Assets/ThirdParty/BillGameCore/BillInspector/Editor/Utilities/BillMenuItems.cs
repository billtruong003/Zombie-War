using UnityEditor;
using UnityEngine;

namespace BillInspector.Editor
{
    /// <summary>
    /// Menu items for BillInspector tools.
    /// </summary>
    public static class BillMenuItems
    {
        [MenuItem("Tools/BillInspector/Validate Active Scene")]
        private static void ValidateScene()
        {
            var results = BillValidator.ValidateScene();
            BillValidator.LogResults(results);
        }

        [MenuItem("Tools/BillInspector/Validate Project Assets")]
        private static void ValidateAssets()
        {
            var results = BillValidator.ValidateProjectAssets();
            BillValidator.LogResults(results);
        }

        [MenuItem("Tools/BillInspector/Clear Caches")]
        private static void ClearCaches()
        {
            BillPropertyTree.ClearCache();
            BillDrawerLocator.ClearCache();
            ExpressionCompiler.ClearCache();
            BillReflectionCache.Clear();
            Debug.Log("[BillInspector] All caches cleared.");
        }

        [MenuItem("Tools/BillInspector/About")]
        private static void About()
        {
            EditorUtility.DisplayDialog("BillInspector",
                "BillInspector v0.4.0\n\n" +
                "Attribute-based inspector, serialization,\n" +
                "shader editor, and validation framework\n" +
                "for Unity 6.\n\n" +
                "Phase 1-4 Complete.",
                "OK");
        }
    }
}
