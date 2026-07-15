using System;

namespace BillInspector
{
    /// <summary>
    /// Adds a search/filter bar to lists, arrays, or complex inspectors.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class)]
    public class BillSearchableAttribute : BillDrawerAttribute { }
}
