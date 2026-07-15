using System;

namespace BillInspector
{
    /// <summary>
    /// Hides the field when the condition is true. Inverse of BillShowIf.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class BillHideIfAttribute : BillConditionAttribute
    {
        public ConditionOperator Operator { get; set; } = ConditionOperator.And;

        public BillHideIfAttribute(string condition) : base(condition) { }
        public BillHideIfAttribute(string condition, object compareValue) : base(condition, compareValue) { }
    }
}
