using System;

namespace BillInspector
{
    /// <summary>
    /// Configures how a Dictionary is drawn in the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillDictionaryDrawerAttribute : BillDrawerAttribute
    {
        public string KeyLabel { get; set; } = "Key";
        public string ValueLabel { get; set; } = "Value";
        public bool IsReadOnly { get; set; }
    }
}
