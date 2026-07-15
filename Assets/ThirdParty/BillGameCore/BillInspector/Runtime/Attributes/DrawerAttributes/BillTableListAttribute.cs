using System;

namespace BillInspector
{
    /// <summary>
    /// Renders a List/Array as a table with column headers.
    /// Each serializable field in the element becomes a column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillTableListAttribute : BillDrawerAttribute
    {
        public bool ShowPaging { get; set; }
        public int PageSize { get; set; } = 20;
        public bool IsReadOnly { get; set; }
        public bool ShowIndexLabels { get; set; }
        public int MinRowCount { get; set; }
    }
}
