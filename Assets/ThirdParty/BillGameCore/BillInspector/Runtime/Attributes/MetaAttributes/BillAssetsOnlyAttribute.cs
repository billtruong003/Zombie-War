using System;

namespace BillInspector
{
    /// <summary>
    /// Restricts an Object field to project assets only (no scene objects).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillAssetsOnlyAttribute : BillMetaAttribute { }
}
