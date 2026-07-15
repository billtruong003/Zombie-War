using System;

namespace BillInspector
{
    /// <summary>
    /// Custom validation via method name or @expression.
    /// Method must return bool. Expression must evaluate to bool.
    /// </summary>
    /// <example>
    /// [BillValidateInput("IsValidHP", "HP must be 0-100")]
    /// [BillValidateInput("@value >= 0 &amp;&amp; value &lt;= maxValue", "Out of range")]
    /// </example>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillValidateInputAttribute : BillMetaAttribute
    {
        public string Condition { get; }
        public string Message { get; }
        public InfoType MessageType { get; set; } = InfoType.Error;

        public BillValidateInputAttribute(string condition, string message = "Invalid value")
        {
            Condition = condition;
            Message = message;
        }
    }
}
