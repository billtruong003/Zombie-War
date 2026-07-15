using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor.Drawers
{
    [BillCustomDrawer(typeof(BillAssetSelectorAttribute))]
    public class BillAssetSelectorDrawer : BillAttributeDrawer<BillAssetSelectorAttribute>
    {
        public override VisualElement CreatePropertyGUI(BillProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;

            var objField = new UnityEditor.UIElements.ObjectField(property.DisplayName);
            objField.objectType = property.FieldType;
            objField.bindingPath = property.SerializedProperty.propertyPath;
            objField.style.flexGrow = 1;
            container.Add(objField);

            if (!string.IsNullOrEmpty(Attribute.Filter))
            {
                var searchBtn = new Button(() =>
                {
                    string[] guids = AssetDatabase.FindAssets(Attribute.Filter,
                        Attribute.Paths ?? new[] { "Assets" });
                    var paths = guids.Select(AssetDatabase.GUIDToAssetPath).ToArray();

                    var menu = new GenericMenu();
                    foreach (var path in paths)
                    {
                        var asset = AssetDatabase.LoadAssetAtPath(path, property.FieldType);
                        if (asset == null) continue;
                        var capturedAsset = asset;
                        menu.AddItem(new GUIContent(path), false, () =>
                        {
                            property.SerializedProperty.objectReferenceValue = capturedAsset;
                            property.SerializedProperty.serializedObject.ApplyModifiedProperties();
                        });
                    }
                    menu.ShowAsContext();
                });
                searchBtn.text = "▼";
                searchBtn.style.width = 22;
                searchBtn.style.marginLeft = 2;
                container.Add(searchBtn);
            }

            return container;
        }

        public override void OnGUI(Rect rect, BillProperty property)
        {
            EditorGUI.ObjectField(rect, property.SerializedProperty,
                property.FieldType, new GUIContent(property.DisplayName));
        }
    }
}
