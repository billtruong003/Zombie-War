using System;

namespace BillInspector
{
    /// <summary>
    /// Validates that the field is not empty/null/default.
    /// Shows an error message in the inspector when invalid.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillRequiredAttribute : BillMetaAttribute
    {
        public string Message { get; }

        public BillRequiredAttribute(string message = null)
        {
            Message = message ?? "This field is required";
        }
    }
}
