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
            var grid = GetComponent<GridLayoutGroup>();
            var rt = (RectTransform)transform;
            float w = rt.rect.width - grid.padding.left - grid.padding.right - grid.spacing.x * (columns - 1);
            if (w <= 0f) return;
            grid.cellSize = new Vector2(Mathf.Floor(w / columns), grid.cellSize.y);
        }
    }
}
