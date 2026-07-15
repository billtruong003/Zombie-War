using System;

namespace BillInspector
{
    /// <summary>
    /// Places fields side by side horizontally.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public class BillHorizontalGroupAttribute : BillGroupAttribute
    {
        public float Width { get; }
        public float MarginLeft { get; set; }
        public float MarginRight { get; set; }

        public BillHorizontalGroupAttribute(string groupPath, float width = 0f) : base(groupPath)
        {
            Width = width;
        }
    }
}
