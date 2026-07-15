using System;

namespace BillInspector
{
    /// <summary>
    /// Dropdown populated from a method, field, or property that returns IEnumerable.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillDropdownAttribute : BillDrawerAttribute
    {
        public string ValuesSource { get; }

        public BillDropdownAttribute(string valuesSource)
        {
            ValuesSource = valuesSource;
        }
    }
}
