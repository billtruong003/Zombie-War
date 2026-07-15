#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace BillGameCore.BillFav
{
    /// <summary>
    /// Standalone EditorWindow for BillFav. No hacks, no reflection.
    /// Can be docked, tabbed, floated — standard Unity editor window.
    /// Uses the same BillFavPanel renderer as the overlay.
    ///
    /// Open via: BillGameCore > BillFav Window, or Ctrl+Shift+F
    /// </summary>
    public class BillFavWindow : EditorWindow
    {
        BillFavPanel _panel;

        [MenuItem("BillGameCore/BillFav Window %#f")]
        public static void ShowWindow()
        {
            var win = GetWindow<BillFavWindow>("BillFav");
            win.minSize = new Vector2(180, 200);
            win.Show();
        }

        void OnEnable()
        {
            _panel = new BillFavPanel();
            titleContent = new GUIContent("BillFav", EditorGUIUtility.IconContent("Favorite Icon").image);
        }

        void OnGUI()
        {
            if (BillFavPrefs.PluginDisabled)
            {
                EditorGUILayout.HelpBox("BillFav is disabled. Enable it in BillGameCore > BillFav menu.", MessageType.Info);
                return;
            }

            _panel ??= new BillFavPanel();

            var rect = new Rect(0, 0, position.width, position.height);
            _panel.Draw(rect, 1f);

            // Continuous repaint while dragging
            if (Event.current.type == EventType.DragUpdated ||
                Event.current.type == EventType.DragPerform ||
                Event.current.type == EventType.MouseDrag)
            {
                Repaint();
            }
        }

        void OnSelectionChange() => Repaint();
        void OnProjectChange() => Repaint();
        void OnHierarchyChange() => Repaint();
    }
}
#endif
