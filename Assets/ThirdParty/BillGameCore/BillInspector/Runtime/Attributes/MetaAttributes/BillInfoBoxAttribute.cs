using System;

namespace BillInspector
{
    /// <summary>
    /// Displays an info/warning/error box above the field.
    /// Can be conditionally shown via VisibleIf.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
    public class BillInfoBoxAttribute : BillMetaAttribute
    {
        public string Message { get; }
        public InfoType Type { get; }
        public string VisibleIf { get; set; }

        public BillInfoBoxAttribute(string message, InfoType type = InfoType.Info)
        {
            Message = message;
            Type = type;
        }
    }
}
