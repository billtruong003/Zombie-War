using System;
using System.Collections.Generic;
using System.Reflection;
using BillInspector;

namespace BillInspector.Editor
{
    /// <summary>
    /// Centralized reflection cache to avoid repeated GetField/GetMethod calls.
    /// Not thread-safe (editor-only, main thread). Auto-clears on domain reload.
    /// </summary>
    public static class BillReflectionCache
    {
        private static readonly Dictionary<(Type, string), FieldInfo> s_fields = new();
        private static readonly Dictionary<(Type, string), PropertyInfo> s_properties = new();
        private static readonly Dictionary<(Type, string), MethodInfo> s_methods = new();
        private static readonly Dictionary<Type, FieldInfo[]> s_allFields = new();

        private const BindingFlags FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static FieldInfo GetField(Type type, string name)
        {
            var key = (type, name);
            if (!s_fields.TryGetValue(key, out var field))
            {
                field = type.GetField(name, FLAGS);
                s_fields[key] = field;
            }
            return field;
        }

        public static PropertyInfo GetProperty(Type type, string name)
        {
            var key = (type, name);
            if (!s_properties.TryGetValue(key, out var prop))
            {
                prop = type.GetProperty(name, FLAGS);
                s_properties[key] = prop;
            }
            return prop;
        }

        public static MethodInfo GetMethod(Type type, string name)
        {
            var key = (type, name);
            if (!s_methods.TryGetValue(key, out var method))
            {
                method = type.GetMethod(name, FLAGS);
                s_methods[key] = method;
            }
            return method;
        }

        public static FieldInfo[] GetAllFields(Type type)
        {
            if (!s_allFields.TryGetValue(type, out var fields))
            {
                var list = new List<FieldInfo>();
                var current = type;
                while (current != null && current != typeof(object))
                {
                    list.AddRange(current.GetFields(FLAGS | BindingFlags.DeclaredOnly));
                    current = current.BaseType;
                }
                fields = list.ToArray();
                s_allFields[type] = fields;
            }
            return fields;
        }

        public static object GetMemberValue(object target, Type type, string name)
        {
            var field = GetField(type, name);
            if (field != null) return field.GetValue(target);
            var prop = GetProperty(type, name);
            if (prop != null) return prop.GetValue(target);
            return null;
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            Clear();
            // Also clear runtime caches that can't use [InitializeOnLoadMethod]
            BillSerializer.ClearCache();
        }

        public static void Clear()
        {
            s_fields.Clear();
            s_properties.Clear();
            s_methods.Clear();
            s_allFields.Clear();
        }
    }
}
