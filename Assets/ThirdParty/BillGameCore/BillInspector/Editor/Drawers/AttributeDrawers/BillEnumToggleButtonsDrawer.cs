using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor.Drawers
{
    [BillCustomDrawer(typeof(BillEnumToggleButtonsAttribute))]
    public class BillEnumToggleButtonsDrawer : BillAttributeDrawer<BillEnumToggleButtonsAttribute>
    {
        public override VisualElement CreatePropertyGUI(BillProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.marginTop = 2;
            container.style.marginBottom = 2;

            var label = new Label(property.DisplayName);
            label.style.width = 120;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            container.Add(label);

            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.flexGrow = 1;

            var enumType = property.FieldType;
            var names = Enum.GetNames(enumType);
            var values = Enum.GetValues(enumType);

            for (int i = 0; i < names.Length; i++)
            {
                int capturedIndex = i;
                var btn = new Button(() =>
                {
                    property.SerializedProperty.serializedObject.Update();
                    property.SerializedProperty.enumValueIndex = capturedIndex;
                    property.SerializedProperty.serializedObject.ApplyModifiedProperties();
                });
                btn.text = ObjectNames.NicifyVariableName(names[i]);
                btn.style.flexGrow = 1;
                btn.style.marginLeft = i == 0 ? 0 : -1;
                buttonsContainer.Add(btn);
            }

            container.Add(buttonsContainer);

            // Highlight active button
            container.schedule.Execute(() =>
            {
                if (property.SerializedProperty == null) return;
                property.SerializedProperty.serializedObject.Update();
                int current = property.SerializedProperty.enumValueIndex;
                for (int i = 0; i < buttonsContainer.childCount; i++)
                {
                    var btn = buttonsContainer[i] as Button;
                    if (btn == null) continue;
                    btn.style.backgroundColor = i == current
                        ? new Color(0.24f, 0.49f, 0.91f, 0.6f)
                        : new Color(0.3f, 0.3f, 0.3f, 0.3f);
                }
            }).Every(100);

            return container;
        }

        public override void OnGUI(Rect rect, BillProperty property)
        {
            var sp = property.SerializedProperty;
            var enumType = property.FieldType;
            var names = Enum.GetNames(enumType);
            var labelRect = new Rect(rect.x, rect.y, 120, rect.height);
            EditorGUI.LabelField(labelRect, property.DisplayName);

            float btnWidth = (rect.width - 124) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                var btnRect = new Rect(rect.x + 124 + i * btnWidth, rect.y, btnWidth, rect.height);
                bool isActive = sp.enumValueIndex == i;
                var style = isActive ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                if (GUI.Toggle(btnRect, isActive, ObjectNames.NicifyVariableName(names[i]), style))
                    sp.enumValueIndex = i;
            }
        }
    }
}
