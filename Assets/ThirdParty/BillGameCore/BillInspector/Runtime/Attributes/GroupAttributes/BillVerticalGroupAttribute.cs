using System;

namespace BillInspector
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public class BillVerticalGroupAttribute : BillGroupAttribute
    {
        public float Width { get; }

        public BillVerticalGroupAttribute(string groupPath, float width = 0f) : base(groupPath)
        {
            Width = width;
        }
    }
}
