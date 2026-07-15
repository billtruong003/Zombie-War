using System;

namespace BillInspector
{
    /// <summary>
    /// Marks a field for BillInspector custom serialization.
    /// Use on Dictionary, HashSet, Tuple, or other types Unity can't serialize.
    /// Requires the class to implement ISerializationCallbackReceiver
    /// (or inherit from BillSerializedMonoBehaviour).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillSerializeAttribute : Attribute { }
}
