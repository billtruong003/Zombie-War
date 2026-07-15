using System;

namespace BillInspector
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class BillIndentAttribute : BillAttribute
    {
        public int Level { get; }

        public BillIndentAttribute(int level = 1)
        {
            Level = level;
        }
    }
}
