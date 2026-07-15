using System;

namespace BillInspector
{
    /// <summary>
    /// Groups fields inside a bordered box with a label.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public class BillBoxGroupAttribute : BillGroupAttribute
    {
        public bool ShowLabel { get; set; } = true;
        public bool CenterLabel { get; set; }

        public BillBoxGroupAttribute(string groupPath = "") : base(groupPath) { }
    }
}
