using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor.Drawers
{
    [BillCustomDrawer(typeof(BillInlineEditorAttribute))]
    public class BillInlineEditorDrawer : BillAttributeDrawer<BillInlineEditorAttribute>
    {
        public override VisualElement CreatePropertyGUI(BillProperty property)
        {
            var container = new VisualElement();
            container.style.marginTop = 2;
            container.style.marginBottom = 2;

            // Object reference field
            var objField = new UnityEditor.UIElements.ObjectField(property.DisplayName);
            objField.objectType = property.FieldType;
            objField.bindingPath = property.SerializedProperty.propertyPath;
            container.Add(objField);

            // Inline editor container
            var inlineContainer = new VisualElement();
            inlineContainer.style.borderLeftWidth = 2;
            inlineContainer.style.borderLeftColor = new Color(0.3f, 0.5f, 1f, 0.5f);
            inlineContainer.style.paddingLeft = 12;
            inlineContainer.style.marginLeft = 4;
            inlineContainer.style.marginTop = 4;
            container.Add(inlineContainer);

            // Rebuild inline editor when object changes
            container.schedule.Execute(() =>
            {
                inlineContainer.Clear();
                var obj = property.SerializedProperty?.objectReferenceValue;
                if (obj == null) return;

                if (Attribute.ShowHeader)
                {
                    var header = new Label(obj.name);
                    header.style.unityFontStyleAndWeight = FontStyle.Bold;
                    header.style.fontSize = 11;
                    header.style.marginBottom = 4;
                    header.style.color = new Color(0.6f, 0.8f, 1f);
                    inlineContainer.Add(header);
                }

                var so = new SerializedObject(obj);
                var iter = so.GetIterator();
                iter.NextVisible(true); // skip "m_Script"
                while (iter.NextVisible(false))
                {
                    var pf = new UnityEditor.UIElements.PropertyField(iter.Copy());
                    pf.Bind(so);
                    inlineContainer.Add(pf);
                }
            }).Every(300);

            return container;
        }

        public override void OnGUI(Rect rect, BillProperty property)
        {
            var sp = property.SerializedProperty;
            EditorGUI.ObjectField(rect, sp, property.FieldType, new GUIContent(property.DisplayName));

            if (sp.objectReferenceValue != null && Attribute.Expanded)
            {
                EditorGUI.indentLevel++;
                var editor = UnityEditor.Editor.CreateEditor(sp.objectReferenceValue);
                editor?.OnInspectorGUI();
                EditorGUI.indentLevel--;
            }
        }
    }
}
