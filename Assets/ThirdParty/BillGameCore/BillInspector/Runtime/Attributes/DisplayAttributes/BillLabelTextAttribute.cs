using System;

namespace BillInspector
{
    /// <summary>
    /// Overrides the display label of a field.
    /// Supports @expression for dynamic labels.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class BillLabelTextAttribute : BillAttribute
    {
        public string Text { get; }

        public BillLabelTextAttribute(string text)
        {
            Text = text;
        }
    }
}
