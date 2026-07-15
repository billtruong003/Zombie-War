using System;

namespace BillInspector
{
    /// <summary>
    /// Renders an enum as horizontal toggle buttons instead of a dropdown.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillEnumToggleButtonsAttribute : BillDrawerAttribute { }
}
