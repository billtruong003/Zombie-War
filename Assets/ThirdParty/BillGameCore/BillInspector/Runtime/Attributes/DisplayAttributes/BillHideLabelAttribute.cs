using System;

namespace BillInspector
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class BillHideLabelAttribute : BillAttribute { }
}
