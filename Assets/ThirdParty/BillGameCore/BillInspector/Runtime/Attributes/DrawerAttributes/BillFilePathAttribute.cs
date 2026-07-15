using System;

namespace BillInspector
{
    [AttributeUsage(AttributeTargets.Field)]
    public class BillFilePathAttribute : BillDrawerAttribute
    {
        public string ParentFolder { get; set; }
        public string Extensions { get; set; }
        public bool AbsolutePath { get; set; }
    }
}
