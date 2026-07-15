using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using BillInspector;

namespace BillInspector.Editor
{
    /// <summary>
    /// Editor window showing validation results for the scene and project.
    /// </summary>
    public class BillValidationWindow : EditorWindow
    {
        private ValidationResultList _results;
        private Vector2 _scrollPos;
        private bool _showInfo = true;
        private bool _showWarnings = true;
        private bool _showErrors = true;
        private string _searchFilter = "";

        [MenuItem("Tools/BillInspector/Validation Window")]
        public static void Open()
        {
            var window = GetWindow<BillValidationWindow>("Bill Validator");
            window.minSize = new Vector2(450, 300);
        }

        private void OnGUI()
        {
            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Validate Scene", EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                _results = BillValidator.ValidateScene();
            }

            if (GUILayout.Button("Validate Assets", EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                _results = BillValidator.ValidateProjectAssets();
            }

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _results = null;
            }

            GUILayout.FlexibleSpace();

            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField,
                GUILayout.Width(180));

            EditorGUILayout.EndHorizontal();

            // Filter toggles
            EditorGUILayout.BeginHorizontal();
            var errorCount = _results?.Entries.Count(e => e.Severity == ValidationSeverity.Error) ?? 0;
            var warnCount = _results?.Entries.Count(e => e.Severity == ValidationSeverity.Warning) ?? 0;
            var infoCount = _results?.Entries.Count(e => e.Severity == ValidationSeverity.Info) ?? 0;

            _showErrors = GUILayout.Toggle(_showErrors, $" Errors ({errorCount})", EditorStyles.toolbarButton);
            _showWarnings = GUILayout.Toggle(_showWarnings, $" Warnings ({warnCount})", EditorStyles.toolbarButton);
            _showInfo = GUILayout.Toggle(_showInfo, $" Info ({infoCount})", EditorStyles.toolbarButton);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Results
            if (_results == null)
            {
                EditorGUILayout.HelpBox("Click 'Validate Scene' or 'Validate Assets' to run validation.",
                    MessageType.Info);
                return;
            }

            if (_results.Entries.Count == 0)
            {
                EditorGUILayout.HelpBox("All validations passed!", MessageType.Info);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var entry in _results.Entries)
            {
                // Filter by severity
                if (entry.Severity == ValidationSeverity.Error && !_showErrors) continue;
                if (entry.Severity == ValidationSeverity.Warning && !_showWarnings) continue;
                if (entry.Severity == ValidationSeverity.Info && !_showInfo) continue;

                // Filter by search
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    var lower = _searchFilter.ToLower();
                    if (!entry.Message.ToLower().Contains(lower) &&
                        !(entry.ObjectName?.ToLower().Contains(lower) ?? false) &&
                        !(entry.FieldName?.ToLower().Contains(lower) ?? false))
                        continue;
                }

                DrawEntry(entry);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry(ValidationEntry entry)
        {
            var bgColor = entry.Severity switch
            {
                ValidationSeverity.Error => new Color(0.8f, 0.2f, 0.2f, 0.1f),
                ValidationSeverity.Warning => new Color(0.8f, 0.6f, 0.1f, 0.1f),
                _ => new Color(0.2f, 0.5f, 0.8f, 0.1f)
            };

            var rect = EditorGUILayout.GetControlRect(false, 36);
            EditorGUI.DrawRect(rect, bgColor);

            // Icon
            var iconRect = new Rect(rect.x + 4, rect.y + 2, 16, 16);
            var icon = entry.Severity switch
            {
                ValidationSeverity.Error => EditorGUIUtility.IconContent("console.erroricon.sml"),
                ValidationSeverity.Warning => EditorGUIUtility.IconContent("console.warnicon.sml"),
                _ => EditorGUIUtility.IconContent("console.infoicon.sml")
            };
            GUI.Label(iconRect, icon);

            // Message
            var msgRect = new Rect(rect.x + 24, rect.y + 2, rect.width - 80, 16);
            EditorGUI.LabelField(msgRect, entry.Message, EditorStyles.boldLabel);

            // Object + Field
            var detailRect = new Rect(rect.x + 24, rect.y + 18, rect.width - 80, 14);
            var detail = $"{entry.ObjectName ?? "?"}.{entry.FieldName ?? "?"}";
            EditorGUI.LabelField(detailRect, detail, EditorStyles.miniLabel);

            // Ping button
            if (entry.Target != null)
            {
                var btnRect = new Rect(rect.x + rect.width - 52, rect.y + 8, 48, 20);
                if (GUI.Button(btnRect, "Select", EditorStyles.miniButton))
                {
                    Selection.activeObject = entry.Target;
                    EditorGUIUtility.PingObject(entry.Target);
                }
            }
        }
    }
}
