using System;

namespace BillInspector
{
    /// <summary>
    /// Restricts an Object field to scene objects only (no project assets).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillSceneObjectsOnlyAttribute : BillMetaAttribute { }
}
