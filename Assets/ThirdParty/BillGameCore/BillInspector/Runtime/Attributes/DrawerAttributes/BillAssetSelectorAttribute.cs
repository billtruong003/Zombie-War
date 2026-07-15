using System;

namespace BillInspector
{
    /// <summary>
    /// Enhanced object picker with type filter and path filter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillAssetSelectorAttribute : BillDrawerAttribute
    {
        public string Filter { get; set; }
        public string[] Paths { get; set; }
        public bool ShowPreview { get; set; } = true;
    }
}
