using System;

namespace BillInspector
{
    /// <summary>
    /// Marks a method as a self-validation method.
    /// The method should accept a ValidationResultList and add errors/warnings.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class BillValidateAttribute : Attribute { }

    /// <summary>
    /// Validates a file path exists on disk.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillFileExistsAttribute : BillMetaAttribute
    {
        public string Extension { get; set; }
        public string Message { get; set; } = "File does not exist";
    }

    /// <summary>
    /// Validates a value is within a range.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillRangeValidationAttribute : BillMetaAttribute
    {
        public float Min { get; }
        public float Max { get; }
        public string Message { get; set; }

        public BillRangeValidationAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }

    /// <summary>
    /// Validates that a collection has items (not empty).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillNotEmptyAttribute : BillMetaAttribute
    {
        public string Message { get; set; } = "Collection must not be empty";
    }
}
