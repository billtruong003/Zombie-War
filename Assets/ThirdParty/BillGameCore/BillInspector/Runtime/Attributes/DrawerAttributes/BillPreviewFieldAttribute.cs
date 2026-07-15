using System;

namespace BillInspector
{
    /// <summary>
    /// Shows a texture/sprite/prefab preview thumbnail.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillPreviewFieldAttribute : BillDrawerAttribute
    {
        public int Size { get; }
        public bool AlignRight { get; set; }

        public BillPreviewFieldAttribute(int size = 64)
        {
            Size = size;
        }
    }
}
