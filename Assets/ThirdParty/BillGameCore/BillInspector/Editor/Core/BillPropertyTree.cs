using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using BillInspector;

namespace BillInspector.Editor
{
    /// <summary>
    /// Scans a type via reflection and builds a tree of BillProperty objects.
    /// Caches results per type for performance.
    /// </summary>
    public class BillPropertyTree
    {
        private static readonly Dictionary<Type, List<MemberRecord>> s_typeCache = new();

        public SerializedObject SerializedObject { get; }
        public List<BillProperty> Properties { get; } = new();
        public List<MethodRecord> ButtonMethods { get; } = new();

        // Group structure: groupPath -> list of properties
        public Dictionary<string, List<BillProperty>> Groups { get; } = new();

        public BillPropertyTree(SerializedObject serializedObject)
        {
            SerializedObject = serializedObject;
            Build();
        }

        private void Build()
        {
            var targetType = SerializedObject.targetObject.GetType();
            var records = GetOrCacheMemberRecords(targetType);

            foreach (var record in records)
            {
                if (record.IsMethod)
                {
                    var buttonAttr = record.Method.GetCustomAttribute<BillButtonAttribute>();
                    if (buttonAttr != null)
                    {
                        ButtonMethods.Add(new MethodRecord
                        {
                            Method = record.Method,
                            ButtonAttribute = buttonAttr,
                            ButtonGroupAttribute = record.Method.GetCustomAttribute<BillButtonGroupAttribute>(),
                            ShowResultAs = record.Method.GetCustomAttribute<BillShowResultAsAttribute>(),
                            GUIColor = record.Method.GetCustomAttribute<BillGUIColorAttribute>(),
                            AllAttributes = record.Method.GetCustomAttributes(typeof(BillAttribute), true)
                                .Cast<BillAttribute>().ToList()
                        });
                    }
                    continue;
                }

                var sp = SerializedObject.FindProperty(record.Field.Name);
                if (sp == null) continue;

                var prop = new BillProperty(sp, record.Field);
                Properties.Add(prop);

                // Register in groups
                foreach (var groupAttr in prop.GroupAttributes)
                {
                    if (!Groups.ContainsKey(groupAttr.GroupPath))
                        Groups[groupAttr.GroupPath] = new List<BillProperty>();
                    Groups[groupAttr.GroupPath].Add(prop);
                }
            }

            // Sort by Order attribute
            Properties.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        private static List<MemberRecord> GetOrCacheMemberRecords(Type type)
        {
            if (s_typeCache.TryGetValue(type, out var cached))
                return cached;

            var records = new List<MemberRecord>();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Walk up the inheritance chain
            var currentType = type;
            while (currentType != null && currentType != typeof(MonoBehaviour)
                   && currentType != typeof(ScriptableObject) && currentType != typeof(object))
            {
                // Fields
                foreach (var field in currentType.GetFields(flags | BindingFlags.DeclaredOnly))
                {
                    // Include if: has SerializeField, is public, or has BillShowInInspector
                    bool isSerializable = field.IsPublic ||
                        field.GetCustomAttribute<UnityEngine.SerializeField>() != null;
                    bool hasBillAttr = field.GetCustomAttribute<BillShowInInspectorAttribute>() != null;

                    if (isSerializable || hasBillAttr)
                    {
                        records.Add(new MemberRecord { Field = field });
                    }
                }

                // Methods (for buttons)
                foreach (var method in currentType.GetMethods(flags | BindingFlags.DeclaredOnly))
                {
                    if (method.GetCustomAttribute<BillButtonAttribute>() != null)
                    {
                        records.Add(new MemberRecord { Method = method, IsMethod = true });
                    }
                }

                currentType = currentType.BaseType;
            }

            s_typeCache[type] = records;
            return records;
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void OnDomainReload() => ClearCache();

        /// <summary>
        /// Clears the reflection cache. Call after domain reload or when types change.
        /// </summary>
        public static void ClearCache() => s_typeCache.Clear();

        // Internal records
        private struct MemberRecord
        {
            public FieldInfo Field;
            public MethodInfo Method;
            public bool IsMethod;
        }

        public class MethodRecord
        {
            public MethodInfo Method;
            public BillButtonAttribute ButtonAttribute;
            public BillButtonGroupAttribute ButtonGroupAttribute;
            public BillShowResultAsAttribute ShowResultAs;
            public BillGUIColorAttribute GUIColor;
            public List<BillAttribute> AllAttributes;
        }
    }
}
