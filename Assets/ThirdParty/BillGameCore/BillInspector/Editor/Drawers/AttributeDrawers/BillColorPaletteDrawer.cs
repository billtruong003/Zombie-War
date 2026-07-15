using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor.Drawers
{
    [BillCustomDrawer(typeof(BillColorPaletteAttribute))]
    public class BillColorPaletteDrawer : BillAttributeDrawer<BillColorPaletteAttribute>
    {
        private static readonly Color[] s_defaultPalette =
        {
            Color.red, new Color(1f, 0.5f, 0f), Color.yellow, Color.green,
            Color.cyan, Color.blue, new Color(0.5f, 0f, 1f), Color.magenta,
            Color.white, Color.gray, Color.black,
            new Color(0.5f, 0.25f, 0f), new Color(1f, 0.75f, 0.8f),
            new Color(0f, 0.5f, 0.5f), new Color(0.5f, 1f, 0.5f), new Color(1f, 0.85f, 0.7f)
        };

        public override VisualElement CreatePropertyGUI(BillProperty property)
        {
            var container = new VisualElement();
            container.style.marginTop = 2;
            container.style.marginBottom = 2;

            var label = new Label(property.DisplayName);
            label.style.marginBottom = 4;
            container.Add(label);

            // Color swatches grid
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;

            foreach (var color in s_defaultPalette)
            {
                var swatch = new VisualElement();
                swatch.style.width = 24;
                swatch.style.height = 24;
                swatch.style.marginRight = 3;
                swatch.style.marginBottom = 3;
                swatch.style.backgroundColor = color;
                swatch.style.borderTopLeftRadius = 3;
                swatch.style.borderTopRightRadius = 3;
                swatch.style.borderBottomLeftRadius = 3;
                swatch.style.borderBottomRightRadius = 3;
                swatch.style.borderTopWidth = 1;
                swatch.style.borderBottomWidth = 1;
                swatch.style.borderLeftWidth = 1;
                swatch.style.borderRightWidth = 1;
                swatch.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                swatch.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                swatch.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                swatch.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

                var captured = color;
                swatch.RegisterCallback<ClickEvent>(evt =>
                {
                    property.SerializedProperty.colorValue = captured;
                    property.SerializedProperty.serializedObject.ApplyModifiedProperties();
                });

                grid.Add(swatch);
            }

            container.Add(grid);

            // Current color field (editable)
            var colorField = new UnityEditor.UIElements.ColorField("Current");
            colorField.bindingPath = property.SerializedProperty.propertyPath;
            colorField.style.marginTop = 4;
            container.Add(colorField);

            return container;
        }

        public override void OnGUI(Rect rect, BillProperty property)
        {
            var sp = property.SerializedProperty;
            var labelRect = new Rect(rect.x, rect.y, 120, 18);
            EditorGUI.LabelField(labelRect, property.DisplayName);

            float swatchSize = 20;
            float x = rect.x + 124;
            float y = rect.y;
            foreach (var color in s_defaultPalette)
            {
                var swatchRect = new Rect(x, y, swatchSize, swatchSize);
                EditorGUI.DrawRect(swatchRect, color);
                if (GUI.Button(swatchRect, GUIContent.none, GUIStyle.none))
                    sp.colorValue = color;

                x += swatchSize + 2;
                if (x + swatchSize > rect.xMax)
                {
                    x = rect.x + 124;
                    y += swatchSize + 2;
                }
            }

            var fieldRect = new Rect(rect.x + 124, y + swatchSize + 4, rect.width - 128, 18);
            sp.colorValue = EditorGUI.ColorField(fieldRect, sp.colorValue);
        }

        public override float GetPropertyHeight(BillProperty property) => 68f;
    }
}
