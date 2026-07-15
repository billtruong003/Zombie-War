using System;

namespace BillInspector
{
    /// <summary>
    /// Displays the return value of a button method as a label.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class BillShowResultAsAttribute : BillAttribute
    {
        public string Format { get; }

        public BillShowResultAsAttribute(string format = "{0}")
        {
            Format = format;
        }
    }
}
