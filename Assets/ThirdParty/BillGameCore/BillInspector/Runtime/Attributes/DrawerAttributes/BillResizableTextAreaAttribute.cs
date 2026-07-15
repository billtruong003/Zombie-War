using System;

namespace BillInspector
{
    /// <summary>
    /// A text area that expands to show all content. No manual scrolling.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillResizableTextAreaAttribute : BillDrawerAttribute
    {
        public int MinLines { get; }
        public int MaxLines { get; }

        public BillResizableTextAreaAttribute(int minLines = 3, int maxLines = 15)
        {
            MinLines = minLines;
            MaxLines = maxLines;
        }
    }
}
