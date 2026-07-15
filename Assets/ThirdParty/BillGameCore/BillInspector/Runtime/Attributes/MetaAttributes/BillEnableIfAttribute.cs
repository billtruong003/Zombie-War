using System;

namespace BillInspector
{
    /// <summary>
    /// Enables (interactive) the field only when the condition is true.
    /// Field is still visible but grayed out when disabled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class BillEnableIfAttribute : BillConditionAttribute
    {
        public ConditionOperator Operator { get; set; } = ConditionOperator.And;

        public BillEnableIfAttribute(string condition) : base(condition) { }
        public BillEnableIfAttribute(string condition, object compareValue) : base(condition, compareValue) { }
    }
}
