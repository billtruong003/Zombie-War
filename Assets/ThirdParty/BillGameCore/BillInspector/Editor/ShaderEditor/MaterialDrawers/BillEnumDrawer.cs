using UnityEditor;
using UnityEngine;

namespace BillInspector.ShaderEditor
{
    /// <summary>
    /// Enhanced enum drawer for shader properties.
    /// Usage: [BillEnum(Opaque,0,Transparent,1)] _Mode("Render Mode", Float) = 0
    /// </summary>
    public class BillEnumDrawer : MaterialPropertyDrawer
    {
        private readonly string[] _names;
        private readonly float[] _values;

        public BillEnumDrawer(params object[] args)
        {
            int count = args.Length / 2;
            _names = new string[count];
            _values = new float[count];

            for (int i = 0; i < count; i++)
            {
                _names[i] = args[i * 2].ToString();
                _values[i] = float.Parse(args[i * 2 + 1].ToString());
            }
        }

        public override void OnGUI(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor)
        {
            EditorGUI.BeginChangeCheck();

            int current = 0;
            for (int i = 0; i < _values.Length; i++)
            {
                if (Mathf.Abs(prop.floatValue - _values[i]) < 0.01f)
                {
                    current = i;
                    break;
                }
            }

            int selected = EditorGUI.Popup(position, label.text, current, _names);

            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = _values[selected];
            }
        }
    }
}
