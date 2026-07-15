#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace BillGameCore.BillFav
{
    /// <summary>
    /// Handles all drag & drop logic for BillFav.
    /// Supports: drag from outside into panel, reorder within panel,
    /// drag from panel to outside (scene/hierarchy).
    /// </summary>
    public class DragDropHandler
    {
        public bool IsDragging { get; private set; }
        public bool IsDraggingFromPage { get; private set; }
        public bool IsDraggingFromOutside => IsDragging && !IsDraggingFromPage;
        public bool IsDraggingToOutside { get; private set; }

        public BillFavItem DraggedItem { get; private set; }
        public float DraggedItemHoldOffset { get; private set; }
        public int DragOriginIndex { get; private set; }

        BillFavData _data;
        float _rowHeight;

        // ───────────────────────────────────────────
        // Public API
        // ───────────────────────────────────────────

        public void Update(Event evt, Rect panelRect, BillFavData data, float rowHeight,
                           Vector2 mousePos, Vector2 mouseDownPos, float scrollPos, float mouseDragDist)
        {
            _data = data;
            _rowHeight = rowHeight;

            TryInitFromOutside(evt, panelRect);
            TryInitFromPage(evt, panelRect, mouseDownPos, scrollPos, mouseDragDist);
            TryAcceptFromOutside(evt);
            TryAcceptFromPage(evt);
            TryCancelFromOutside(panelRect);
            TryCancelFromPageToOutside(evt, panelRect);

            if (IsDragging)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                EditorGUIUtility.hotControl = EditorGUIUtility.GetControlID(FocusType.Passive);
            }

            // Reset draggingToOutside when DnD completes
            if (IsDraggingToOutside && !DragAndDrop.objectReferences.Any())
                IsDraggingToOutside = false;
        }

        /// <summary>
        /// Where to insert the dragged item based on current mouse Y.
        /// </summary>
        public int InsertIndex(Vector2 mouseRowsPos)
        {
            if (!IsDragging || _data == null) return 0;
            int idx = Mathf.FloorToInt((mouseRowsPos.y + DraggedItemHoldOffset) / _rowHeight);
            return Mathf.Clamp(idx, 0, _data.ActivePage.Items.Count);
        }

        /// <summary>
        /// Y position of the dragged item visual in group space.
        /// </summary>
        public float DraggedItemY(Vector2 mouseGroupPos)
        {
            return Mathf.Max(0, mouseGroupPos.y - _rowHeight / 2 + DraggedItemHoldOffset);
        }

        public void Cancel()
        {
            if (!IsDragging) return;

            if (IsDraggingFromPage && DraggedItem != null)
            {
                // Return item to original position
                var items = _data.ActivePage.Items;
                int idx = Mathf.Clamp(DragOriginIndex, 0, items.Count);
                items.Insert(idx, DraggedItem);
                _data.SetDirty();
            }

            Reset();
        }

        // ───────────────────────────────────────────
        // Init
        // ───────────────────────────────────────────

        void TryInitFromOutside(Event evt, Rect panelRect)
        {
            if (IsDragging) return;
            if (!panelRect.Contains(evt.mousePosition)) return;
            if (evt.type != EventType.DragUpdated) return;
            if (!DragAndDrop.objectReferences.Any()) return;
            if (IsDraggingToOutside) return; // avoid re-catch

            IsDragging = true;
            IsDraggingFromPage = false;
            DraggedItem = new BillFavItem(DragAndDrop.objectReferences[0]);
            DraggedItemHoldOffset = 0;
        }

        void TryInitFromPage(Event evt, Rect panelRect, Vector2 mouseDownPos, float scrollPos, float dragDist)
        {
            if (IsDragging) return;
            if (!panelRect.Contains(evt.mousePosition)) return;
            if (evt.type != EventType.MouseDrag) return;
            if (dragDist < 3f) return;
            if (_data == null) return;

            float rowsY = mouseDownPos.y + scrollPos;
            int idx = Mathf.FloorToInt(rowsY / _rowHeight);
            var items = _data.ActivePage.Items;
            if (idx < 0 || idx >= items.Count) return;

            IsDragging = true;
            IsDraggingFromPage = true;
            DragOriginIndex = idx;
            DraggedItem = items[idx];
            DraggedItemHoldOffset = (idx * _rowHeight + _rowHeight / 2f) - rowsY;

            items.RemoveAt(idx);
            _data.SetDirty();
        }

        // ───────────────────────────────────────────
        // Accept
        // ───────────────────────────────────────────

        void TryAcceptFromOutside(Event evt)
        {
            if (!IsDragging || IsDraggingFromPage) return;
            if (evt.type != EventType.DragPerform) return;

            DragAndDrop.AcceptDrag();
            evt.Use();
            AcceptDrop(evt);
        }

        void TryAcceptFromPage(Event evt)
        {
            if (!IsDragging || !IsDraggingFromPage) return;
            if (evt.type != EventType.MouseUp) return;

            evt.Use();
            AcceptDrop(evt);
        }

        void AcceptDrop(Event evt)
        {
            if (DraggedItem == null || _data == null) { Reset(); return; }

            var items = _data.ActivePage.Items;

            // Deduplicate check for outside drops
            if (!IsDraggingFromPage && items.Any(i => i.Equals(DraggedItem)))
            {
                Reset();
                return;
            }

            Vector2 mouseRowsPos = evt.mousePosition; // caller should transform this
            int insertAt = InsertIndex(mouseRowsPos);
            insertAt = Mathf.Clamp(insertAt, 0, items.Count);
            items.Insert(insertAt, DraggedItem);

            _data.SetDirty();
            _data.Save();

            // Store for animation callback
            var dropped = DraggedItem;
            Reset();

            // Signal that a drop happened (for animation)
            OnItemDropped?.Invoke(dropped, insertAt);
        }

        // ───────────────────────────────────────────
        // Cancel
        // ───────────────────────────────────────────

        void TryCancelFromOutside(Rect panelRect)
        {
            if (!IsDraggingFromOutside) return;
            if (panelRect.Contains(Event.current.mousePosition)) return;
            Reset();
        }

        void TryCancelFromPageToOutside(Event evt, Rect panelRect)
        {
            if (!IsDraggingFromPage) return;
            if (evt.type != EventType.MouseDrag) return;
            if (panelRect.Contains(evt.mousePosition)) return;
            if (DragAndDrop.objectReferences.Any()) return;
            if (DraggedItem == null || !DraggedItem.IsLoaded) return;

            // Start Unity DnD so user can drop into scene/hierarchy
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new[] { DraggedItem.Obj };
            DragAndDrop.StartDrag(DraggedItem.Name);

            IsDraggingToOutside = true;
            Reset();
        }

        void Reset()
        {
            IsDragging = false;
            IsDraggingFromPage = false;
            DraggedItem = null;
            EditorGUIUtility.hotControl = 0;
        }

        // ───────────────────────────────────────────
        // Event
        // ───────────────────────────────────────────

        public System.Action<BillFavItem, int> OnItemDropped;
    }
}
#endif
