#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BillGameCore.BillFav
{
    /// <summary>
    /// Drives all BillFav animations: opacity fade, page scroll, row gaps, drop highlight.
    /// Extracted from vFav's inline animation code into a clean, testable service.
    /// Uses SmoothDamp for buttery smooth transitions.
    /// </summary>
    public class AnimationDriver
    {
        // ── Opacity ──
        public float Opacity;
        float _opacityVelocity;

        // ── Page scroll ──
        public float PageScrollPos = -1f;
        float _pageScrollVelocity;

        // ── Dropped item ──
        public float DroppedItemY;
        public float DroppedItemShadow;
        public float DroppedItemHighlight;
        float _droppedYVelocity;
        public bool IsAnimatingDrop;

        // ── Page button flash ──
        public float PrevPageBrightness = 1f;
        public float NextPageBrightness = 1f;

        // ── Row gaps (for drag insertion animation) ──
        float[] _rowGaps;
        public float[] RowGaps => _rowGaps;

        float _deltaTime;
        double _lastTime;

        // ───────────────────────────────────────────
        // Public API
        // ───────────────────────────────────────────

        public void Tick(float targetOpacity, int targetPageIndex, BillFavData data,
                         bool isDragging, int insertIndex, float rowHeight)
        {
            CalcDeltaTime();
            TickOpacity(targetOpacity);
            TickPageScroll(targetPageIndex);
            TickRowGaps(data, isDragging, insertIndex, rowHeight);
            TickDroppedItem(data, rowHeight);
            TickPageButtons();
        }

        public float GetRowGap(int index)
        {
            if (_rowGaps == null || index < 0 || index >= _rowGaps.Length) return 0f;
            return _rowGaps[index];
        }

        public void SetRowGap(int index, float value)
        {
            EnsureRowGaps(index + 1);
            _rowGaps[index] = value;
        }

        public void FlashPrevPage() => PrevPageBrightness = 2f;
        public void FlashNextPage() => NextPageBrightness = 2f;

        public void StartDropAnimation(float startY)
        {
            DroppedItemY = startY;
            _droppedYVelocity = 0;
            DroppedItemShadow = 1f;
            DroppedItemHighlight = 1f;
            IsAnimatingDrop = true;
        }

        public void CancelAnimations()
        {
            if (_rowGaps != null)
                for (int i = 0; i < _rowGaps.Length; i++)
                    _rowGaps[i] = 0;

            IsAnimatingDrop = false;
        }

        public bool IsAnimating =>
            !Mathf.Approximately(Opacity, Opacity > 0.5f ? 1f : 0f) ||
            IsAnimatingDrop ||
            (PageScrollPos >= 0 && !Mathf.Approximately(PageScrollPos, Mathf.Round(PageScrollPos)));

        // ───────────────────────────────────────────
        // Tick internals
        // ───────────────────────────────────────────

        void CalcDeltaTime()
        {
            double now = EditorApplication.timeSinceStartup;
            _deltaTime = (float)(now - _lastTime);
            if (_deltaTime > 0.05f) _deltaTime = 0.0166f; // clamp spikes
            _lastTime = now;
        }

        void TickOpacity(float target)
        {
            if (!BillFavPrefs.FadeAnimations) { Opacity = target; return; }

            Opacity = Mathf.SmoothDamp(Opacity, target, ref _opacityVelocity, 0.09f, 100f, _deltaTime);

            if (target == 0f && Opacity < 0.04f) Opacity = 0f;
            if (target == 1f && Opacity > 0.96f) Opacity = 1f;
        }

        void TickPageScroll(int targetIndex)
        {
            if (PageScrollPos < 0) PageScrollPos = targetIndex;
            if (!BillFavPrefs.PageScrollAnimation) { PageScrollPos = targetIndex; return; }

            PageScrollPos = Mathf.SmoothDamp(PageScrollPos, targetIndex, ref _pageScrollVelocity, 0.2f, 100f, _deltaTime);

            if (Mathf.Abs(PageScrollPos - targetIndex) < 0.001f)
                PageScrollPos = targetIndex;
        }

        void TickRowGaps(BillFavData data, bool isDragging, int insertIndex, float rowHeight)
        {
            if (data == null) return;
            int count = data.ActivePage.Items.Count + 1;
            EnsureRowGaps(count);

            float speed = 10f;
            for (int i = 0; i < count; i++)
            {
                float target = (isDragging && i == insertIndex) ? rowHeight : 0f;
                _rowGaps[i] = Mathf.Lerp(_rowGaps[i], target, speed * _deltaTime);
                if (Mathf.Abs(_rowGaps[i]) < 0.5f && target == 0f) _rowGaps[i] = 0f;
            }
        }

        void TickDroppedItem(BillFavData data, float rowHeight)
        {
            if (!IsAnimatingDrop) return;

            // Find target Y based on item index in the list
            float targetY = 0; // will be set by caller
            float ySpeed = 8f, shadowSpeed = 8f, highlightSpeed = 10f;

            DroppedItemShadow = Mathf.Lerp(DroppedItemShadow, 0f, shadowSpeed * _deltaTime);
            DroppedItemHighlight = Mathf.Lerp(DroppedItemHighlight, 0f, highlightSpeed * _deltaTime);

            if (DroppedItemShadow < 0.01f)
                IsAnimatingDrop = false;
        }

        void TickPageButtons()
        {
            if (!BillFavPrefs.PageScrollAnimation)
            {
                PrevPageBrightness = NextPageBrightness = 1f;
                return;
            }

            PrevPageBrightness = Mathf.Lerp(PrevPageBrightness, 1f, 7f * _deltaTime);
            NextPageBrightness = Mathf.Lerp(NextPageBrightness, 1f, 7f * _deltaTime);
        }

        void EnsureRowGaps(int count)
        {
            if (_rowGaps == null || _rowGaps.Length < count)
            {
                var old = _rowGaps;
                _rowGaps = new float[count + 4]; // a bit of slack
                if (old != null)
                    System.Array.Copy(old, _rowGaps, Mathf.Min(old.Length, _rowGaps.Length));
            }
        }
    }
}
#endif
