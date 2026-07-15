using System;

namespace BillInspector
{
    /// <summary>
    /// Turns a method into a clickable button in the inspector.
    /// Methods with parameters get auto-generated input fields.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class BillButtonAttribute : BillAttribute
    {
        public string Label { get; }
        public ButtonSize Size { get; set; } = ButtonSize.Medium;
        public string Icon { get; set; }
        public string EnableIf { get; set; }

        public BillButtonAttribute(string label = null)
        {
            Label = label;
        }

        public BillButtonAttribute(string label, ButtonSize size)
        {
            Label = label;
            Size = size;
        }
    }
}
