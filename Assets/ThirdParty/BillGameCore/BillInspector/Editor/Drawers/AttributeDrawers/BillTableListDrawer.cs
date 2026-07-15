using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor.Drawers
{
    [BillCustomDrawer(typeof(BillTableListAttribute))]
    public class BillTableListDrawer : BillAttributeDrawer<BillTableListAttribute>
    {
        public override VisualElement CreatePropertyGUI(BillProperty property)
        {
            var container = new VisualElement();
            container.style.marginTop = 4;
            container.style.marginBottom = 4;

            var sp = property.SerializedProperty;
            if (!sp.isArray)
            {
                container.Add(new Label($"{property.DisplayName} is not an array/list"));
                return container;
            }

            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;

            var titleLabel = new Label(property.DisplayName);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(titleLabel);

            var countLabel = new Label();
            countLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            countLabel.style.fontSize = 11;
            header.Add(countLabel);

            container.Add(header);

            // Table container
            var table = new VisualElement();
            table.style.borderTopWidth = 1;
            table.style.borderBottomWidth = 1;
            table.style.borderLeftWidth = 1;
            table.style.borderRightWidth = 1;
            table.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            table.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            table.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            table.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            table.style.borderTopLeftRadius = 4;
            table.style.borderTopRightRadius = 4;
            table.style.borderBottomLeftRadius = 4;
            table.style.borderBottomRightRadius = 4;

            // Column headers (from element's SerializedProperty children)
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.2f);
            headerRow.style.paddingTop = 4;
            headerRow.style.paddingBottom = 4;
            headerRow.style.paddingLeft = 4;
            table.Add(headerRow);

            // Rows container
            var rowsContainer = new VisualElement();
            table.Add(rowsContainer);
            container.Add(table);

            // Add/remove buttons
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.justifyContent = Justify.FlexEnd;
            toolbar.style.marginTop = 4;

            if (!Attribute.IsReadOnly)
            {
                var addBtn = new Button(() =>
                {
                    sp.serializedObject.Update();
                    sp.InsertArrayElementAtIndex(sp.arraySize);
                    sp.serializedObject.ApplyModifiedProperties();
                });
                addBtn.text = "+ Add";
                addBtn.style.width = 60;
                toolbar.Add(addBtn);
            }

            container.Add(toolbar);

            // Rebuild table
            container.schedule.Execute(() =>
            {
                sp.serializedObject.Update();
                countLabel.text = $"({sp.arraySize})";

                // Build column headers on first run
                if (headerRow.childCount <= 1 && sp.arraySize > 0)
                {
                    headerRow.Clear();
                    if (Attribute.ShowIndexLabels)
                    {
                        var idxHeader = new Label("#");
                        idxHeader.style.width = 30;
                        idxHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                        idxHeader.style.fontSize = 11;
                        headerRow.Add(idxHeader);
                    }

                    var firstElem = sp.GetArrayElementAtIndex(0);
                    var iter = firstElem.Copy();
                    var end = iter.GetEndProperty();
                    iter.NextVisible(true);
                    do
                    {
                        if (SerializedProperty.EqualContents(iter, end)) break;
                        var colHeader = new Label(ObjectNames.NicifyVariableName(iter.name));
                        colHeader.style.flexGrow = 1;
                        colHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                        colHeader.style.fontSize = 11;
                        headerRow.Add(colHeader);
                    } while (iter.NextVisible(false));

                    if (!Attribute.IsReadOnly)
                    {
                        var actHeader = new Label("");
                        actHeader.style.width = 24;
                        headerRow.Add(actHeader);
                    }
                }

                // Rebuild rows
                int currentRowCount = rowsContainer.childCount;
                int targetCount = sp.arraySize;

                if (Attribute.ShowPaging && Attribute.PageSize > 0)
                    targetCount = Mathf.Min(targetCount, Attribute.PageSize);

                // Simple rebuild (optimize later with pooling)
                if (currentRowCount != targetCount)
                {
                    rowsContainer.Clear();
                    for (int i = 0; i < targetCount; i++)
                    {
                        var elem = sp.GetArrayElementAtIndex(i);
                        var row = new VisualElement();
                        row.style.flexDirection = FlexDirection.Row;
                        row.style.paddingTop = 2;
                        row.style.paddingBottom = 2;
                        row.style.paddingLeft = 4;
                        if (i % 2 == 1)
                            row.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.15f);

                        if (Attribute.ShowIndexLabels)
                        {
                            var idxLabel = new Label(i.ToString());
                            idxLabel.style.width = 30;
                            idxLabel.style.fontSize = 11;
                            idxLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                            row.Add(idxLabel);
                        }

                        var elemIter = elem.Copy();
                        var elemEnd = elemIter.GetEndProperty();
                        elemIter.NextVisible(true);
                        do
                        {
                            if (SerializedProperty.EqualContents(elemIter, elemEnd)) break;
                            var field = new PropertyField(elemIter.Copy());
                            field.label = "";
                            field.style.flexGrow = 1;
                            field.Bind(sp.serializedObject);
                            row.Add(field);
                        } while (elemIter.NextVisible(false));

                        if (!Attribute.IsReadOnly)
                        {
                            int capturedIdx = i;
                            var delBtn = new Button(() =>
                            {
                                sp.serializedObject.Update();
                                sp.DeleteArrayElementAtIndex(capturedIdx);
                                sp.serializedObject.ApplyModifiedProperties();
                            });
                            delBtn.text = "×";
                            delBtn.style.width = 24;
                            delBtn.style.height = 18;
                            row.Add(delBtn);
                        }

                        rowsContainer.Add(row);
                    }
                }
            }).Every(300);

            return container;
        }

        public override void OnGUI(Rect rect, BillProperty property)
        {
            EditorGUI.PropertyField(rect, property.SerializedProperty,
                new GUIContent(property.DisplayName), true);
        }

        public override float GetPropertyHeight(BillProperty property)
        {
            return EditorGUI.GetPropertyHeight(property.SerializedProperty, true);
        }
    }
}
