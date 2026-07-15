using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor.Drawers
{
    [BillCustomDrawer(typeof(BillFilePathAttribute))]
    public class BillFilePathDrawer : BillAttributeDrawer<BillFilePathAttribute>
    {
        public override VisualElement CreatePropertyGUI(BillProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;

            var textField = new TextField(property.DisplayName);
            textField.bindingPath = property.SerializedProperty.propertyPath;
            textField.style.flexGrow = 1;
            container.Add(textField);

            var browseBtn = new Button(() =>
            {
                string dir = Attribute.ParentFolder ?? "Assets";
                string ext = Attribute.Extensions ?? "";
                string path = Attribute.AbsolutePath
                    ? EditorUtility.OpenFilePanel("Select File", dir, ext)
                    : EditorUtility.OpenFilePanel("Select File", dir, ext);

                if (!string.IsNullOrEmpty(path))
                {
                    if (!Attribute.AbsolutePath && path.StartsWith(Application.dataPath))
                        path = "Assets" + path.Substring(Application.dataPath.Length);

                    property.SerializedProperty.stringValue = path;
                    property.SerializedProperty.serializedObject.ApplyModifiedProperties();
                }
            });
            browseBtn.text = "...";
            browseBtn.style.width = 28;
            browseBtn.style.marginLeft = 2;
            container.Add(browseBtn);

            return container;
        }

        public override void OnGUI(Rect rect, BillProperty property)
        {
            var sp = property.SerializedProperty;
            var fieldRect = new Rect(rect.x, rect.y, rect.width - 32, rect.height);
            var btnRect = new Rect(rect.x + rect.width - 28, rect.y, 28, rect.height);

            sp.stringValue = EditorGUI.TextField(fieldRect, property.DisplayName, sp.stringValue);
            if (GUI.Button(btnRect, "..."))
            {
                string path = EditorUtility.OpenFilePanel("Select File",
                    Attribute.ParentFolder ?? "Assets", Attribute.Extensions ?? "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (!Attribute.AbsolutePath && path.StartsWith(Application.dataPath))
                        path = "Assets" + path.Substring(Application.dataPath.Length);
                    sp.stringValue = path;
                }
            }
        }
    }
}
