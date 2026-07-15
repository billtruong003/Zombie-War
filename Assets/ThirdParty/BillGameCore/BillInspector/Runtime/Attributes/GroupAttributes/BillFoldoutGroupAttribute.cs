using System;

namespace BillInspector
{
    /// <summary>
    /// Groups fields inside a collapsible foldout section.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public class BillFoldoutGroupAttribute : BillGroupAttribute
    {
        public bool Expanded { get; set; } = true;

        public BillFoldoutGroupAttribute(string groupPath) : base(groupPath) { }
    }
}
