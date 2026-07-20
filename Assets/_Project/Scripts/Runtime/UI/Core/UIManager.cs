using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// Quản lý stack màn UI dưới 1 UIRoot Canvas duy nhất (portrait 1080x1920).
    /// Screens là con trực tiếp của UIRoot, đăng ký tự động ở Awake (kể cả inactive).
    /// API: Push / Pop / Replace / Get. Escape (Android back) pop màn top nếu cho phép.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Tooltip("Màn mở tự động khi scene start (vd HubScreen ở Menu). Để trống nếu flow tự push.")]
        [SerializeField] private UIScreen initialScreen;

        private readonly Dictionary<System.Type, UIScreen> _screens = new();
        private readonly List<UIScreen> _stack = new();

        public UIScreen Top => _stack.Count > 0 ? _stack[^1] : null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Đăng ký mọi UIScreen con (prefab screen đặt inactive sẵn trong UIRoot).
            foreach (var s in GetComponentsInChildren<UIScreen>(includeInactive: true))
            {
                var t = s.GetType();
                if (_screens.ContainsKey(t))
                {
                    Debug.LogWarning($"[UIManager] Trùng screen type {t.Name} — bỏ qua {s.name}");
                    continue;
                }
                _screens.Add(t, s);
                s.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (initialScreen != null) Push(initialScreen);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            var top = Top;
            if (top == null) return;
            if (top.OnEscape()) return;          // màn tự xử lý (vd: đóng tab con)
            if (top.AllowEscapePop) Pop();
        }

        // ------------------------------------------------ public API
        public T Get<T>() where T : UIScreen
            => _screens.TryGetValue(typeof(T), out var s) ? (T)s : null;

        /// <summary>Đè màn mới lên stack. Màn dưới bị Blur (vẫn render nếu popup).</summary>
        public T Push<T>() where T : UIScreen
        {
            var s = Get<T>();
            if (s == null) { Debug.LogError($"[UIManager] Không có screen {typeof(T).Name}"); return null; }
            return (T)Push(s);
        }

        /// <summary>Overload non-generic — dùng cho serialized ref (initialScreen, button wiring).</summary>
        public UIScreen Push(UIScreen s)
        {
            if (s == null) return null;
            if (Top == s) return s;
            // Màn full-screen (opaque) che kín màn dưới → ẩn hẳn để khỏi render chồng.
            // Popup (IsOpaque=false) chỉ blur màn dưới cho thấy mờ phía sau.
            if (s.IsOpaque)
            {
                for (int i = _stack.Count - 1; i >= 0; i--)
                    if (_stack[i].IsShown) _stack[i].Hide();
            }
            else
            {
                Top?.Blur();
            }
            _stack.Remove(s);
            _stack.Add(s);
            s.transform.SetAsLastSibling();      // render trên cùng
            s.Show();
            return s;
        }

        /// <summary>Pop màn top; màn dưới nhận Focus.</summary>
        public void Pop()
        {
            if (_stack.Count == 0) return;
            var top = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);
            top.Hide();
            // Màn dưới có thể đã bị Hide (khi top là opaque) → Show() lại; nếu chỉ Blur thì Show()→Focus.
            Top?.Show();
        }

        /// <summary>Xoá sạch stack rồi push màn mới — dùng khi đổi context (vd: vào trận → HUD).</summary>
        public T Replace<T>() where T : UIScreen
        {
            for (int i = _stack.Count - 1; i >= 0; i--) _stack[i].Hide(instant: true);
            _stack.Clear();
            return Push<T>();
        }

        /// <summary>Pop cho tới khi màn T là top (nếu có trong stack).</summary>
        public void PopTo<T>() where T : UIScreen
        {
            var target = Get<T>();
            if (target == null) return;
            while (_stack.Count > 0 && _stack[^1] != target)
            {
                var top = _stack[^1];
                _stack.RemoveAt(_stack.Count - 1);
                top.Hide();
            }
            Top?.Show();
        }
    }
}
