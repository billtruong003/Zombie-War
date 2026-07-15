using System;
using UnityEditor;
using UnityEngine;

namespace BillInspector.ShaderEditor
{
    /// <summary>
    /// Enhanced toggle drawer with automatic keyword management.
    /// Usage in shader: [BillToggle] _PropertyName("Label", Float) = 0
    /// Keyword: _PROPERTYNAME_ON
    /// </summary>
    public class BillToggleDrawer : MaterialPropertyDrawer
    {
        private readonly string _keyword;

        public BillToggleDrawer() { }
        public BillToggleDrawer(string keyword) { _keyword = keyword; }

        public override void OnGUI(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor)
        {
            EditorGUI.BeginChangeCheck();
            bool value = prop.floatValue > 0.5f;
            value = EditorGUI.Toggle(position, label, value);

            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = value ? 1f : 0f;

                string kw = _keyword;
                if (string.IsNullOrEmpty(kw))
                {
                    kw = prop.name.ToUpper();
                    if (kw.StartsWith("_")) kw = kw.Substring(1);
                    kw += "_ON";
                }

                foreach (var t in editor.targets)
                {
                    var mat = t as Material;
                    if (mat == null) continue;
                    if (value) mat.EnableKeyword(kw);
                    else mat.DisableKeyword(kw);
                }
            }
        }
    }
}
