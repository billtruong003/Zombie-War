using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BillInspector.Editor
{
    /// <summary>
    /// Utility methods for BillInspector editor operations.
    /// </summary>
    public static class BillEditorUtility
    {
        /// <summary>Mark object dirty for serialization.</summary>
        public static void MarkDirty(UnityEngine.Object target)
        {
            if (target == null) return;
            EditorUtility.SetDirty(target);
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        /// <summary>Get a nicified display name from a field name.</summary>
        public static string NicifyName(string name)
        {
            return ObjectNames.NicifyVariableName(name);
        }

        /// <summary>Get the underlying element type of a collection.</summary>
        public static Type GetCollectionElementType(Type collectionType)
        {
            if (collectionType.IsArray)
                return collectionType.GetElementType();
            if (collectionType.IsGenericType)
                return collectionType.GetGenericArguments()[0];
            return null;
        }

        /// <summary>Check if a type is a Unity-serializable primitive.</summary>
        public static bool IsUnityPrimitive(Type type)
        {
            return type.IsPrimitive || type == typeof(string) || type.IsEnum
                || type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4)
                || type == typeof(Color) || type == typeof(Rect) || type == typeof(Bounds)
                || type == typeof(Quaternion) || type == typeof(AnimationCurve)
                || type == typeof(Gradient) || type == typeof(LayerMask);
        }
    }
}
