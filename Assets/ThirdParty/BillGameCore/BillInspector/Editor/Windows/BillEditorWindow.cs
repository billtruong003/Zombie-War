using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor
{
    /// <summary>
    /// Base class for attribute-driven editor windows.
    /// Fields, properties, and methods with BillInspector attributes
    /// are automatically drawn as inspector UI.
    ///
    /// Usage:
    /// public class MyWindow : BillEditorWindow
    /// {
    ///     [MenuItem("Tools/My Window")]
    ///     static void Open() => GetWindow&lt;MyWindow&gt;("My Window");
    ///
    ///     [BillSlider(0, 100)]
    ///     public float myValue;
    ///
    ///     [BillButton("Do Thing")]
    ///     void DoThing() { ... }
    /// }
    /// </summary>
    public abstract class BillEditorWindow : EditorWindow
    {
        private WindowPropertyTree _tree;
        private VisualElement _rootContainer;
        [NonSerialized]
        private bool _dirty = true;

        // Serialized state for window fields
        [SerializeField, HideInInspector]
        private string _serializedWindowState;

        protected virtual void OnEnable()
        {
            _tree = new WindowPropertyTree(this);
            RestoreState();
        }

        protected virtual void OnDisable()
        {
            SaveState();
        }

        public void CreateGUI()
        {
            _rootContainer = new VisualElement();
            _rootContainer.style.paddingTop = 8;
            _rootContainer.style.paddingLeft = 8;
            _rootContainer.style.paddingRight = 8;
            _rootContainer.style.paddingBottom = 8;

            rootVisualElement.Add(_rootContainer);
            RebuildUI();
        }

        /// <summary>Override to add custom UI before the auto-generated content.</summary>
        protected virtual void OnCreateHeader(VisualElement root) { }

        /// <summary>Override to add custom UI after the auto-generated content.</summary>
        protected virtual void OnCreateFooter(VisualElement root) { }

        /// <summary>Force rebuild the window UI.</summary>
        protected new void SetDirty() => _dirty = true;

        private void RebuildUI()
        {
            _rootContainer.Clear();

            OnCreateHeader(_rootContainer);

            var drawnFields = new HashSet<string>();

            // Draw grouped fields first
            foreach (var group in _tree.Groups)
            {
                var groupElement = DrawWindowGroup(group.Key, group.Value);
                if (groupElement != null)
                {
                    _rootContainer.Add(groupElement);
                    foreach (var f in group.Value)
                        drawnFields.Add(f.Name);
                }
            }

            // Draw ungrouped fields
            foreach (var field in _tree.Fields)
            {
                if (drawnFields.Contains(field.Name)) continue;
                var element = DrawWindowField(field);
                if (element != null) _rootContainer.Add(element);
            }

            // Buttons
            DrawWindowButtons(_rootContainer);

            OnCreateFooter(_rootContainer);
        }

        private VisualElement DrawWindowField(WindowField field)
        {
            var container = new VisualElement();
            container.style.marginBottom = 2;

            // Visibility schedule
            if (field.ShowIfCondition != null)
            {
                container.schedule.Execute(() =>
                {
                    bool visible = ConditionEvaluator.EvaluateString(field.ShowIfCondition, this);
                    container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                }).Every(100);
            }

            // Title
            var titleAttr = field.Field.GetCustomAttribute<BillTitleAttribute>();
            if (titleAttr != null)
            {
                var title = new Label(ValueResolver.ResolveString(titleAttr.Title, this));
                title.style.fontSize = 14;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.marginTop = 8;
                title.style.marginBottom = 4;
                container.Add(title);

                if (!string.IsNullOrEmpty(titleAttr.Subtitle))
                {
                    var sub = new Label(titleAttr.Subtitle);
                    sub.style.fontSize = 10;
                    sub.style.color = new Color(0.6f, 0.6f, 0.6f);
                    container.Add(sub);
                }

                var line = new VisualElement();
                line.style.height = 1;
                line.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
                line.style.marginTop = 4;
                line.style.marginBottom = 6;
                container.Add(line);
            }

            // InfoBox
            var infoBox = field.Field.GetCustomAttribute<BillInfoBoxAttribute>();
            if (infoBox != null)
            {
                Color bgColor = infoBox.Type switch
                {
                    InfoType.Warning => new Color(0.9f, 0.7f, 0.1f, 0.15f),
                    InfoType.Error => new Color(0.9f, 0.2f, 0.2f, 0.15f),
                    _ => new Color(0.2f, 0.4f, 0.8f, 0.15f)
                };
                var box = new VisualElement();
                box.style.backgroundColor = bgColor;
                box.style.borderTopLeftRadius = 4;
                box.style.borderTopRightRadius = 4;
                box.style.borderBottomLeftRadius = 4;
                box.style.borderBottomRightRadius = 4;
                box.style.paddingTop = 6;
                box.style.paddingBottom = 6;
                box.style.paddingLeft = 8;
                box.style.paddingRight = 8;
                box.style.marginBottom = 4;
                box.Add(new Label(infoBox.Message) { style = { fontSize = 11 } });
                container.Add(box);
            }

            // Create field UI based on type and attributes
            var fieldElement = CreateFieldElement(field);
            if (fieldElement != null) container.Add(fieldElement);

            return container;
        }

        private VisualElement CreateFieldElement(WindowField field)
        {
            var type = field.Field.FieldType;
            var label = field.DisplayName;

            // Slider
            var sliderAttr = field.Field.GetCustomAttribute<BillSliderAttribute>();
            if (sliderAttr != null)
            {
                if (type == typeof(int))
                {
                    var s = new SliderInt(label, (int)sliderAttr.MinValue, (int)sliderAttr.MaxValue);
                    s.showInputField = true;
                    s.SetValueWithoutNotify((int)field.Field.GetValue(this));
                    s.RegisterValueChangedCallback(e => { field.Field.SetValue(this, e.newValue); SaveState(); });
                    return s;
                }
                else
                {
                    var s = new Slider(label, sliderAttr.MinValue, sliderAttr.MaxValue);
                    s.showInputField = true;
                    s.SetValueWithoutNotify(Convert.ToSingle(field.Field.GetValue(this)));
                    s.RegisterValueChangedCallback(e => { field.Field.SetValue(this, e.newValue); SaveState(); });
                    return s;
                }
            }

            // Enum toggle buttons
            if (field.Field.GetCustomAttribute<BillEnumToggleButtonsAttribute>() != null && type.IsEnum)
            {
                var container = new VisualElement();
                container.style.flexDirection = FlexDirection.Row;
                container.style.marginBottom = 2;
                var lbl = new Label(label) { style = { width = 120 } };
                container.Add(lbl);
                var names = Enum.GetNames(type);
                for (int i = 0; i < names.Length; i++)
                {
                    int ci = i;
                    var btn = new Button(() =>
                    {
                        field.Field.SetValue(this, Enum.GetValues(type).GetValue(ci));
                        SaveState();
                        RebuildUI();
                    });
                    btn.text = ObjectNames.NicifyVariableName(names[i]);
                    btn.style.flexGrow = 1;
                    var currentIdx = Array.IndexOf(Enum.GetValues(type), field.Field.GetValue(this));
                    btn.style.backgroundColor = currentIdx == i
                        ? new Color(0.24f, 0.49f, 0.91f, 0.6f)
                        : new Color(0.3f, 0.3f, 0.3f, 0.3f);
                    container.Add(btn);
                }
                return container;
            }

            // ReadOnly
            if (field.Field.GetCustomAttribute<BillReadOnlyAttribute>() != null)
            {
                var lbl = new Label($"{label}: {field.Field.GetValue(this)}");
                lbl.style.color = new Color(0.6f, 0.6f, 0.6f);
                lbl.schedule.Execute(() => lbl.text = $"{label}: {field.Field.GetValue(this)}").Every(200);
                return lbl;
            }

            // Default fields by type
            if (type == typeof(string))
            {
                var f = new TextField(label);
                f.SetValueWithoutNotify((string)field.Field.GetValue(this) ?? "");
                f.RegisterValueChangedCallback(e => { field.Field.SetValue(this, e.newValue); SaveState(); });
                return f;
            }
            if (type == typeof(int))
            {
                var f = new IntegerField(label);
                f.SetValueWithoutNotify((int)field.Field.GetValue(this));
                f.RegisterValueChangedCallback(e => { field.Field.SetValue(this, e.newValue); SaveState(); });
                return f;
            }
            if (type == typeof(float))
            {
                var f = new FloatField(label);
                f.SetValueWithoutNotify((float)field.Field.GetValue(this));
                f.RegisterValueChangedCallback(e => { field.Field.SetValue(this, e.newValue); SaveState(); });
                return f;
            }
            if (type == typeof(bool))
            {
                var f = new Toggle(label);
                f.SetValueWithoutNotify((bool)field.Field.GetValue(this));
                f.RegisterValueChangedCallback(e => { field.Field.SetValue(this, e.newValue); SaveState(); });
                return f;
            }
            if (type == typeof(Color))
            {
                var f = new ColorField(label);
                f.SetValueWithoutNotify((Color)field.Field.GetValue(this));
                f.RegisterValueChangedCallback(e => { field.Field.SetValue(this, e.newValue); SaveState(); });
                return f;
            }
            if (type == typeof(Vector2))
            {
                var f = new Vector2Field(label);
                f.SetValueWithoutNotify((Vector2)field.Field.GetValue(this));
                f.RegisterValueChangedCallback(e => { field.Field.SetValue(this, e.newValue); SaveState(); });
                return f;
            }
            if (type == typeof(Vector3))
            {
                var f = new Vector3Field(label);
                f.SetValueWithoutNotify((Vector3)field.Field.GetValue(this));
                f.RegisterValueChangedCallback(e => { field.Field.SetValue(this, e.newValue); SaveState(); });
                return f;
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var f = new ObjectField(label) { objectType = type };
                f.SetValueWithoutNotify((UnityEngine.Object)field.Field.GetValue(this));
                f.RegisterValueChangedCallback(e => { field.Field.SetValue(this, e.newValue); SaveState(); });
                return f;
            }

            // Fallback label
            var fallback = new Label($"{label}: {field.Field.GetValue(this)} ({type.Name})");
            fallback.style.color = new Color(0.5f, 0.5f, 0.5f);
            return fallback;
        }

        private VisualElement DrawWindowGroup(string groupPath, List<WindowField> fields)
        {
            var firstField = fields.FirstOrDefault();
            if (firstField == null) return null;

            var groupAttr = firstField.GroupAttributes.FirstOrDefault();
            if (groupAttr == null) return null;

            switch (groupAttr)
            {
                case BillHorizontalGroupAttribute:
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.marginBottom = 4;
                    foreach (var f in fields)
                    {
                        var el = DrawWindowField(f);
                        if (el == null) continue;
                        var hAttr = f.GroupAttributes.OfType<BillHorizontalGroupAttribute>().FirstOrDefault();
                        if (hAttr?.Width > 0) el.style.width = Length.Percent(hAttr.Width * 100f);
                        else el.style.flexGrow = 1;
                        el.style.marginRight = 4;
                        row.Add(el);
                    }
                    return row;

                case BillVerticalGroupAttribute va:
                    var col = new VisualElement();
                    if (va.Width > 0) col.style.width = Length.Percent(va.Width * 100f);
                    foreach (var f in fields)
                    {
                        var el = DrawWindowField(f);
                        if (el != null) col.Add(el);
                    }
                    return col;

                case BillBoxGroupAttribute box:
                    var boxEl = new VisualElement();
                    boxEl.style.borderTopWidth = boxEl.style.borderBottomWidth =
                        boxEl.style.borderLeftWidth = boxEl.style.borderRightWidth = 1;
                    boxEl.style.borderTopColor = boxEl.style.borderBottomColor =
                        boxEl.style.borderLeftColor = boxEl.style.borderRightColor =
                            new Color(0.3f, 0.3f, 0.3f, 0.4f);
                    boxEl.style.borderTopLeftRadius = boxEl.style.borderTopRightRadius =
                        boxEl.style.borderBottomLeftRadius = boxEl.style.borderBottomRightRadius = 6;
                    boxEl.style.paddingTop = boxEl.style.paddingBottom =
                        boxEl.style.paddingLeft = boxEl.style.paddingRight = 8;
                    boxEl.style.marginTop = boxEl.style.marginBottom = 4;

                    if (box.ShowLabel && !string.IsNullOrEmpty(box.GroupPath))
                    {
                        var h = new Label(ObjectNames.NicifyVariableName(box.GroupPath));
                        h.style.unityFontStyleAndWeight = FontStyle.Bold;
                        h.style.marginBottom = 6;
                        boxEl.Add(h);
                    }
                    foreach (var f in fields)
                    {
                        var el = DrawWindowField(f);
                        if (el != null) boxEl.Add(el);
                    }
                    return boxEl;

                case BillFoldoutGroupAttribute foldout:
                    var fold = new Foldout();
                    fold.text = ObjectNames.NicifyVariableName(
                        foldout.GroupPath.Contains("/")
                            ? foldout.GroupPath.Split('/').Last()
                            : foldout.GroupPath);
                    fold.value = foldout.Expanded;
                    foreach (var f in fields)
                    {
                        var el = DrawWindowField(f);
                        if (el != null) fold.Add(el);
                    }
                    return fold;

                default:
                    var def = new VisualElement();
                    foreach (var f in fields)
                    {
                        var el = DrawWindowField(f);
                        if (el != null) def.Add(el);
                    }
                    return def;
            }
        }

        private void DrawWindowButtons(VisualElement root)
        {
            if (_tree.Methods.Count == 0) return;

            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            separator.style.marginTop = 8;
            separator.style.marginBottom = 8;
            root.Add(separator);

            var grouped = new Dictionary<string, List<WindowMethod>>();
            var ungrouped = new List<WindowMethod>();

            foreach (var m in _tree.Methods)
            {
                var bg = m.Method.GetCustomAttribute<BillButtonGroupAttribute>();
                if (bg != null)
                {
                    if (!grouped.ContainsKey(bg.GroupName)) grouped[bg.GroupName] = new();
                    grouped[bg.GroupName].Add(m);
                }
                else ungrouped.Add(m);
            }

            foreach (var group in grouped)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom = 2;
                foreach (var m in group.Value)
                {
                    var btn = CreateWindowButton(m);
                    btn.style.flexGrow = 1;
                    btn.style.marginRight = 2;
                    row.Add(btn);
                }
                root.Add(row);
            }

            foreach (var m in ungrouped)
                root.Add(CreateWindowButton(m));
        }

        private Button CreateWindowButton(WindowMethod wm)
        {
            var label = wm.ButtonAttr.Label ?? ObjectNames.NicifyVariableName(wm.Method.Name);
            var btn = new Button(() =>
            {
                var result = wm.Method.Invoke(this, null);
                var showResult = wm.Method.GetCustomAttribute<BillShowResultAsAttribute>();
                if (showResult != null && result != null)
                    Debug.Log(string.Format(showResult.Format, result));
                RebuildUI();
            });
            btn.text = label;
            btn.style.height = wm.ButtonAttr.Size switch
            {
                ButtonSize.Small => 22,
                ButtonSize.Large => 36,
                _ => 28
            };
            btn.style.marginTop = 2;
            btn.style.marginBottom = 2;
            return btn;
        }

        // ═══════════════════════════════════════════════════════
        // State persistence (across domain reload)
        // ═══════════════════════════════════════════════════════

        private void SaveState()
        {
            try
            {
                var state = new Dictionary<string, string>();
                foreach (var field in _tree.Fields)
                {
                    var val = field.Field.GetValue(this);
                    if (val != null) state[field.Name] = JsonUtility.ToJson(new Wrapper { json = val.ToString() });
                }
                _serializedWindowState = JsonUtility.ToJson(new StateWrapper { pairs = state.Select(
                    kv => new KV { k = kv.Key, v = kv.Value }).ToList() });
            }
            catch { }
        }

        private void RestoreState()
        {
            if (string.IsNullOrEmpty(_serializedWindowState)) return;

            try
            {
                var stateWrapper = JsonUtility.FromJson<StateWrapper>(_serializedWindowState);
                if (stateWrapper?.pairs == null) return;

                var fieldMap = new Dictionary<string, WindowField>();
                foreach (var f in _tree.Fields) fieldMap[f.Name] = f;

                foreach (var kv in stateWrapper.pairs)
                {
                    if (!fieldMap.TryGetValue(kv.k, out var wf)) continue;

                    var wrapper = JsonUtility.FromJson<Wrapper>(kv.v);
                    if (wrapper?.json == null) continue;

                    var type = wf.Field.FieldType;
                    object parsed = null;
                    if (type == typeof(string)) parsed = wrapper.json;
                    else if (type == typeof(int) && int.TryParse(wrapper.json, out int iv)) parsed = iv;
                    else if (type == typeof(float) && float.TryParse(wrapper.json, out float fv)) parsed = fv;
                    else if (type == typeof(bool) && bool.TryParse(wrapper.json, out bool bv)) parsed = bv;
                    else if (type.IsEnum)
                    {
                        try { parsed = Enum.Parse(type, wrapper.json); } catch { }
                    }

                    if (parsed != null)
                        wf.Field.SetValue(this, parsed);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BillEditorWindow] RestoreState failed: {e.Message}");
            }
        }

        [Serializable] private class Wrapper { public string json; }
        [Serializable] private class StateWrapper { public List<KV> pairs = new(); }
        [Serializable] private class KV { public string k; public string v; }

        // ═══════════════════════════════════════════════════════
        // Window property tree (scan fields + methods)
        // ═══════════════════════════════════════════════════════

        public class WindowField
        {
            public FieldInfo Field;
            public string Name;
            public string DisplayName;
            public int Order;
            public string ShowIfCondition;
            public List<BillGroupAttribute> GroupAttributes = new();
        }

        public class WindowMethod
        {
            public MethodInfo Method;
            public BillButtonAttribute ButtonAttr;
        }

        private class WindowPropertyTree
        {
            public List<WindowField> Fields = new();
            public List<WindowMethod> Methods = new();
            public Dictionary<string, List<WindowField>> Groups = new();

            public WindowPropertyTree(BillEditorWindow window)
            {
                var type = window.GetType();
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

                foreach (var field in type.GetFields(flags))
                {
                    if (field.GetCustomAttribute<HideInInspector>() != null) continue;
                    if (field.GetCustomAttribute<SerializeField>() != null &&
                        field.GetCustomAttribute<HideInInspector>() != null) continue;

                    bool hasBillAttr = field.GetCustomAttributes(typeof(BillAttribute), true).Length > 0;
                    if (!field.IsPublic && !hasBillAttr) continue;

                    var wf = new WindowField
                    {
                        Field = field,
                        Name = field.Name,
                        DisplayName = ObjectNames.NicifyVariableName(field.Name),
                        Order = field.GetCustomAttribute<BillPropertyOrderAttribute>()?.PropertyOrder ?? 0
                    };

                    var labelAttr = field.GetCustomAttribute<BillLabelTextAttribute>();
                    if (labelAttr != null) wf.DisplayName = labelAttr.Text;

                    var showIf = field.GetCustomAttribute<BillShowIfAttribute>();
                    if (showIf != null) wf.ShowIfCondition = showIf.Condition;

                    foreach (var ga in field.GetCustomAttributes<BillGroupAttribute>(true))
                    {
                        wf.GroupAttributes.Add(ga);
                        if (!Groups.ContainsKey(ga.GroupPath)) Groups[ga.GroupPath] = new();
                        Groups[ga.GroupPath].Add(wf);
                    }

                    Fields.Add(wf);
                }

                Fields.Sort((a, b) => a.Order.CompareTo(b.Order));

                foreach (var method in type.GetMethods(flags))
                {
                    var btnAttr = method.GetCustomAttribute<BillButtonAttribute>();
                    if (btnAttr != null)
                        Methods.Add(new WindowMethod { Method = method, ButtonAttr = btnAttr });
                }
            }
        }
    }
}
