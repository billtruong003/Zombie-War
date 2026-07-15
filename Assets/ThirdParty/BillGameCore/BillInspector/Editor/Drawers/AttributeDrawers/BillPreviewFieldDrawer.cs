using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor.Drawers
{
    [BillCustomDrawer(typeof(BillPreviewFieldAttribute))]
    public class BillPreviewFieldDrawer : BillAttributeDrawer<BillPreviewFieldAttribute>
    {
        public override VisualElement CreatePropertyGUI(BillProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.FlexStart;

            // Object field
            var objField = new UnityEditor.UIElements.ObjectField(property.DisplayName);
            objField.objectType = property.FieldType;
            objField.bindingPath = property.SerializedProperty.propertyPath;
            objField.style.flexGrow = 1;
            container.Add(objField);

            // Preview image
            var preview = new VisualElement();
            preview.style.width = Attribute.Size;
            preview.style.height = Attribute.Size;
            preview.style.marginLeft = 4;
            preview.style.borderTopLeftRadius = 4;
            preview.style.borderTopRightRadius = 4;
            preview.style.borderBottomLeftRadius = 4;
            preview.style.borderBottomRightRadius = 4;
            preview.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.3f);

            container.Add(preview);

            // Update preview on schedule
            container.schedule.Execute(() =>
            {
                var obj = property.SerializedProperty?.objectReferenceValue;
                if (obj != null)
                {
                    var tex = AssetPreview.GetAssetPreview(obj)
                              ?? AssetPreview.GetMiniThumbnail(obj);
                    if (tex != null)
                        preview.style.backgroundImage = tex;
                }
            }).Every(200);

            return container;
        }

        public override void OnGUI(Rect rect, BillProperty property)
        {
            var sp = property.SerializedProperty;
            var fieldRect = new Rect(rect.x, rect.y, rect.width - Attribute.Size - 4, 18);
            EditorGUI.ObjectField(fieldRect, sp, property.FieldType, new GUIContent(property.DisplayName));

            if (sp.objectReferenceValue != null)
            {
                var previewRect = new Rect(rect.x + rect.width - Attribute.Size,
                    rect.y, Attribute.Size, Attribute.Size);
                var tex = AssetPreview.GetAssetPreview(sp.objectReferenceValue)
                          ?? AssetPreview.GetMiniThumbnail(sp.objectReferenceValue);
                if (tex != null)
                    GUI.DrawTexture(previewRect, tex, ScaleMode.ScaleToFit);
            }
        }

        public override float GetPropertyHeight(BillProperty property) => Mathf.Max(18, Attribute.Size);
    }
}
