using UnityEditor;
using UnityEngine;

namespace BillInspector.ShaderEditor
{
    /// <summary>
    /// Draws a gradient editor for a texture property.
    /// Generates a gradient texture and assigns it to the property.
    /// Usage: [BillGradient] _RampTex("Lighting Ramp", 2D) = "white" {}
    /// </summary>
    public class BillGradientDrawer : MaterialPropertyDrawer
    {
        private Gradient _gradient;
        private Texture2D _gradientTex;

        public override void OnGUI(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor)
        {
            if (_gradient == null)
                _gradient = new Gradient();

            EditorGUI.BeginChangeCheck();

            var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            var gradientRect = new Rect(position.x + EditorGUIUtility.labelWidth + 2,
                position.y, position.width - EditorGUIUtility.labelWidth - 2, position.height);

            EditorGUI.LabelField(labelRect, label);
            _gradient = EditorGUI.GradientField(gradientRect, _gradient);

            if (EditorGUI.EndChangeCheck())
            {
                _gradientTex = GenerateGradientTexture(_gradient, 256, 4);
                prop.textureValue = _gradientTex;
            }

            // Draw small preview below
            if (prop.textureValue != null)
            {
                var previewRect = new Rect(position.x + EditorGUIUtility.labelWidth + 2,
                    position.y + position.height + 2,
                    position.width - EditorGUIUtility.labelWidth - 2, 8);
                EditorGUI.DrawPreviewTexture(previewRect, prop.textureValue);
            }
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return prop.textureValue != null ? 30 : 18;
        }

        private static Texture2D GenerateGradientTexture(Gradient gradient, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int x = 0; x < width; x++)
            {
                var color = gradient.Evaluate((float)x / (width - 1));
                for (int y = 0; y < height; y++)
                    tex.SetPixel(x, y, color);
            }

            tex.Apply();
            return tex;
        }
    }
}
