using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using BillInspector;

namespace BillInspector.Editor
{
    /// <summary>
    /// Wraps a SerializedProperty with cached reflection info and attribute data.
    /// This is the central type that drawers operate on.
    /// </summary>
    public class BillProperty
    {
        public SerializedProperty SerializedProperty { get; }
        public SerializedObject SerializedObject => SerializedProperty?.serializedObject;
        public FieldInfo FieldInfo { get; }
        public string Name { get; }
        public string DisplayName { get; private set; }
        public int Order { get; }
        public Type FieldType { get; }

        // Cached attributes by category
        public List<BillMetaAttribute> MetaAttributes { get; } = new();
        public List<BillGroupAttribute> GroupAttributes { get; } = new();
        public BillDrawerAttribute DrawerAttribute { get; set; }
        public List<BillAttribute> AllAttributes { get; } = new();

        // Events
        public event Action<object> OnValueChanged;

        public BillProperty(SerializedProperty serializedProperty, FieldInfo fieldInfo)
        {
            SerializedProperty = serializedProperty;
            FieldInfo = fieldInfo;
            Name = fieldInfo.Name;
            DisplayName = ObjectNames.NicifyVariableName(fieldInfo.Name);
            FieldType = fieldInfo.FieldType;

            // Cache all BillInspector attributes
            var attributes = fieldInfo.GetCustomAttributes(typeof(BillAttribute), true);
            foreach (var attr in attributes)
            {
                var billAttr = (BillAttribute)attr;
                AllAttributes.Add(billAttr);

                switch (billAttr)
                {
                    case BillDrawerAttribute drawer:
                        DrawerAttribute = drawer;
                        break;
                    case BillGroupAttribute group:
                        GroupAttributes.Add(group);
                        break;
                    case BillMetaAttribute meta:
                        MetaAttributes.Add(meta);
                        break;
                }
            }

            // Apply LabelText if present
            var labelAttr = fieldInfo.GetCustomAttribute<BillLabelTextAttribute>();
            if (labelAttr != null)
                DisplayName = labelAttr.Text;

            // Apply PropertyOrder if present
            var orderAttr = fieldInfo.GetCustomAttribute<BillPropertyOrderAttribute>();
            Order = orderAttr?.PropertyOrder ?? 0;
        }

        /// <summary>
        /// Gets the current value of the underlying field.
        /// </summary>
        public T GetValue<T>()
        {
            if (SerializedObject?.targetObject == null) return default;
            return (T)FieldInfo.GetValue(SerializedObject.targetObject);
        }

        /// <summary>
        /// Sets the value with proper Undo support and fires OnValueChanged.
        /// </summary>
        public void SetValue(object value)
        {
            if (SerializedObject?.targetObject == null) return;
            Undo.RecordObject(SerializedObject.targetObject, $"Set {Name}");
            FieldInfo.SetValue(SerializedObject.targetObject, value);
            EditorUtility.SetDirty(SerializedObject.targetObject);
            OnValueChanged?.Invoke(value);
        }

        /// <summary>
        /// Gets attribute of a specific type, or null if not present.
        /// </summary>
        public T GetAttribute<T>() where T : BillAttribute
        {
            foreach (var attr in AllAttributes)
            {
                if (attr is T typed) return typed;
            }
            return null;
        }

        public bool HasAttribute<T>() where T : BillAttribute
        {
            return GetAttribute<T>() != null;
        }
    }
}
