using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor.Drawers
{
    [BillCustomDrawer(typeof(BillMinMaxSliderAttribute))]
    public class BillMinMaxSliderDrawer : BillAttributeDrawer<BillMinMaxSliderAttribute>
    {
        public override VisualElement CreatePropertyGUI(BillProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            var label = new Label(property.DisplayName);
            label.style.width = 120;
            container.Add(label);

            var minField = new FloatField();
            minField.style.width = 50;
            container.Add(minField);

            var slider = new MinMaxSlider(Attribute.MinLimit, Attribute.MaxLimit,
                Attribute.MinLimit, Attribute.MaxLimit);
            slider.style.flexGrow = 1;
            slider.style.marginLeft = 4;
            slider.style.marginRight = 4;
            container.Add(slider);

            var maxField = new FloatField();
            maxField.style.width = 50;
            container.Add(maxField);

            // Sync slider <-> fields <-> property
            container.schedule.Execute(() =>
            {
                var sp = property.SerializedProperty;
                if (sp == null) return;
                sp.serializedObject.Update();
                var v = sp.vector2Value;
                slider.lowLimit = Attribute.MinLimit;
                slider.highLimit = Attribute.MaxLimit;
                slider.value = new Vector2(v.x, v.y);
                minField.SetValueWithoutNotify(v.x);
                maxField.SetValueWithoutNotify(v.y);
            }).Every(100);

            slider.RegisterValueChangedCallback(evt =>
            {
                var sp = property.SerializedProperty;
                sp.vector2Value = new Vector2(
                    Mathf.Round(evt.newValue.x * 10f) / 10f,
                    Mathf.Round(evt.newValue.y * 10f) / 10f);
                sp.serializedObject.ApplyModifiedProperties();
            });

            minField.RegisterValueChangedCallback(evt =>
            {
                var sp = property.SerializedProperty;
                sp.vector2Value = new Vector2(evt.newValue, sp.vector2Value.y);
                sp.serializedObject.ApplyModifiedProperties();
            });

            maxField.RegisterValueChangedCallback(evt =>
            {
                var sp = property.SerializedProperty;
                sp.vector2Value = new Vector2(sp.vector2Value.x, evt.newValue);
                sp.serializedObject.ApplyModifiedProperties();
            });

            return container;
        }

        public override void OnGUI(Rect rect, BillProperty property)
        {
            var sp = property.SerializedProperty;
            var v = sp.vector2Value;
            float min = v.x, max = v.y;

            var labelRect = new Rect(rect.x, rect.y, 120, rect.height);
            var minRect = new Rect(rect.x + 124, rect.y, 50, rect.height);
            var sliderRect = new Rect(rect.x + 178, rect.y, rect.width - 302, rect.height);
            var maxRect = new Rect(rect.x + rect.width - 50, rect.y, 50, rect.height);

            EditorGUI.LabelField(labelRect, property.DisplayName);
            min = EditorGUI.FloatField(minRect, min);
            EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, Attribute.MinLimit, Attribute.MaxLimit);
            max = EditorGUI.FloatField(maxRect, max);

            sp.vector2Value = new Vector2(min, max);
        }
    }
}
