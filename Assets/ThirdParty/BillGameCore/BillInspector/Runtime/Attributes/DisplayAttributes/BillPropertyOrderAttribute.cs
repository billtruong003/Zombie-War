using System;

namespace BillInspector
{
    /// <summary>
    /// Controls the display order of a field in the inspector.
    /// Lower values appear first. Default is 0.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class BillPropertyOrderAttribute : BillAttribute
    {
        public int PropertyOrder { get; }

        public BillPropertyOrderAttribute(int order)
        {
            PropertyOrder = order;
        }
    }
}
