using System;

namespace BillInspector
{
    /// <summary>
    /// Configures list/array drawing behavior.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillListDrawerSettingsAttribute : BillAttribute
    {
        public bool ShowFoldout { get; set; } = true;
        public bool DraggableItems { get; set; } = true;
        public bool ShowItemCount { get; set; } = true;
        public string CustomAddFunction { get; set; }
        public string CustomRemoveFunction { get; set; }
        public int MaxItems { get; set; } = int.MaxValue;
    }
}
