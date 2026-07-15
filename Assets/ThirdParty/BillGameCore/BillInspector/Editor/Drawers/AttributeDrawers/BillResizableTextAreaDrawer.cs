using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor.Drawers
{
    [BillCustomDrawer(typeof(BillResizableTextAreaAttribute))]
    public class BillResizableTextAreaDrawer : BillAttributeDrawer<BillResizableTextAreaAttribute>
    {
        public override VisualElement CreatePropertyGUI(BillProperty property)
        {
            var field = new TextField(property.DisplayName);
            field.multiline = true;
            field.bindingPath = property.SerializedProperty.propertyPath;
            field.style.minHeight = Attribute.MinLines * 16;
            field.style.maxHeight = Attribute.MaxLines * 16;
            field.style.whiteSpace = WhiteSpace.Normal;
            return field;
        }

        public override void OnGUI(Rect rect, BillProperty property)
        {
            var sp = property.SerializedProperty;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, 16), property.DisplayName);
            float textHeight = Mathf.Max(Attribute.MinLines * 16, rect.height - 18);
            sp.stringValue = EditorGUI.TextArea(
                new Rect(rect.x, rect.y + 18, rect.width, textHeight),
                sp.stringValue);
        }

        public override float GetPropertyHeight(BillProperty property)
        {
            int lines = property.SerializedProperty.stringValue?.Split('\n').Length ?? 1;
            lines = Mathf.Clamp(lines, Attribute.MinLines, Attribute.MaxLines);
            return 18 + lines * 16;
        }
    }
}
