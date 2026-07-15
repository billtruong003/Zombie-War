using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor
{
    /// <summary>
    /// Main custom editor that replaces Unity's default inspector.
    /// Processes all BillInspector attributes and draws them using
    /// the UI Toolkit drawer pipeline (with IMGUI fallback).
    ///
    /// Only activates on types that have at least one BillAttribute.
    /// Types without any BillAttribute get Unity's default inspector.
    /// </summary>
    [CanEditMultipleObjects]
    public class BillInspectorEditor : UnityEditor.Editor
    {
        private BillPropertyTree _tree;
        private bool _hasBillAttributes;

        // Group state
        private readonly Dictionary<string, bool> _foldoutStates = new();
        private readonly Dictionary<string, int> _tabStates = new();

        // ═══════════════════════════════════════════════════════
        // Initialization
        // ═══════════════════════════════════════════════════════

        private void OnEnable()
        {
            _tree = new BillPropertyTree(serializedObject);
            _hasBillAttributes = _tree.Properties.Any(p => p.AllAttributes.Count > 0)
                                 || _tree.ButtonMethods.Count > 0;
        }

        // ═══════════════════════════════════════════════════════
        // UI Toolkit path (primary for Unity 6)
        // ═══════════════════════════════════════════════════════

        public override VisualElement CreateInspectorGUI()
        {
            if (!_hasBillAttributes)
                return null; // Fall back to default inspector

            var root = new VisualElement();
            root.style.paddingTop = 4;

            serializedObject.Update();

            // Track which properties are already drawn via groups
            var drawnProperties = new HashSet<string>();

            // 1. Draw ungrouped properties in order
            foreach (var prop in _tree.Properties)
            {
                if (prop.GroupAttributes.Count > 0)
                {
                    // Draw group if not already drawn
                    foreach (var groupAttr in prop.GroupAttributes)
                    {
                        if (drawnProperties.Contains("__group__" + groupAttr.GroupPath))
                            continue;
                        drawnProperties.Add("__group__" + groupAttr.GroupPath);

                        var groupElement = DrawGroupUIToolkit(groupAttr, _tree.Groups[groupAttr.GroupPath]);
                        if (groupElement != null)
                        {
                            root.Add(groupElement);
                            foreach (var gp in _tree.Groups[groupAttr.GroupPath])
                                drawnProperties.Add(gp.Name);
                        }
                    }
                }

                if (drawnProperties.Contains(prop.Name))
                    continue;

                var element = DrawPropertyUIToolkit(prop);
                if (element != null)
                    root.Add(element);
                drawnProperties.Add(prop.Name);
            }

            // 2. Draw buttons
            DrawButtonsUIToolkit(root);

            return root;
        }

        private VisualElement DrawPropertyUIToolkit(BillProperty prop)
        {
            var container = new VisualElement();
            container.style.marginBottom = 1;

            // Title attribute
            var titleAttr = prop.GetAttribute<BillTitleAttribute>();
            if (titleAttr != null)
            {
                container.Add(CreateTitleElement(titleAttr));
            }

            // InfoBox (before field)
            foreach (var meta in prop.MetaAttributes)
            {
                if (meta is BillInfoBoxAttribute infoBox)
                {
                    var box = CreateInfoBox(infoBox);
                    if (box != null) container.Add(box);
                }
            }

            // Visibility check — hide container if condition fails
            container.schedule.Execute(() =>
            {
                bool visible = EvaluateVisibility(prop);
                container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

                bool enabled = EvaluateEnabled(prop);
                container.SetEnabled(enabled);
            }).Every(250);

            // Main field: use custom drawer or default PropertyField
            VisualElement fieldElement = null;

            if (prop.DrawerAttribute != null)
            {
                var drawer = BillDrawerLocator.CreateDrawer(prop.DrawerAttribute);
                if (drawer != null)
                    fieldElement = drawer.CreatePropertyGUI(prop);
            }

            if (fieldElement == null)
            {
                // Default PropertyField binding
                var pf = new PropertyField(prop.SerializedProperty, prop.DisplayName);
                pf.bindingPath = prop.SerializedProperty.propertyPath;
                fieldElement = pf;
            }

            // Apply indent
            var indentAttr = prop.GetAttribute<BillIndentAttribute>();
            if (indentAttr != null)
            {
                fieldElement.style.marginLeft = indentAttr.Level * 15;
            }

            // Apply suffix
            var suffixAttr = prop.GetAttribute<BillSuffixAttribute>();
            if (suffixAttr != null)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                fieldElement.style.flexGrow = 1;
                row.Add(fieldElement);

                var suffix = new Label(suffixAttr.Text);
                suffix.style.marginLeft = 4;
                suffix.style.color = new Color(0.6f, 0.6f, 0.6f);
                suffix.style.fontSize = 11;
                row.Add(suffix);

                container.Add(row);
            }
            else
            {
                container.Add(fieldElement);
            }

            // Validation (Required)
            var requiredAttr = prop.GetAttribute<BillRequiredAttribute>();
            if (requiredAttr != null)
            {
                var errorLabel = new Label(requiredAttr.Message);
                errorLabel.style.color = new Color(1f, 0.3f, 0.3f);
                errorLabel.style.fontSize = 10;
                errorLabel.style.display = DisplayStyle.None;
                container.Add(errorLabel);

                container.schedule.Execute(() =>
                {
                    bool isEmpty = IsEmpty(prop.SerializedProperty);
                    errorLabel.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
                    if (isEmpty)
                    {
                        fieldElement.style.borderBottomColor = new Color(1f, 0.3f, 0.3f, 0.8f);
                        fieldElement.style.borderBottomWidth = 2;
                    }
                    else
                    {
                        fieldElement.style.borderBottomWidth = 0;
                    }
                }).Every(500);
            }

            return container;
        }

        // ═══════════════════════════════════════════════════════
        // Group rendering (UI Toolkit)
        // ═══════════════════════════════════════════════════════

        private VisualElement DrawGroupUIToolkit(BillGroupAttribute groupAttr, List<BillProperty> properties)
        {
            switch (groupAttr)
            {
                case BillBoxGroupAttribute box:
                    return DrawBoxGroupUIToolkit(box, properties);
                case BillFoldoutGroupAttribute foldout:
                    return DrawFoldoutGroupUIToolkit(foldout, properties);
                case BillTabGroupAttribute tab:
                    return DrawTabGroupUIToolkit(tab);
                case BillHorizontalGroupAttribute horizontal:
                    return DrawHorizontalGroupUIToolkit(horizontal, properties);
                case BillVerticalGroupAttribute vertical:
                    return DrawVerticalGroupUIToolkit(vertical, properties);
                case BillToggleGroupAttribute toggle:
                    return DrawToggleGroupUIToolkit(toggle, properties);
                default:
                    return null;
            }
        }

        private VisualElement DrawBoxGroupUIToolkit(BillBoxGroupAttribute attr, List<BillProperty> properties)
        {
            var box = new VisualElement();
            box.style.borderTopWidth = 1;
            box.style.borderBottomWidth = 1;
            box.style.borderLeftWidth = 1;
            box.style.borderRightWidth = 1;
            box.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            box.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            box.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            box.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            box.style.borderTopLeftRadius = 4;
            box.style.borderTopRightRadius = 4;
            box.style.borderBottomLeftRadius = 4;
            box.style.borderBottomRightRadius = 4;
            box.style.paddingTop = 8;
            box.style.paddingBottom = 8;
            box.style.paddingLeft = 8;
            box.style.paddingRight = 8;
            box.style.marginTop = 4;
            box.style.marginBottom = 4;

            if (attr.ShowLabel && !string.IsNullOrEmpty(attr.GroupPath))
            {
                var header = new Label(ObjectNames.NicifyVariableName(attr.GroupPath));
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
                header.style.marginBottom = 6;
                header.style.fontSize = 12;
                box.Add(header);
            }

            foreach (var prop in properties)
            {
                var el = DrawPropertyUIToolkit(prop);
                if (el != null) box.Add(el);
            }

            return box;
        }

        private VisualElement DrawFoldoutGroupUIToolkit(BillFoldoutGroupAttribute attr, List<BillProperty> properties)
        {
            var foldout = new Foldout();
            foldout.text = ObjectNames.NicifyVariableName(
                attr.GroupPath.Contains("/") ? attr.GroupPath.Split('/').Last() : attr.GroupPath);
            foldout.value = attr.Expanded;
            foldout.style.marginTop = 2;
            foldout.style.marginBottom = 2;

            foreach (var prop in properties)
            {
                var el = DrawPropertyUIToolkit(prop);
                if (el != null) foldout.Add(el);
            }

            return foldout;
        }

        private VisualElement DrawTabGroupUIToolkit(BillTabGroupAttribute attr)
        {
            // Collect all tabs for this group ID
            var allTabProperties = new Dictionary<string, List<BillProperty>>();
            foreach (var prop in _tree.Properties)
            {
                foreach (var ga in prop.GroupAttributes)
                {
                    if (ga is BillTabGroupAttribute tab && tab.GroupPath == attr.GroupPath)
                    {
                        if (!allTabProperties.ContainsKey(tab.TabName))
                            allTabProperties[tab.TabName] = new List<BillProperty>();
                        allTabProperties[tab.TabName].Add(prop);
                    }
                }
            }

            var container = new VisualElement();
            container.style.marginTop = 4;
            container.style.marginBottom = 4;

            // Tab buttons row
            var tabBar = new VisualElement();
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.borderBottomWidth = 1;
            tabBar.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            tabBar.style.marginBottom = 8;

            // Tab content panels
            var panels = new Dictionary<string, VisualElement>();

            int idx = 0;
            foreach (var kvp in allTabProperties)
            {
                string tabName = kvp.Key;
                int capturedIdx = idx;

                var tabBtn = new Button(() =>
                {
                    _tabStates[attr.GroupPath] = capturedIdx;
                    foreach (var p in panels)
                        p.Value.style.display = DisplayStyle.None;
                    if (panels.ContainsKey(tabName))
                        panels[tabName].style.display = DisplayStyle.Flex;

                    // Update button styles
                    for (int i = 0; i < tabBar.childCount; i++)
                    {
                        var b = tabBar[i] as Button;
                        if (b != null)
                        {
                            b.style.borderBottomWidth = i == capturedIdx ? 2 : 0;
                            b.style.borderBottomColor = new Color(0.24f, 0.49f, 0.91f);
                        }
                    }
                });
                tabBtn.text = tabName;
                tabBtn.style.flexGrow = 1;
                tabBtn.style.borderTopLeftRadius = 0;
                tabBtn.style.borderTopRightRadius = 0;
                tabBtn.style.borderBottomLeftRadius = 0;
                tabBtn.style.borderBottomRightRadius = 0;

                if (!_tabStates.ContainsKey(attr.GroupPath))
                    _tabStates[attr.GroupPath] = 0;

                bool isActive = _tabStates[attr.GroupPath] == idx;
                tabBtn.style.borderBottomWidth = isActive ? 2 : 0;
                tabBtn.style.borderBottomColor = new Color(0.24f, 0.49f, 0.91f);

                tabBar.Add(tabBtn);

                // Panel
                var panel = new VisualElement();
                panel.style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;
                foreach (var prop in kvp.Value)
                {
                    var el = DrawPropertyUIToolkit(prop);
                    if (el != null) panel.Add(el);
                }
                panels[tabName] = panel;

                idx++;
            }

            container.Add(tabBar);
            foreach (var p in panels.Values)
                container.Add(p);

            return container;
        }

        private VisualElement DrawHorizontalGroupUIToolkit(BillHorizontalGroupAttribute attr, List<BillProperty> properties)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 2;
            row.style.marginBottom = 2;

            foreach (var prop in properties)
            {
                var el = DrawPropertyUIToolkit(prop);
                if (el == null) continue;

                var hAttr = prop.GroupAttributes.OfType<BillHorizontalGroupAttribute>().FirstOrDefault();
                if (hAttr?.Width > 0)
                    el.style.width = Length.Percent(hAttr.Width * 100f);
                else
                    el.style.flexGrow = 1;

                el.style.marginRight = 4;
                row.Add(el);
            }

            return row;
        }

        private VisualElement DrawVerticalGroupUIToolkit(BillVerticalGroupAttribute attr, List<BillProperty> properties)
        {
            var col = new VisualElement();
            col.style.marginTop = 2;
            col.style.marginBottom = 2;
            if (attr.Width > 0)
                col.style.width = Length.Percent(attr.Width * 100f);

            foreach (var prop in properties)
            {
                var el = DrawPropertyUIToolkit(prop);
                if (el != null) col.Add(el);
            }

            return col;
        }

        private VisualElement DrawToggleGroupUIToolkit(BillToggleGroupAttribute attr, List<BillProperty> properties)
        {
            var container = new VisualElement();
            container.style.marginTop = 4;
            container.style.marginBottom = 4;
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            container.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            container.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            container.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 8;
            container.style.paddingLeft = 8;
            container.style.paddingRight = 8;

            // Header toggle — GroupPath is the bool field name
            var toggleLabel = attr.Label ?? ObjectNames.NicifyVariableName(attr.GroupPath);
            var toggle = new Toggle(toggleLabel);
            toggle.style.unityFontStyleAndWeight = FontStyle.Bold;

            var toggleProp = serializedObject.FindProperty(attr.GroupPath);
            if (toggleProp != null)
                toggle.value = toggleProp.boolValue;

            container.Add(toggle);

            // Content panel for child properties
            var content = new VisualElement();
            content.style.marginTop = 4;
            content.style.paddingLeft = 4;

            foreach (var prop in properties)
            {
                var el = DrawPropertyUIToolkit(prop);
                if (el != null) content.Add(el);
            }

            container.Add(content);

            // Toggle controls content visibility and enabled state
            void UpdateContentState(bool enabled)
            {
                content.SetEnabled(enabled);
                content.style.opacity = enabled ? 1f : 0.5f;
            }

            UpdateContentState(toggle.value);

            toggle.RegisterValueChangedCallback(evt =>
            {
                if (toggleProp != null)
                {
                    toggleProp.boolValue = evt.newValue;
                    serializedObject.ApplyModifiedProperties();
                }
                UpdateContentState(evt.newValue);
            });

            // Keep in sync with serialized value
            container.schedule.Execute(() =>
            {
                if (toggleProp != null)
                {
                    serializedObject.Update();
                    bool current = toggleProp.boolValue;
                    if (toggle.value != current)
                    {
                        toggle.SetValueWithoutNotify(current);
                        UpdateContentState(current);
                    }
                }
            }).Every(250);

            return container;
        }

        // ═══════════════════════════════════════════════════════
        // Buttons rendering (UI Toolkit)
        // ═══════════════════════════════════════════════════════

        private void DrawButtonsUIToolkit(VisualElement root)
        {
            if (_tree.ButtonMethods.Count == 0) return;

            var buttonContainer = new VisualElement();
            buttonContainer.style.marginTop = 8;

            // Group buttons
            var grouped = new Dictionary<string, List<BillPropertyTree.MethodRecord>>();
            var ungrouped = new List<BillPropertyTree.MethodRecord>();

            foreach (var m in _tree.ButtonMethods)
            {
                if (m.ButtonGroupAttribute != null)
                {
                    if (!grouped.ContainsKey(m.ButtonGroupAttribute.GroupName))
                        grouped[m.ButtonGroupAttribute.GroupName] = new();
                    grouped[m.ButtonGroupAttribute.GroupName].Add(m);
                }
                else
                {
                    ungrouped.Add(m);
                }
            }

            // Draw grouped buttons in rows
            foreach (var group in grouped)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom = 2;

                foreach (var m in group.Value)
                {
                    var btn = CreateButtonElement(m);
                    btn.style.flexGrow = 1;
                    btn.style.marginRight = 2;
                    row.Add(btn);
                }

                buttonContainer.Add(row);
            }

            // Draw ungrouped buttons
            foreach (var m in ungrouped)
            {
                buttonContainer.Add(CreateButtonElement(m));
            }

            root.Add(buttonContainer);
        }

        private Button CreateButtonElement(BillPropertyTree.MethodRecord record)
        {
            var label = record.ButtonAttribute.Label
                        ?? ObjectNames.NicifyVariableName(record.Method.Name);

            var btn = new Button(() =>
            {
                foreach (var t in targets)
                {
                    var result = record.Method.Invoke(t, null);
                    if (record.ShowResultAs != null && result != null)
                    {
                        Debug.Log(string.Format(record.ShowResultAs.Format, result));
                    }
                }
            });
            btn.text = label;
            btn.style.marginTop = 2;
            btn.style.marginBottom = 2;

            // Button size
            switch (record.ButtonAttribute.Size)
            {
                case ButtonSize.Small:
                    btn.style.height = 22;
                    btn.style.fontSize = 11;
                    break;
                case ButtonSize.Large:
                    btn.style.height = 36;
                    btn.style.fontSize = 14;
                    break;
                default:
                    btn.style.height = 28;
                    break;
            }

            return btn;
        }

        // ═══════════════════════════════════════════════════════
        // IMGUI fallback
        // ═══════════════════════════════════════════════════════

        public override void OnInspectorGUI()
        {
            if (!_hasBillAttributes)
            {
                DrawDefaultInspector();
                return;
            }

            serializedObject.Update();

            var drawnProperties = new HashSet<string>();

            // Draw grouped properties first
            foreach (var prop in _tree.Properties)
            {
                if (prop.GroupAttributes.Count > 0)
                {
                    foreach (var groupAttr in prop.GroupAttributes)
                    {
                        var groupKey = "__group__" + groupAttr.GroupPath;
                        if (drawnProperties.Contains(groupKey)) continue;
                        drawnProperties.Add(groupKey);

                        if (_tree.Groups.TryGetValue(groupAttr.GroupPath, out var groupProps))
                        {
                            DrawGroupIMGUI(groupAttr, groupProps, drawnProperties);
                            foreach (var gp in groupProps)
                                drawnProperties.Add(gp.Name);
                        }
                    }
                }
            }

            foreach (var prop in _tree.Properties)
            {
                if (drawnProperties.Contains(prop.Name)) continue;

                // Visibility check
                if (!EvaluateVisibility(prop)) continue;

                // Enable check
                bool wasEnabled = GUI.enabled;
                if (!EvaluateEnabled(prop)) GUI.enabled = false;

                // Title
                var titleAttr = prop.GetAttribute<BillTitleAttribute>();
                if (titleAttr != null) DrawTitleIMGUI(titleAttr);

                // InfoBox
                foreach (var meta in prop.MetaAttributes)
                {
                    if (meta is BillInfoBoxAttribute info)
                        DrawInfoBoxIMGUI(info);
                }

                // Main field
                if (prop.DrawerAttribute != null)
                {
                    var drawer = BillDrawerLocator.CreateDrawer(prop.DrawerAttribute);
                    if (drawer != null)
                    {
                        float height = drawer.GetPropertyHeight(prop);
                        var rect = EditorGUILayout.GetControlRect(true, height);
                        drawer.OnGUI(rect, prop);
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(prop.SerializedProperty,
                            new GUIContent(prop.DisplayName), true);
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(prop.SerializedProperty,
                        new GUIContent(prop.DisplayName), true);
                }

                // Required validation
                var req = prop.GetAttribute<BillRequiredAttribute>();
                if (req != null && IsEmpty(prop.SerializedProperty))
                {
                    EditorGUILayout.HelpBox(req.Message, MessageType.Error);
                }

                GUI.enabled = wasEnabled;
                drawnProperties.Add(prop.Name);
            }

            // Buttons
            if (_tree.ButtonMethods.Count > 0)
            {
                EditorGUILayout.Space(8);
                foreach (var m in _tree.ButtonMethods)
                {
                    if (GUILayout.Button(m.ButtonAttribute.Label
                        ?? ObjectNames.NicifyVariableName(m.Method.Name)))
                    {
                        foreach (var t in targets)
                            m.Method.Invoke(t, null);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ═══════════════════════════════════════════════════════
        // IMGUI Group rendering
        // ═══════════════════════════════════════════════════════

        private void DrawGroupIMGUI(BillGroupAttribute groupAttr, List<BillProperty> properties, HashSet<string> drawnProperties)
        {
            switch (groupAttr)
            {
                case BillBoxGroupAttribute box:
                    if (box.ShowLabel && !string.IsNullOrEmpty(box.GroupPath))
                        EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(box.GroupPath), EditorStyles.boldLabel);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    foreach (var prop in properties) DrawSinglePropertyIMGUI(prop);
                    EditorGUILayout.EndVertical();
                    break;

                case BillFoldoutGroupAttribute foldout:
                    var foldKey = foldout.GroupPath;
                    if (!_foldoutStates.ContainsKey(foldKey))
                        _foldoutStates[foldKey] = foldout.Expanded;
                    _foldoutStates[foldKey] = EditorGUILayout.Foldout(_foldoutStates[foldKey],
                        ObjectNames.NicifyVariableName(
                            foldout.GroupPath.Contains("/") ? foldout.GroupPath.Split('/').Last() : foldout.GroupPath),
                        true);
                    if (_foldoutStates[foldKey])
                    {
                        EditorGUI.indentLevel++;
                        foreach (var prop in properties) DrawSinglePropertyIMGUI(prop);
                        EditorGUI.indentLevel--;
                    }
                    break;

                case BillHorizontalGroupAttribute:
                    EditorGUILayout.BeginHorizontal();
                    foreach (var prop in properties) DrawSinglePropertyIMGUI(prop);
                    EditorGUILayout.EndHorizontal();
                    break;

                case BillVerticalGroupAttribute:
                    EditorGUILayout.BeginVertical();
                    foreach (var prop in properties) DrawSinglePropertyIMGUI(prop);
                    EditorGUILayout.EndVertical();
                    break;

                case BillToggleGroupAttribute toggle:
                    var toggleProp = serializedObject.FindProperty(toggle.GroupPath);
                    bool isEnabled = toggleProp != null && toggleProp.boolValue;
                    var label = toggle.Label ?? ObjectNames.NicifyVariableName(toggle.GroupPath);
                    if (toggleProp != null)
                    {
                        toggleProp.boolValue = EditorGUILayout.ToggleLeft(label, toggleProp.boolValue, EditorStyles.boldLabel);
                    }
                    EditorGUI.BeginDisabledGroup(!isEnabled);
                    EditorGUI.indentLevel++;
                    foreach (var prop in properties) DrawSinglePropertyIMGUI(prop);
                    EditorGUI.indentLevel--;
                    EditorGUI.EndDisabledGroup();
                    break;

                default:
                    foreach (var prop in properties) DrawSinglePropertyIMGUI(prop);
                    break;
            }
        }

        private void DrawSinglePropertyIMGUI(BillProperty prop)
        {
            if (!EvaluateVisibility(prop)) return;

            bool wasEnabled = GUI.enabled;
            if (!EvaluateEnabled(prop)) GUI.enabled = false;

            var titleAttr = prop.GetAttribute<BillTitleAttribute>();
            if (titleAttr != null) DrawTitleIMGUI(titleAttr);

            foreach (var meta in prop.MetaAttributes)
            {
                if (meta is BillInfoBoxAttribute info)
                    DrawInfoBoxIMGUI(info);
            }

            if (prop.DrawerAttribute != null)
            {
                var drawer = BillDrawerLocator.CreateDrawer(prop.DrawerAttribute);
                if (drawer != null)
                {
                    float height = drawer.GetPropertyHeight(prop);
                    var rect = EditorGUILayout.GetControlRect(true, height);
                    drawer.OnGUI(rect, prop);
                }
                else
                {
                    EditorGUILayout.PropertyField(prop.SerializedProperty,
                        new GUIContent(prop.DisplayName), true);
                }
            }
            else
            {
                EditorGUILayout.PropertyField(prop.SerializedProperty,
                    new GUIContent(prop.DisplayName), true);
            }

            var req = prop.GetAttribute<BillRequiredAttribute>();
            if (req != null && IsEmpty(prop.SerializedProperty))
                EditorGUILayout.HelpBox(req.Message, MessageType.Error);

            GUI.enabled = wasEnabled;
        }

        // ═══════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════

        private bool EvaluateVisibility(BillProperty prop)
        {
            foreach (var meta in prop.MetaAttributes)
            {
                if (meta is BillShowIfAttribute showIf)
                {
                    if (!ConditionEvaluator.Evaluate(showIf, serializedObject))
                        return false;
                }
                else if (meta is BillHideIfAttribute hideIf)
                {
                    if (ConditionEvaluator.Evaluate(hideIf, serializedObject))
                        return false;
                }
            }
            return true;
        }

        private bool EvaluateEnabled(BillProperty prop)
        {
            if (prop.HasAttribute<BillReadOnlyAttribute>())
                return false;

            foreach (var meta in prop.MetaAttributes)
            {
                if (meta is BillEnableIfAttribute enableIf)
                {
                    if (!ConditionEvaluator.Evaluate(enableIf, serializedObject))
                        return false;
                }
                else if (meta is BillDisableIfAttribute disableIf)
                {
                    if (ConditionEvaluator.Evaluate(disableIf, serializedObject))
                        return false;
                }
            }
            return true;
        }

        private static bool IsEmpty(SerializedProperty sp)
        {
            if (sp == null) return true;
            switch (sp.propertyType)
            {
                case SerializedPropertyType.String:
                    return string.IsNullOrEmpty(sp.stringValue);
                case SerializedPropertyType.ObjectReference:
                    return sp.objectReferenceValue == null;
                case SerializedPropertyType.ExposedReference:
                    return sp.exposedReferenceValue == null;
                case SerializedPropertyType.ArraySize:
                    return sp.intValue == 0;
                default:
                    return false;
            }
        }

        private VisualElement CreateTitleElement(BillTitleAttribute attr)
        {
            var container = new VisualElement();
            container.style.marginTop = 8;
            container.style.marginBottom = 4;

            var title = new Label(attr.Title);
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = attr.Bold ? FontStyle.Bold : FontStyle.Normal;
            container.Add(title);

            if (!string.IsNullOrEmpty(attr.Subtitle))
            {
                var sub = new Label(attr.Subtitle);
                sub.style.fontSize = 10;
                sub.style.color = new Color(0.6f, 0.6f, 0.6f);
                container.Add(sub);
            }

            if (attr.HorizontalLine)
            {
                var line = new VisualElement();
                line.style.height = 1;
                line.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
                line.style.marginTop = 4;
                container.Add(line);
            }

            return container;
        }

        private VisualElement CreateInfoBox(BillInfoBoxAttribute attr)
        {
            // TODO: evaluate VisibleIf condition
            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.paddingTop = 6;
            box.style.paddingBottom = 6;
            box.style.paddingLeft = 8;
            box.style.paddingRight = 8;
            box.style.marginBottom = 4;
            box.style.borderTopLeftRadius = 4;
            box.style.borderTopRightRadius = 4;
            box.style.borderBottomLeftRadius = 4;
            box.style.borderBottomRightRadius = 4;

            Color bgColor = attr.Type switch
            {
                InfoType.Info => new Color(0.2f, 0.4f, 0.8f, 0.15f),
                InfoType.Warning => new Color(0.9f, 0.7f, 0.1f, 0.15f),
                InfoType.Error => new Color(0.9f, 0.2f, 0.2f, 0.15f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.1f),
            };
            box.style.backgroundColor = bgColor;

            var label = new Label(attr.Message);
            label.style.fontSize = 11;
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);

            return box;
        }

        private void DrawTitleIMGUI(BillTitleAttribute attr)
        {
            EditorGUILayout.Space(6);
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField(attr.Title, style);
            if (!string.IsNullOrEmpty(attr.Subtitle))
            {
                var subStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
                EditorGUILayout.LabelField(attr.Subtitle, subStyle);
            }
            if (attr.HorizontalLine)
            {
                var rect = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.4f));
            }
            EditorGUILayout.Space(2);
        }

        private void DrawInfoBoxIMGUI(BillInfoBoxAttribute attr)
        {
            var msgType = attr.Type switch
            {
                InfoType.Info => MessageType.Info,
                InfoType.Warning => MessageType.Warning,
                InfoType.Error => MessageType.Error,
                _ => MessageType.None,
            };
            EditorGUILayout.HelpBox(attr.Message, msgType);
        }
    }

    /// <summary>Concrete editor registration for MonoBehaviour types.</summary>
    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
    [CanEditMultipleObjects]
    public class BillMonoBehaviourInspector : BillInspectorEditor { }

    /// <summary>Concrete editor registration for ScriptableObject types.</summary>
    [CustomEditor(typeof(ScriptableObject), true, isFallback = true)]
    [CanEditMultipleObjects]
    public class BillScriptableObjectInspector : BillInspectorEditor { }
}
