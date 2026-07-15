using UnityEditor;
using UnityEngine;

namespace BillInspector.ShaderEditor
{
    /// <summary>
    /// Material property decorator that draws a styled header.
    /// Usage in shader: [BillHeader(Surface Options)]
    /// </summary>
    public class BillHeaderDecorator : MaterialPropertyDrawer
    {
        private readonly string _header;

        public BillHeaderDecorator(string header)
        {
            _header = header;
        }

        public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
        {
            position.y += 6;
            position.height = 22;

            // Background
            EditorGUI.DrawRect(position, new Color(0.15f, 0.15f, 0.15f, 0.3f));

            // Label
            var labelStyle = new GUIStyle(EditorStyles.boldLabel);
            EditorGUI.LabelField(position, _header, labelStyle);

            // Underline
            var lineRect = new Rect(position.x, position.y + position.height, position.width, 1);
            EditorGUI.DrawRect(lineRect, new Color(0.3f, 0.3f, 0.3f, 0.3f));
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return 30f;
        }
    }
}
