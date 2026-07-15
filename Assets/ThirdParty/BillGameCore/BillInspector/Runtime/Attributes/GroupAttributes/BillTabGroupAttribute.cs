using System;

namespace BillInspector
{
    /// <summary>
    /// Groups fields into tabs.
    /// Use the same groupId for fields in different tabs.
    /// </summary>
    /// <example>
    /// [BillTabGroup("tabs", "Stats")]
    /// public int strength;
    /// [BillTabGroup("tabs", "Inventory")]
    /// public Sprite headSlot;
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public class BillTabGroupAttribute : BillGroupAttribute
    {
        public string TabName { get; }

        public BillTabGroupAttribute(string groupId, string tabName) : base(groupId)
        {
            TabName = tabName;
        }
    }
}
