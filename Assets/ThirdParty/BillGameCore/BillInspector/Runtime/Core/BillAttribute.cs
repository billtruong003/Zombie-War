using System;

namespace BillInspector
{
    /// <summary>
    /// Base class for all BillInspector attributes.
    /// Provides common infrastructure for the drawer pipeline.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public abstract class BillAttribute : Attribute
    {
        /// <summary>
        /// Order in which this attribute is processed. Lower = earlier.
        /// </summary>
        public int Order { get; set; }
    }

    /// <summary>
    /// Base class for attributes that change how a field is drawn.
    /// Only one DrawerAttribute per field (last one wins if multiple).
    /// </summary>
    public abstract class BillDrawerAttribute : BillAttribute { }

    /// <summary>
    /// Base class for attributes that group fields together.
    /// </summary>
    public abstract class BillGroupAttribute : BillAttribute
    {
        public string GroupPath { get; protected set; }

        protected BillGroupAttribute(string groupPath)
        {
            GroupPath = groupPath;
        }
    }

    /// <summary>
    /// Base class for meta attributes (show/hide, enable/disable, validation).
    /// Multiple meta attributes can stack on one field.
    /// </summary>
    public abstract class BillMetaAttribute : BillAttribute { }

    /// <summary>
    /// Base for conditional visibility/enable attributes.
    /// </summary>
    public abstract class BillConditionAttribute : BillMetaAttribute
    {
        public string Condition { get; protected set; }
        public object CompareValue { get; protected set; }

        /// <summary>
        /// Condition can be: field name, method name, or @expression.
        /// </summary>
        protected BillConditionAttribute(string condition, object compareValue = null)
        {
            Condition = condition;
            CompareValue = compareValue;
        }
    }
}
