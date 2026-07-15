using System;

namespace BillInspector
{
    /// <summary>
    /// Groups multiple buttons into a horizontal row.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class BillButtonGroupAttribute : BillAttribute
    {
        public string GroupName { get; }

        public BillButtonGroupAttribute(string groupName)
        {
            GroupName = groupName;
        }
    }
}
