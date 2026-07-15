using System;

namespace BillInspector
{
    /// <summary>
    /// Shows a non-serialized field, property, or method result in the inspector.
    /// Read-only by default for non-serialized members.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class BillShowInInspectorAttribute : BillAttribute { }
}
