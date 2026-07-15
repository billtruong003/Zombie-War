using System;

namespace BillInspector
{
    /// <summary>
    /// Displays a large title/header above the field.
    /// Use "$fieldName" for dynamic title from a field value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = true)]
    public class BillTitleAttribute : BillAttribute
    {
        public string Title { get; }
        public string Subtitle { get; }
        public bool Bold { get; set; } = true;
        public bool HorizontalLine { get; set; } = true;

        public BillTitleAttribute(string title, string subtitle = null)
        {
            Title = title;
            Subtitle = subtitle;
        }
    }
}
