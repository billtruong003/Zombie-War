using System;

namespace BillInspector
{
    /// <summary>
    /// Groups fields that are enabled/disabled by a bool field.
    /// The bool field acts as a toggle checkbox for the entire group.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public class BillToggleGroupAttribute : BillGroupAttribute
    {
        public string Label { get; }

        public BillToggleGroupAttribute(string toggleFieldPath, string label = null) : base(toggleFieldPath)
        {
            Label = label;
        }
    }
}
