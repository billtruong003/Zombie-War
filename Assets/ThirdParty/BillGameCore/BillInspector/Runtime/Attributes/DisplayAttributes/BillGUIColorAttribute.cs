using System;

namespace BillInspector
{
    /// <summary>
    /// Tints the GUI color of this field. Supports @expression for dynamic color.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class BillGUIColorAttribute : BillAttribute
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }
        public string Expression { get; }

        public BillGUIColorAttribute(float r, float g, float b, float a = 1f)
        {
            R = r; G = g; B = b; A = a;
        }

        public BillGUIColorAttribute(string expression)
        {
            Expression = expression;
        }
    }
}
