using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// G12.8 (owner-approved 04/10): wires the serialized references the screens used to find by path
    /// at run time (RunOverlays views, RunEndV2, FtueCoach, Home, Shop, Arsenal, AvatarPicker), in the
    /// prefabs themselves. Only references are written; no layout changes. Re-run after renaming or
    /// rebuilding a prefab; SerializedRefsTests fails until then.
    /// </summary>
    public static class SerializedRefsWiring
    {
        public const string Hud = "Assets/_Project/UI/Prefabs/Screens/UI_Hud.prefab";
        public const string Home = "Assets/_Project/UI/Prefabs/V2/UI_V2_Home.prefab";
        public const string Shop = "Assets/_Project/UI/Prefabs/V2/UI_V2_Shop.prefab";
        public const string Arsenal = "Assets/_Project/UI/Prefabs/V2/UI_V2_Arsenal.prefab";
        public const string Profile = "Assets/_Project/UI/Prefabs/V2/UI_V2_Profile.prefab";

        public static readonly string[] Prefabs = { Hud, Home, Shop, Arsenal, Profile };

        [MenuItem("HordeCall/UI/Wire Serialized Refs (G12.8)")]
        public static void WireAll()
        {
            var report = new List<string>();
            foreach (var path in Prefabs) report.AddRange(Wire(path));
            if (report.Count == 0) Debug.Log("[SerializedRefs] every prefab wired.");
            else Debug.LogError("[SerializedRefs] missing:\n" + string.Join("\n", report));
        }

        /// Wires one prefab and saves it; returns what could not be found.
        public static List<string> Wire(string path)
        {
            var missing = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var c in root.GetComponentsInChildren<RunOverlays>(true)) missing.AddRange(c.EditorWire());
                foreach (var c in root.GetComponentsInChildren<RunEndV2>(true)) missing.AddRange(c.EditorWire());
                foreach (var c in root.GetComponentsInChildren<FtueCoach>(true)) missing.AddRange(c.EditorWire());
                foreach (var c in root.GetComponentsInChildren<HomeScreen>(true)) missing.AddRange(c.EditorWire());
                foreach (var c in root.GetComponentsInChildren<ShopScreenV2>(true)) missing.AddRange(c.EditorWire());
                foreach (var c in root.GetComponentsInChildren<ArsenalScreen>(true)) missing.AddRange(c.EditorWire());
                foreach (var c in root.GetComponentsInChildren<AvatarPicker>(true)) missing.AddRange(c.EditorWire());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            for (int i = 0; i < missing.Count; i++) missing[i] = System.IO.Path.GetFileNameWithoutExtension(path) + ": " + missing[i];
            return missing;
        }

        /// Null references left in a prefab, without changing it (for the test).
        public static List<string> Unwired(string path)
        {
            var nulls = new List<string>();
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) { nulls.Add(path + " missing"); return nulls; }
            void Add(Component c, List<string> list) { foreach (var n in list) nulls.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)}/{c.GetType().Name}.{n}"); }
            foreach (var c in root.GetComponentsInChildren<RunOverlays>(true)) Add(c, c.EditorUnwired());
            foreach (var c in root.GetComponentsInChildren<RunEndV2>(true)) Add(c, c.EditorUnwired());
            foreach (var c in root.GetComponentsInChildren<FtueCoach>(true)) Add(c, c.EditorUnwired());
            foreach (var c in root.GetComponentsInChildren<HomeScreen>(true)) Add(c, c.EditorUnwired());
            foreach (var c in root.GetComponentsInChildren<ShopScreenV2>(true)) Add(c, c.EditorUnwired());
            foreach (var c in root.GetComponentsInChildren<ArsenalScreen>(true)) Add(c, c.EditorUnwired());
            foreach (var c in root.GetComponentsInChildren<AvatarPicker>(true)) Add(c, c.EditorUnwired());
            return nulls;
        }
    }
}
