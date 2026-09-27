using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// M9 UI kit: a GridLayoutGroup whose cells always split the available width into
    /// <see cref="Columns"/> equal columns, so a grid built for one screen width fits every phone.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class GridFit : UIBehaviour
    {
        [SerializeField, Min(1)] private int columns = 3;

        public int Columns { get => columns; set { columns = Mathf.Max(1, value); Fit(); } }

        protected override void OnEnable() { base.OnEnable(); Fit(); }
        protected override void OnRectTransformDimensionsChange() => Fit();

        public void Fit()
        {
            // GridLayoutGroup reports columns x cell as its minimum width. Once the screen got wider
            // (rotation, a resized window, a tablet) the cells grew, the parent layout could never
            // make the grid narrower again, and the last columns ran off the right edge. The grid's
            // width comes from its parent only.
            var le = GetComponent<LayoutElement>();
            if (le == null) le = gameObject.AddComponent<LayoutElement>();
            le.minWidth = 0f;
            le.preferredWidth = 0f;
            if (le.flexibleWidth < 0f) le.flexibleWidth = 1f;

            var grid = GetComponent<GridLayoutGroup>();
            var rt = (RectTransform)transform;
            float w = rt.rect.width - grid.padding.left - grid.padding.right - grid.spacing.x * (columns - 1);
            if (w <= 0f) return;
            grid.cellSize = new Vector2(Mathf.Floor(w / columns), grid.cellSize.y);
        }
    }
}
