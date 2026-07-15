using UnityEngine;
using UnityEngine.UIElements;

namespace BillInspector.Editor
{
    /// <summary>
    /// Interface for all BillInspector drawers.
    /// Supports both UI Toolkit (primary) and IMGUI (fallback).
    /// </summary>
    public interface IBillDrawer
    {
        /// <summary>UI Toolkit rendering (primary path for Unity 6).</summary>
        VisualElement CreatePropertyGUI(BillProperty property);

        /// <summary>IMGUI rendering (fallback for legacy custom editors).</summary>
        void OnGUI(Rect rect, BillProperty property);

        /// <summary>IMGUI height calculation.</summary>
        float GetPropertyHeight(BillProperty property);
    }
}
