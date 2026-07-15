#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace BillGameCore.BillFav
{
    [InitializeOnLoad]
    static class BillFavOverlay
    {
        // ── Reflection cache: Unity internals ──
        static readonly Type t_ProjectBrowser;
        static readonly Type t_HostView;
        static readonly Type t_EditorWindowDelegate;
        static readonly FieldInfo fi_m_Parent;
        static readonly FieldInfo fi_m_OnGUI;
        static readonly MethodInfo mi_Wrapper;

        // THE FIX: Event.s_Current maintains modifier state between OnGUI calls.
        // Event.current is only valid INSIDE OnGUI. In EditorApplication.update,
        // Event.current is null/stale. s_Current is the internal field that works everywhere.
        // This is exactly how vFavorites detects Alt in its update loop.
        static readonly FieldInfo fi_Event_s_Current;

        // ── State ──
        static EditorWindow _wrappedBrowser;
        static Delegate _originalGUI;
        static BillFavPanel _panel;
        static float _opacity;
        static bool _shortcutHeld;
        static double _lastOnGUITime;
        static bool _initOk;

        // ───────────────────────────────────────────
        // Static init
        // ───────────────────────────────────────────

        static BillFavOverlay()
        {
            var editorAsm = typeof(Editor).Assembly;
            t_ProjectBrowser = editorAsm.GetType("UnityEditor.ProjectBrowser");
            t_HostView = editorAsm.GetType("UnityEditor.HostView");

            if (t_ProjectBrowser == null || t_HostView == null)
            {
                Debug.LogWarning("[BillFav] Unity internal types not found. Use Window mode (Ctrl+Shift+F).");
                return;
            }

            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            fi_m_Parent = typeof(EditorWindow).GetField("m_Parent", flags);
            fi_m_OnGUI = t_HostView?.GetField("m_OnGUI", flags);
            t_EditorWindowDelegate = t_HostView?.GetNestedType("EditorWindowDelegate",
                BindingFlags.NonPublic | BindingFlags.Public);
            mi_Wrapper = typeof(BillFavOverlay).GetMethod(nameof(WrappedOnGUI),
                BindingFlags.Static | BindingFlags.NonPublic);
            fi_Event_s_Current = typeof(Event).GetField("s_Current",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (fi_m_Parent == null || fi_m_OnGUI == null || mi_Wrapper == null ||
                t_EditorWindowDelegate == null || fi_Event_s_Current == null)
            {
                Debug.LogWarning("[BillFav] Reflection setup incomplete. Use Window mode.");
                return;
            }

            _initOk = true;
            _panel = new BillFavPanel();

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        // ───────────────────────────────────────────
        // Alt detection — reads Event.s_Current (works in update loop)
        // ───────────────────────────────────────────

        static bool IsShortcutHeld()
        {
            var evt = fi_Event_s_Current?.GetValue(null) as Event;
            if (evt == null) return false;

            switch (BillFavPrefs.ActivationKey)
            {
                case BillFavPrefs.KeyCombination.Alt:
                    return evt.alt && !evt.shift && !evt.control && !evt.command;
                case BillFavPrefs.KeyCombination.AltShift:
                    return evt.alt && evt.shift;
                case BillFavPrefs.KeyCombination.CtrlAlt:
                    return Application.platform == RuntimePlatform.OSXEditor
                        ? evt.command && evt.alt : evt.control && evt.alt;
            }
            return false;
        }

        // ───────────────────────────────────────────
        // Update loop — wrap/unwrap
        // ───────────────────────────────────────────

        static void Tick()
        {
            if (!_initOk || BillFavPrefs.PluginDisabled || !BillFavPrefs.OverlayEnabled) return;

            _shortcutHeld = IsShortcutHeld();
            bool mouseOverBrowser = EditorWindow.mouseOverWindow != null &&
                                    EditorWindow.mouseOverWindow.GetType() == t_ProjectBrowser;

            if (_shortcutHeld && mouseOverBrowser && _wrappedBrowser == null &&
                UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                TryWrap(EditorWindow.mouseOverWindow);

            if (!_shortcutHeld && _wrappedBrowser != null && _opacity <= 0.01f)
                Unwrap();

            if (_wrappedBrowser != null)
                _wrappedBrowser.Repaint();
        }

        // ───────────────────────────────────────────
        // Wrap / Unwrap
        // ───────────────────────────────────────────

        static void TryWrap(EditorWindow browser)
        {
            try
            {
                var hostView = fi_m_Parent.GetValue(browser);
                if (hostView == null) return;

                var currentDelegate = fi_m_OnGUI.GetValue(hostView) as Delegate;
                if (currentDelegate == null) return;

                // If the delegate is already our wrapper (stale from before domain reload),
                // skip — we can't capture it as "original" or we'd get circular delegation.
                // The browser will re-initialize its default delegate on next repaint.
                if (currentDelegate.Method == mi_Wrapper) return;

                _originalGUI = currentDelegate;

                var newDelegate = mi_Wrapper.CreateDelegate(t_EditorWindowDelegate, hostView);
                fi_m_OnGUI.SetValue(hostView, newDelegate);

                _wrappedBrowser = browser;
                _opacity = 0f;
                browser.Focus();
                browser.Repaint();
            }
            catch (Exception e)
            {
                Debug.LogError($"[BillFav] Wrap failed: {e.Message}");
                _wrappedBrowser = null;
                _originalGUI = null;
            }
        }

        static void Unwrap()
        {
            if (_wrappedBrowser == null) return;
            try
            {
                var hostView = fi_m_Parent.GetValue(_wrappedBrowser);
                if (hostView != null)
                {
                    var cur = fi_m_OnGUI.GetValue(hostView) as Delegate;
                    if (cur?.Method == mi_Wrapper)
                        fi_m_OnGUI.SetValue(hostView, _originalGUI);
                }
                _wrappedBrowser.Repaint();
            }
            catch (Exception e) { Debug.LogError($"[BillFav] Unwrap failed: {e.Message}"); }
            finally { _wrappedBrowser = null; _originalGUI = null; }
        }

        // ───────────────────────────────────────────
        // Wrapped OnGUI
        // ───────────────────────────────────────────

        static void WrappedOnGUI(object _)
        {
            if (_wrappedBrowser == null || _wrappedBrowser.GetType() != t_ProjectBrowser)
            { CallOriginalGUI(); return; }

            // Inside OnGUI, Event.current IS valid
            bool held = Event.current != null && Event.current.alt;
            _shortcutHeld = held;
            float target = held ? 1f : 0f;

            float dt = Mathf.Clamp((float)(EditorApplication.timeSinceStartup - _lastOnGUITime), 0.001f, 0.05f);
            _lastOnGUITime = EditorApplication.timeSinceStartup;
            _opacity = Mathf.MoveTowards(_opacity, target, 8f * dt);
            if (target == 0f && _opacity < 0.04f) _opacity = 0f;
            if (target == 1f && _opacity > 0.96f) _opacity = 1f;

            // ── Mirrors vFavorites WrappedOnGUI flow exactly ──

            // 1) Like vFav's UpdateDragging(): always handle drag BEFORE any GUI
            //    so DragPerform is accepted (and Use()'d) before the browser sees it,
            //    and hotControl is claimed so the browser ignores DragUpdated.
            var overlayRect = GetOverlayRect();
            if (_opacity > 0.01f)
                _panel.UpdateDrag(overlayRect);

            // 2) Like vFav: doOriginalGUIFirst = isRepaint || isLayout
            var evt = Event.current;
            bool doOriginalGUIFirst = evt.type == EventType.Repaint || evt.type == EventType.Layout;

            if (doOriginalGUIFirst)
            {
                // Like vFav doOriginalGUIFirst_(): browser draws behind, BillFav draws on top
                CallOriginalGUI();
                DrawBillFavOverlay(overlayRect);
            }
            else
            {
                // Like vFav doVFavoritesGUIFirst_(): BillFav handles input first
                DrawBillFavOverlay(overlayRect);

                // Like vFav: consume MouseUp/MouseDrag/Scroll so browser doesn't interfere.
                // NOT DragUpdated (hotControl already blocks browser).
                // NOT DragPerform (already Use()'d inside DragDropHandler.TryAcceptFromOutside).
                if (_opacity > 0.5f && overlayRect.Contains(evt.mousePosition))
                {
                    if (evt.type == EventType.MouseUp ||
                        evt.type == EventType.MouseDrag ||
                        evt.type == EventType.ScrollWheel)
                        evt.Use();
                }

                CallOriginalGUI();
            }

            if (_panel.IsDragging || _opacity > 0.01f)
                _wrappedBrowser.Repaint();
        }

        static Rect GetOverlayRect()
        {
            var rect = _wrappedBrowser.position;
            rect.x = 0; rect.y = 0;

            var tvr = GetTreeViewRect();
            if (tvr.width > 10)
            {
                rect.width = tvr.width;
                rect.y = rect.height - tvr.height;
                rect.height = tvr.height;
            }
            return rect;
        }

        static void DrawBillFavOverlay(Rect overlayRect)
        {
            if (_opacity <= 0.01f) return;

            try
            {
                var prev = GUI.color;
                GUI.color = new Color(1, 1, 1, _opacity);
                GUI.BeginGroup(overlayRect);
                _panel.Draw(new Rect(0, 0, overlayRect.width, overlayRect.height), _opacity);
                GUI.EndGroup();
                GUI.color = prev;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BillFav] Draw error — unwrapping: {e.Message}\n{e.StackTrace}");
                Unwrap();
            }
        }

        static void CallOriginalGUI()
        {
            if (_originalGUI == null) return;
            try
            {
                var m = _originalGUI.Method;
                if (m.IsStatic) m.Invoke(null, new object[] { _wrappedBrowser });
                else m.Invoke(_wrappedBrowser, null);
            }
            catch (Exception e) { Debug.LogError($"[BillFav] Original GUI: {e.Message}"); }
        }

        static Rect GetTreeViewRect()
        {
            try
            {
                var fi = t_ProjectBrowser.GetField("m_TreeViewRect",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                return fi != null ? (Rect)fi.GetValue(_wrappedBrowser) : Rect.zero;
            }
            catch { return Rect.zero; }
        }

        [InitializeOnLoadMethod]
        static void DomainReloadCleanup()
        {
            if (_wrappedBrowser != null) { Unwrap(); return; }

            // After domain reload, static fields are null but some browsers may still
            // have our WrappedOnGUI as their delegate (stale from previous session).
            // Find and unwrap them so the browser re-initializes its default OnGUI.
            if (t_ProjectBrowser == null || fi_m_Parent == null || fi_m_OnGUI == null || mi_Wrapper == null) return;

            foreach (var obj in Resources.FindObjectsOfTypeAll(t_ProjectBrowser))
            {
                var browser = obj as EditorWindow;
                if (browser == null) continue;
                try
                {
                    var hostView = fi_m_Parent.GetValue(browser);
                    if (hostView == null) continue;
                    var curDelegate = fi_m_OnGUI.GetValue(hostView) as Delegate;
                    if (curDelegate?.Method == mi_Wrapper)
                    {
                        // Stale wrapper — set to null so Unity re-creates the default delegate.
                        fi_m_OnGUI.SetValue(hostView, null);
                        browser.Repaint();
                    }
                }
                catch { /* ignore individual failures */ }
            }
        }
    }
}
#endif
