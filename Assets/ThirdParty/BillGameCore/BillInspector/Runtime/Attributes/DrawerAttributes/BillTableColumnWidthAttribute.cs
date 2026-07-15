using System;

namespace BillInspector
{
    /// <summary>
    /// Sets the column width when a field is inside a [BillTableList].
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillTableColumnWidthAttribute : BillAttribute
    {
        public int Width { get; }
        public BillTableColumnWidthAttribute(int width) { Width = width; }
    }
}
