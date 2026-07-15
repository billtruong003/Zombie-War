using System;

namespace BillInspector
{
    /// <summary>
    /// Calls a method or executes an expression when the field value changes.
    /// Only fires from inspector edits, not from code changes.
    /// </summary>
    /// <example>
    /// [BillOnValueChanged("OnHealthChanged")]
    /// [BillOnValueChanged("@Debug.Log($\"New value: {health}\")")]
    /// </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class BillOnValueChangedAttribute : BillMetaAttribute
    {
        public string Callback { get; }

        public BillOnValueChangedAttribute(string callback)
        {
            Callback = callback;
        }
    }
}
