using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor.Drawers
{
    [BillCustomDrawer(typeof(BillSliderAttribute))]
    public class BillSliderDrawer : BillAttributeDrawer<BillSliderAttribute>
    {
        public override VisualElement CreatePropertyGUI(BillProperty property)
        {
            if (property.FieldType == typeof(int))
            {
                var field = new SliderInt(property.DisplayName,
                    (int)Attribute.MinValue, (int)Attribute.MaxValue);
                field.showInputField = true;
                field.bindingPath = property.SerializedProperty.propertyPath;
                return field;
            }
            else
            {
                var field = new Slider(property.DisplayName,
                    Attribute.MinValue, Attribute.MaxValue);
                field.showInputField = true;
                field.bindingPath = property.SerializedProperty.propertyPath;
                return field;
            }
        }

        public override void OnGUI(Rect rect, BillProperty property)
        {
            var sp = property.SerializedProperty;
            if (sp.propertyType == SerializedPropertyType.Integer)
                sp.intValue = EditorGUI.IntSlider(rect, property.DisplayName,
                    sp.intValue, (int)Attribute.MinValue, (int)Attribute.MaxValue);
            else if (sp.propertyType == SerializedPropertyType.Float)
                sp.floatValue = EditorGUI.Slider(rect, property.DisplayName,
                    sp.floatValue, Attribute.MinValue, Attribute.MaxValue);
        }
    }
}
