using System;

namespace BillInspector
{
    /// <summary>
    /// Disables (grays out) the field when the condition is true. Inverse of BillEnableIf.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class BillDisableIfAttribute : BillConditionAttribute
    {
        public ConditionOperator Operator { get; set; } = ConditionOperator.And;

        public BillDisableIfAttribute(string condition) : base(condition) { }
        public BillDisableIfAttribute(string condition, object compareValue) : base(condition, compareValue) { }
    }
}
