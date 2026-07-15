using System;

namespace BillInspector
{
    /// <summary>
    /// Draws a ScriptableObject or Component's fields inline (expanded) in the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillInlineEditorAttribute : BillDrawerAttribute
    {
        public bool Expanded { get; set; } = true;
        public bool ShowHeader { get; set; } = true;
    }
}
