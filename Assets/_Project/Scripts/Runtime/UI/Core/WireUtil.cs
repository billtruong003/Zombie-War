#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// G12.8 (owner-approved 04/10): screens used to find their widgets by path at run time, so a
    /// renamed node silently blanked a label. Each screen now holds serialized references and wires
    /// them once from those paths (EditorWire, menu HordeCall/UI/Wire Serialized Refs); the test
    /// SerializedRefsTests fails when a prefab has a path or a reference missing.
    /// </summary>
    public static class WireUtil
    {
        /// The component at <paramref name="path"/> under <paramref name="root"/> ("" = root itself);
        /// a miss is added to <paramref name="missing"/>.
        public static T Find<T>(Transform root, string path, List<string> missing) where T : Component
        {
            var t = root == null ? null : string.IsNullOrEmpty(path) ? root : root.Find(path);
            var c = t != null ? t.GetComponent<T>() : null;
            if (c == null) missing.Add($"{(root != null ? root.name : "?")}/{path} ({typeof(T).Name})");
            return c;
        }

        public static GameObject Node(Transform root, string path, List<string> missing)
        {
            var t = root != null ? root.Find(path) : null;
            if (t == null) missing.Add($"{(root != null ? root.name : "?")}/{path}");
            return t != null ? t.gameObject : null;
        }

        /// The names of <paramref name="fields"/> whose value is null (Unity null included).
        public static List<string> Nulls(params (string name, Object value)[] fields)
        {
            var list = new List<string>();
            foreach (var (name, value) in fields) if (value == null) list.Add(name);
            return list;
        }
    }
}
#endif
