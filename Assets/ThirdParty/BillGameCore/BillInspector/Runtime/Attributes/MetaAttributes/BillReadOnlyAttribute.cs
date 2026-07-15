using System;

namespace BillInspector
{
    /// <summary>
    /// Makes the field always read-only (grayed out) in the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class BillReadOnlyAttribute : BillMetaAttribute { }
}
