using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor.Drawers
{
    [BillCustomDrawer(typeof(BillDropdownAttribute))]
    public class BillDropdownDrawer : BillAttributeDrawer<BillDropdownAttribute>
    {
        public override VisualElement CreatePropertyGUI(BillProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;

            var label = new Label(property.DisplayName);
            label.style.width = 120;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            container.Add(label);

            var popup = new PopupField<string>(new List<string> { "Loading..." }, 0);
            popup.style.flexGrow = 1;

            container.schedule.Execute(() =>
            {
                var items = ResolveValues(property);
                if (items == null || items.Count == 0) return;

                popup.choices = items;
                var currentVal = property.SerializedProperty.propertyType == SerializedPropertyType.String
                    ? property.SerializedProperty.stringValue
                    : property.SerializedProperty.propertyType == SerializedPropertyType.Integer
                        ? property.SerializedProperty.intValue.ToString()
                        : "";

                int idx = items.IndexOf(currentVal);
                if (idx >= 0) popup.index = idx;
            }).Every(500);

            popup.RegisterValueChangedCallback(evt =>
            {
                if (property.SerializedProperty.propertyType == SerializedPropertyType.String)
                    property.SerializedProperty.stringValue = evt.newValue;
                else if (property.SerializedProperty.propertyType == SerializedPropertyType.Integer
                         && int.TryParse(evt.newValue, out int v))
                    property.SerializedProperty.intValue = v;
                property.SerializedProperty.serializedObject.ApplyModifiedProperties();
            });

            container.Add(popup);
            return container;
        }

        public override void OnGUI(Rect rect, BillProperty property)
        {
            var items = ResolveValues(property);
            if (items == null || items.Count == 0)
            {
                EditorGUI.LabelField(rect, property.DisplayName, "No values");
                return;
            }

            var currentVal = property.SerializedProperty.propertyType == SerializedPropertyType.String
                ? property.SerializedProperty.stringValue : "";
            int current = Mathf.Max(0, items.IndexOf(currentVal));
            int selected = EditorGUI.Popup(rect, property.DisplayName, current, items.ToArray());
            if (selected >= 0 && selected < items.Count)
                property.SerializedProperty.stringValue = items[selected];
        }

        private List<string> ResolveValues(BillProperty property)
        {
            var target = property.SerializedObject.targetObject;
            var type = target.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Try method
            var method = type.GetMethod(Attribute.ValuesSource, flags);
            if (method != null)
            {
                var result = method.Invoke(target, null);
                if (result is IEnumerable enumerable)
                    return enumerable.Cast<object>().Select(x => x?.ToString() ?? "").ToList();
            }

            // Try field/property
            var field = type.GetField(Attribute.ValuesSource, flags);
            if (field?.GetValue(target) is IEnumerable fe)
                return fe.Cast<object>().Select(x => x?.ToString() ?? "").ToList();

            var prop = type.GetProperty(Attribute.ValuesSource, flags);
            if (prop?.GetValue(target) is IEnumerable pe)
                return pe.Cast<object>().Select(x => x?.ToString() ?? "").ToList();

            return new List<string>();
        }
    }
}
