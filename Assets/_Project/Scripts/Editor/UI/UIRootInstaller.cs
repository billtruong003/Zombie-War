using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// Nguồn DUY NHẤT dựng UIRoot cho scene UI: Canvas (ScreenSpaceOverlay, portrait)
    /// + CanvasScaler + GraphicRaycaster + <see cref="UIManager"/> + EventSystem.
    /// Mọi installer màn (Hub/Menu/HUD) gọi <see cref="EnsureRoot"/> rồi gắn UIScreen làm con.
    /// UIManager tự register screen con ở Awake, push initialScreen ở Start —
    /// KHÔNG có UIManager thì scene không hiện UI (chỉ thấy model preview nền).
    /// </summary>
    public static class UIRootInstaller
    {
        /// <summary>
        /// Ensure UIRoot GO tồn tại với đủ Canvas + Scaler + Raycaster + UIManager + EventSystem.
        /// Trả về Canvas (parent cho các UIScreen). Idempotent.
        /// </summary>
        public static Canvas EnsureRoot()
        {
            var go = GameObject.Find("UIRoot");
            if (go == null) go = new GameObject("UIRoot");

            var canvas = go.GetComponent<Canvas>();
            if (canvas == null) canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UITheme.RefWidth, UITheme.RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
            if (go.GetComponent<UIManager>() == null) go.AddComponent<UIManager>();

            EnsureEventSystem();
            return canvas;
        }

        /// <summary>Ensure có 1 EventSystem trong scene (bắt buộc để button/raycast hoạt động).</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        /// <summary>
        /// Gán initialScreen (private [SerializeField]) cho UIManager qua SerializedObject —
        /// đây là màn được push tự động khi vào Play (vd HubScreen ở Menu).
        /// </summary>
        public static void SetInitialScreen(UIScreen screen)
        {
            if (screen == null) return;
            var go = GameObject.Find("UIRoot");
            var mgr = go != null ? go.GetComponent<UIManager>() : null;
            if (mgr == null) { Debug.LogWarning("[UIRootInstaller] Không thấy UIManager trên UIRoot."); return; }

            var so = new SerializedObject(mgr);
            var p = so.FindProperty("initialScreen");
            if (p == null) { Debug.LogWarning("[UIRootInstaller] Thiếu field 'initialScreen' trên UIManager."); return; }
            p.objectReferenceValue = screen;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// One-click fix cho scene hiện tại: ensure UIRoot đầy đủ + wire HubScreen làm initialScreen nếu có.
        /// </summary>
        [MenuItem("ZombieWar/UI/Ensure UIRoot (Canvas + UIManager)")]
        public static void Build()
        {
            var canvas = EnsureRoot();
            var hub = Object.FindFirstObjectByType<HubScreen>(FindObjectsInactive.Include);
            if (hub != null) SetInitialScreen(hub);
            else Debug.LogWarning("[UIRootInstaller] Chưa có HubScreen trong scene — build Hub trước để set initialScreen.");

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Debug.Log("[UIRootInstaller] UIRoot ensured (Canvas + UIManager + EventSystem)"
                      + (hub != null ? " + initialScreen=HubScreen." : " — CHƯA set initialScreen (thiếu Hub)."));
        }
    }
}
