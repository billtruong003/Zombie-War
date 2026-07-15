using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor.Drawers
{
    [BillCustomDrawer(typeof(BillProgressBarAttribute))]
    public class BillProgressBarDrawer : BillAttributeDrawer<BillProgressBarAttribute>
    {
        private static readonly Color[] s_colors =
        {
            Color.white, Color.red, Color.green, Color.blue,
            Color.yellow, Color.cyan, Color.magenta,
            new Color(1f, 0.5f, 0f), new Color(0.6f, 0.3f, 1f)
        };

        public override VisualElement CreatePropertyGUI(BillProperty property)
        {
            var container = new VisualElement();
            container.style.height = 22;
            container.style.marginTop = 2;
            container.style.marginBottom = 2;

            var label = new Label(property.DisplayName);
            label.style.position = Position.Absolute;
            label.style.left = 4;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.fontSize = 11;

            var bar = new VisualElement();
            bar.style.height = new StyleLength(StyleKeyword.Auto);
            bar.style.flexGrow = 0;
            bar.style.borderTopLeftRadius = 3;
            bar.style.borderBottomLeftRadius = 3;
            bar.style.borderTopRightRadius = 3;
            bar.style.borderBottomRightRadius = 3;
            bar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            bar.style.height = 22;

            var fill = new VisualElement();
            fill.style.position = Position.Absolute;
            fill.style.left = 0;
            fill.style.top = 0;
            fill.style.bottom = 0;
            fill.style.borderTopLeftRadius = 3;
            fill.style.borderBottomLeftRadius = 3;
            fill.style.borderTopRightRadius = 3;
            fill.style.borderBottomRightRadius = 3;

            var color = s_colors[(int)Attribute.Color];
            fill.style.backgroundColor = color;

            bar.Add(fill);
            bar.Add(label);
            container.Add(bar);

            // Bind update
            container.schedule.Execute(() =>
            {
                if (property.SerializedProperty == null) return;
                property.SerializedProperty.serializedObject.Update();
                float val = property.SerializedProperty.propertyType == SerializedPropertyType.Integer
                    ? property.SerializedProperty.intValue
                    : property.SerializedProperty.floatValue;
                float t = Mathf.InverseLerp(Attribute.MinValue, Attribute.MaxValue, val);
                fill.style.width = Length.Percent(t * 100f);
                label.text = $"{property.DisplayName}: {val:F0} / {Attribute.MaxValue}";
            }).Every(100);

            return container;
        }

        public override void OnGUI(Rect rect, BillProperty property)
        {
            var sp = property.SerializedProperty;
            float val = sp.propertyType == SerializedPropertyType.Integer
                ? sp.intValue : sp.floatValue;
            float t = Mathf.InverseLerp(Attribute.MinValue, Attribute.MaxValue, val);
            EditorGUI.ProgressBar(rect, t,
                $"{property.DisplayName}: {val:F0} / {Attribute.MaxValue}");
        }

        public override float GetPropertyHeight(BillProperty property) => 22f;
    }
}
