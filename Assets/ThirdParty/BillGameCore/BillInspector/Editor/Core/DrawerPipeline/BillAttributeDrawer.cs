using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace BillInspector.Editor
{
    /// <summary>
    /// Register a custom drawer for a specific attribute type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class BillCustomDrawerAttribute : Attribute
    {
        public Type AttributeType { get; }

        public BillCustomDrawerAttribute(Type attributeType)
        {
            AttributeType = attributeType;
        }
    }

    /// <summary>
    /// Base class for attribute-specific drawers.
    /// Override CreatePropertyGUI for UI Toolkit, OnGUI for IMGUI fallback.
    /// </summary>
    public abstract class BillAttributeDrawer<TAttribute> : IBillDrawer
        where TAttribute : BillAttribute
    {
        public TAttribute Attribute { get; internal set; }

        public virtual VisualElement CreatePropertyGUI(BillProperty property) => null;
        public virtual void OnGUI(Rect rect, BillProperty property) { }
        public virtual float GetPropertyHeight(BillProperty property) => 18f;
    }
}
