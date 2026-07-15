using System;

namespace BillInspector
{
    /// <summary>
    /// Displays a suffix label after the field (e.g., "px", "%", "seconds").
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillSuffixAttribute : BillAttribute
    {
        public string Text { get; }

        public BillSuffixAttribute(string text)
        {
            Text = text;
        }
    }
}
