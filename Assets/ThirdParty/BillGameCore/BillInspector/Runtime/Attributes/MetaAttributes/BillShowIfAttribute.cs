using System;

namespace BillInspector
{
    /// <summary>
    /// Shows the field only when the condition is true.
    /// Condition can be: field name, method name, enum comparison, or @expression.
    /// </summary>
    /// <example>
    /// [BillShowIf("isActive")]
    /// [BillShowIf("weaponType", WeaponType.Melee)]
    /// [BillShowIf("@level >= 5 &amp;&amp; isReady")]
    /// [BillShowIf("CanShowMethod")]
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class BillShowIfAttribute : BillConditionAttribute
    {
        public ConditionOperator Operator { get; set; } = ConditionOperator.And;

        public BillShowIfAttribute(string condition) : base(condition) { }
        public BillShowIfAttribute(string condition, object compareValue) : base(condition, compareValue) { }
    }
}
