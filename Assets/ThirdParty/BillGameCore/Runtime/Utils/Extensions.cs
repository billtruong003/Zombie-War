using System.Collections.Generic;
using UnityEngine;

namespace BillGameCore
{
    public static class BillExtensions
    {
        // Transform
        public static void DestroyAllChildren(this Transform t) { for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject); }
        public static void ResetLocal(this Transform t) { t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one; }
        public static void SetX(this Transform t, float x) { var p = t.position; p.x = x; t.position = p; }
        public static void SetY(this Transform t, float y) { var p = t.position; p.y = y; t.position = p; }
        public static void SetZ(this Transform t, float z) { var p = t.position; p.z = z; t.position = p; }

        // GameObject
        public static T GetOrAdd<T>(this GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();
        public static bool Has<T>(this GameObject go) where T : Component => go.GetComponent<T>() != null;
        public static void ReturnToPool(this GameObject go) => Bill.Pool?.Return(go);
        public static void ReturnToPool(this GameObject go, float delay) => Bill.Pool?.Return(go, delay);
        public static void ReturnToPool(this Component c) => c.gameObject.ReturnToPool();

        // Collections
        public static T Random<T>(this IList<T> list) => list == null || list.Count == 0 ? default : list[UnityEngine.Random.Range(0, list.Count)];
        public static void Shuffle<T>(this IList<T> list) { for (int i = list.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); } }
        public static bool IsNullOrEmpty<T>(this ICollection<T> c) => c == null || c.Count == 0;
        public static T SafeGet<T>(this IList<T> list, int i, T fb = default) => i >= 0 && i < list.Count ? list[i] : fb;

        // Vector
        public static Vector3 Flat(this Vector3 v) => new(v.x, 0f, v.z);
        public static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);
        public static float FlatDistance(this Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
