using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor.Drawers
{
    /// <summary>
    /// Custom drawer for Dictionary fields on BillSerializedMonoBehaviour.
    /// Draws as a list of key-value pairs with add/remove controls.
    /// </summary>
    [BillCustomDrawer(typeof(BillDictionaryDrawerAttribute))]
    public class BillDictionaryTypeDrawer : BillAttributeDrawer<BillDictionaryDrawerAttribute>
    {
        public override VisualElement CreatePropertyGUI(BillProperty property)
        {
            var container = new VisualElement();
            container.style.marginTop = 4;
            container.style.marginBottom = 4;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 4;

            var foldout = new Foldout();
            foldout.text = property.DisplayName;
            foldout.value = true;

            var addBtn = new Button(() => { /* Add entry logic via reflection */ });
            addBtn.text = "+";
            addBtn.style.width = 24;
            addBtn.style.height = 20;

            var listContainer = new VisualElement();

            // Rebuild entries from the actual dictionary
            Action rebuild = () =>
            {
                listContainer.Clear();
                var dict = GetDictionary(property);
                if (dict == null) return;

                int idx = 0;
                foreach (DictionaryEntry entry in dict)
                {
                    var entryRow = new VisualElement();
                    entryRow.style.flexDirection = FlexDirection.Row;
                    entryRow.style.marginBottom = 2;
                    entryRow.style.paddingLeft = 8;

                    var keyLabel = new Label(entry.Key?.ToString() ?? "null");
                    keyLabel.style.width = 120;
                    keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    keyLabel.style.fontSize = 11;
                    entryRow.Add(keyLabel);

                    var valLabel = new Label(entry.Value?.ToString() ?? "null");
                    valLabel.style.flexGrow = 1;
                    valLabel.style.fontSize = 11;
                    entryRow.Add(valLabel);

                    var removeBtn = new Button(() =>
                    {
                        dict.Remove(entry.Key);
                        EditorUtility.SetDirty(property.SerializedObject.targetObject);
                    });
                    removeBtn.text = "×";
                    removeBtn.style.width = 20;
                    removeBtn.style.height = 18;
                    removeBtn.style.fontSize = 12;
                    entryRow.Add(removeBtn);

                    listContainer.Add(entryRow);
                    idx++;
                }

                // Count label
                foldout.text = $"{property.DisplayName} ({idx})";
            };

            foldout.Add(listContainer);
            container.Add(foldout);

            container.schedule.Execute(rebuild).Every(500);

            return container;
        }

        public override void OnGUI(Rect rect, BillProperty property)
        {
            EditorGUI.LabelField(rect, property.DisplayName, "[Dictionary — use UI Toolkit]");
        }

        private IDictionary GetDictionary(BillProperty property)
        {
            var target = property.SerializedObject.targetObject;
            return property.FieldInfo.GetValue(target) as IDictionary;
        }
    }
}
