using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BillInspector.ShaderEditor
{
    /// <summary>
    /// Advanced ShaderGUI that auto-generates URP-quality material inspectors.
    ///
    /// Usage in shader: CustomEditor "BillShaderGUI"
    /// Or subclass for full control: CustomEditor "MyShaderEditor"
    ///
    /// Naming conventions:
    ///   _H_SectionName  → Foldable header section
    ///   [Toggle] _Prop  → Toggle + keyword (_PROP_ON)
    ///   _BaseTex + _BaseColor consecutive → Single-line texture with color
    ///
    /// Override OnDrawProperties() for full custom control.
    /// </summary>
    public class BillShaderGUI : UnityEditor.ShaderGUI
    {
        // Foldout states persist per material
        private readonly Dictionary<string, bool> _foldouts = new();
        private MaterialEditor _editor;
        private MaterialProperty[] _props;
        private Material _targetMat;

        // Track which properties have been drawn (skip in auto-draw)
        private readonly HashSet<string> _drawnProperties = new();

        // Section tracking for toggle-dependent properties
        private bool _currentToggleState = true;
        private int _indentLevel;

        // ═══════════════════════════════════════════════════════
        // Main entry point
        // ═══════════════════════════════════════════════════════

        public sealed override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _editor = materialEditor;
            _props = properties;
            _targetMat = materialEditor.target as Material;
            _drawnProperties.Clear();
            _currentToggleState = true;
            _indentLevel = 0;

            EditorGUILayout.Space(2);

            OnDrawProperties(materialEditor, properties);

            EditorGUILayout.Space(8);
            DrawAdvancedOptions(materialEditor);
        }

        /// <summary>
        /// Override this for full custom control. Default implementation uses naming conventions.
        /// </summary>
        protected virtual void OnDrawProperties(MaterialEditor editor, MaterialProperty[] properties)
        {
            AutoDrawProperties(properties);
        }

        // ═══════════════════════════════════════════════════════
        // Auto-draw using naming conventions
        // ═══════════════════════════════════════════════════════

        private void AutoDrawProperties(MaterialProperty[] properties)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                var prop = properties[i];
                if (_drawnProperties.Contains(prop.name)) continue;
                if (prop.propertyFlags.HasFlag(UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector)) continue;

                // ── Header section ──
                if (prop.name.StartsWith("_H_"))
                {
                    string sectionName = prop.displayName;
                    if (string.IsNullOrEmpty(sectionName))
                        sectionName = prop.name.Substring(3).Replace("_", " ");

                    // End previous section indent
                    if (_indentLevel > 0)
                    {
                        _indentLevel = 0;
                        EditorGUI.indentLevel--;
                    }
                    _currentToggleState = true;

                    DrawHeader(sectionName);
                    _drawnProperties.Add(prop.name);
                    continue;
                }

                // Skip if current toggle is off
                if (!_currentToggleState && _indentLevel > 0)
                {
                    _drawnProperties.Add(prop.name);
                    continue;
                }

                // ── Toggle property ──
                if (IsToggle(prop))
                {
                    if (_indentLevel > 0)
                    {
                        _indentLevel = 0;
                        EditorGUI.indentLevel--;
                    }

                    _currentToggleState = DrawToggle(prop.name, prop.displayName);
                    if (_currentToggleState)
                    {
                        _indentLevel = 1;
                        EditorGUI.indentLevel++;
                    }
                    else
                    {
                        _indentLevel = 1;
                    }
                    _drawnProperties.Add(prop.name);
                    continue;
                }

                // ── Texture + next-color combo (single line) ──
                if (prop.propertyType == UnityEngine.Rendering.ShaderPropertyType.Texture && i + 1 < properties.Length)
                {
                    var next = properties[i + 1];
                    if (next.propertyType == UnityEngine.Rendering.ShaderPropertyType.Color && !next.name.StartsWith("_H_"))
                    {
                        DrawTextureWithColor(prop.name, next.name);
                        _drawnProperties.Add(prop.name);
                        _drawnProperties.Add(next.name);
                        continue;
                    }
                }

                // ── Normal property ──
                DrawProperty(prop);
                _drawnProperties.Add(prop.name);
            }

            // End last indent
            if (_indentLevel > 0)
            {
                EditorGUI.indentLevel--;
                _indentLevel = 0;
            }
        }

        // ═══════════════════════════════════════════════════════
        // Drawing helpers — available to subclasses
        // ═══════════════════════════════════════════════════════

        /// <summary>Draw a foldable section header.</summary>
        protected void DrawHeader(string title)
        {
            if (!_foldouts.ContainsKey(title)) _foldouts[title] = true;

            EditorGUILayout.Space(6);
            var rect = EditorGUILayout.GetControlRect(false, 22);

            // Background
            var bgRect = new Rect(rect.x - 2, rect.y, rect.width + 4, rect.height);
            EditorGUI.DrawRect(bgRect, new Color(0.15f, 0.15f, 0.15f, 0.3f));

            // Foldout arrow + label
            _foldouts[title] = EditorGUI.Foldout(rect, _foldouts[title], title,
                true, EditorStyles.boldLabel);

            // Separator line
            var lineRect = new Rect(rect.x, rect.y + rect.height, rect.width, 1);
            EditorGUI.DrawRect(lineRect, new Color(0.3f, 0.3f, 0.3f, 0.3f));
        }

        /// <summary>Draw a toggle that controls a shader keyword.</summary>
        protected bool DrawToggle(string propertyName, string label = null)
        {
            var prop = FindProperty(propertyName);
            if (prop == null) return true;

            _drawnProperties.Add(propertyName);
            label ??= prop.displayName;

            EditorGUI.BeginChangeCheck();
            bool value = prop.floatValue > 0.5f;
            value = EditorGUILayout.Toggle(label, value);
            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = value ? 1f : 0f;
                SetKeyword(propertyName, value);
            }
            return value;
        }

        /// <summary>Draw texture and color on the same line (URP style).</summary>
        protected void DrawTextureWithColor(string textureProp, string colorProp)
        {
            var tex = FindProperty(textureProp);
            var col = FindProperty(colorProp);
            if (tex == null) return;

            _drawnProperties.Add(textureProp);
            if (col != null) _drawnProperties.Add(colorProp);

            _editor.TexturePropertySingleLine(
                new GUIContent(tex.displayName),
                tex, col);
        }

        /// <summary>Draw a single-line texture property.</summary>
        protected void DrawTextureSingleLine(string propertyName, string label = null)
        {
            var prop = FindProperty(propertyName);
            if (prop == null) return;
            _drawnProperties.Add(propertyName);
            _editor.TexturePropertySingleLine(
                new GUIContent(label ?? prop.displayName), prop);
        }

        /// <summary>Draw a normal map with scale.</summary>
        protected void DrawNormalMap(string textureProp, string scaleProp)
        {
            var tex = FindProperty(textureProp);
            var scale = FindProperty(scaleProp);
            if (tex == null) return;

            _drawnProperties.Add(textureProp);
            if (scale != null) _drawnProperties.Add(scaleProp);

            _editor.TexturePropertySingleLine(
                new GUIContent(tex.displayName), tex,
                tex.textureValue != null ? scale : null);
        }

        /// <summary>Draw a slider property.</summary>
        protected void DrawSlider(string propertyName, string label = null)
        {
            var prop = FindProperty(propertyName);
            if (prop == null) return;
            _drawnProperties.Add(propertyName);
            _editor.ShaderProperty(prop, label ?? prop.displayName);
        }

        /// <summary>Draw a color property.</summary>
        protected void DrawColor(string propertyName, string label = null)
        {
            var prop = FindProperty(propertyName);
            if (prop == null) return;
            _drawnProperties.Add(propertyName);
            _editor.ShaderProperty(prop, label ?? prop.displayName);
        }

        /// <summary>Draw a vector property.</summary>
        protected void DrawVector(string propertyName, string label = null)
        {
            var prop = FindProperty(propertyName);
            if (prop == null) return;
            _drawnProperties.Add(propertyName);
            _editor.ShaderProperty(prop, label ?? prop.displayName);
        }

        /// <summary>Draw an enum popup property.</summary>
        protected void DrawEnum(string propertyName, string label = null, string[] options = null)
        {
            var prop = FindProperty(propertyName);
            if (prop == null) return;
            _drawnProperties.Add(propertyName);

            if (options != null)
            {
                EditorGUI.BeginChangeCheck();
                int val = (int)prop.floatValue;
                val = EditorGUILayout.Popup(label ?? prop.displayName, val, options);
                if (EditorGUI.EndChangeCheck())
                    prop.floatValue = val;
            }
            else
            {
                _editor.ShaderProperty(prop, label ?? prop.displayName);
            }
        }

        /// <summary>Draw a generic property (auto-detect type).</summary>
        protected void DrawProperty(MaterialProperty prop)
        {
            _editor.ShaderProperty(prop, prop.displayName);
        }

        /// <summary>Draw a block with increased indentation.</summary>
        protected void DrawIndented(Action drawContent)
        {
            EditorGUI.indentLevel++;
            drawContent?.Invoke();
            EditorGUI.indentLevel--;
        }

        /// <summary>Draw advanced options: render queue, instancing, double-sided GI.</summary>
        protected void DrawAdvancedOptions(MaterialEditor editor)
        {
            DrawHeader("Advanced Options");
            if (_foldouts.TryGetValue("Advanced Options", out bool open) && open)
            {
                EditorGUI.indentLevel++;
                editor.RenderQueueField();
                editor.EnableInstancingField();
                editor.DoubleSidedGIField();
                EditorGUI.indentLevel--;
            }
        }

        // ═══════════════════════════════════════════════════════
        // Utilities
        // ═══════════════════════════════════════════════════════

        private MaterialProperty FindProperty(string name)
        {
            return _props?.FirstOrDefault(p => p.name == name);
        }

        private bool IsToggle(MaterialProperty prop)
        {
            if (prop.propertyType != UnityEngine.Rendering.ShaderPropertyType.Float &&
                prop.propertyType != UnityEngine.Rendering.ShaderPropertyType.Range)
                return false;

            // Check for [Toggle] attribute on the property
            var attrs = _editor.serializedObject
                .FindProperty("m_SavedProperties.m_Floats")?.Copy();

            // Heuristic: name starts with common toggle patterns
            var lower = prop.name.ToLower();
            if (lower.StartsWith("_use") || lower.StartsWith("_enable") ||
                lower.StartsWith("_has") || lower.StartsWith("_is") ||
                lower.Contains("toggle"))
                return true;

            // Check display name patterns
            if (prop.displayName.StartsWith("Enable") || prop.displayName.StartsWith("Use"))
                return true;

            // Check if shader declares [Toggle]
            return prop.propertyFlags.HasFlag(UnityEngine.Rendering.ShaderPropertyFlags.None) &&
                   prop.propertyType == UnityEngine.Rendering.ShaderPropertyType.Float &&
                   prop.rangeLimits.x == 0 && prop.rangeLimits.y == 0 &&
                   (prop.floatValue == 0f || prop.floatValue == 1f);
        }

        private void SetKeyword(string propertyName, bool enabled)
        {
            if (_targetMat == null) return;
            // Convention: _PropertyName → _PROPERTYNAME_ON
            string keyword = propertyName.ToUpper();
            if (keyword.StartsWith("_")) keyword = keyword.Substring(1);
            keyword += "_ON";

            foreach (var t in _editor.targets)
            {
                var mat = t as Material;
                if (mat == null) continue;
                if (enabled) mat.EnableKeyword(keyword);
                else mat.DisableKeyword(keyword);
            }
        }
    }
}
