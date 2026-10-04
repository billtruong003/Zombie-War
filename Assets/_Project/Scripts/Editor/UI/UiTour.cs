using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar.EditorTools
{
    /// Play-mode tour of every screen: captures a PNG per screen and measures the LIVE rects (after
    /// layout groups, safe area and runtime sizing), which a static prefab scan cannot see.
    /// Start Play from Bootstrap.unity first, then run the menu. Output: Review/UiTour/<tag>/.
    public static class UiTour
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        struct Step { public string name; public Action act; public Func<bool> until; public float wait; }

        static List<Step> _steps;
        static int _i;
        static double _t;
        static bool _acted;
        static double _settleUntil;   // a capture lands on a later rendered frame: hold the next step
        static string _dir;
        static readonly StringBuilder Log = new StringBuilder();

        public static string LastReport => Log.ToString();
        public static bool Running => _steps != null;

        [MenuItem("ZombieWar/UI/Audit/Screen Tour (Play Mode)")]
        public static void RunMenu() => Run("latest", true);

        public static void Run(string tag, bool includeRun)
        {
            if (!Application.isPlaying) { Debug.LogError("[UiTour] Enter Play from Bootstrap.unity first."); return; }
            _dir = System.IO.Path.GetFullPath($"Review/UiTour/{tag}");
            System.IO.Directory.CreateDirectory(_dir);
            Log.Clear();
            Log.AppendLine($"[UiTour] {tag} screen {Screen.width}x{Screen.height}");
            _steps = BuildSteps(includeRun);
            _i = 0; _acted = false; _t = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static List<Step> BuildSteps(bool includeRun)
        {
            var s = new List<Step>
            {
                new Step { name = null, until = () => UIManager.Instance != null && UIManager.Instance.Top != null, wait = 5f },   // boot splash fades out first
                // The V2 menu (the V1 screens were removed 04/10).
                Shot("home", () => UIManager.Instance.PopTo<HomeScreen>()),
                Shot("arsenal", () => UIManager.Instance.Push<ArsenalScreen>()),
                Shot("shop", () => { UIManager.Instance.PopTo<HomeScreen>(); UIManager.Instance.Push<ShopScreenV2>(); }),
                Shot("studio", () => { UIManager.Instance.PopTo<HomeScreen>(); UIManager.Instance.Push<StudioScreen>(); }),
                Shot("gacha", () => { UIManager.Instance.PopTo<HomeScreen>(); UIManager.Instance.Push<GachaScreen>(); }),
                Shot("pass", () => { UIManager.Instance.PopTo<HomeScreen>(); UIManager.Instance.Push<PassScreenV2>(); }),
            };
            if (!includeRun) return s;
            s.Add(Shot("loading", () => { UIManager.Instance.PopTo<HomeScreen>(); GameFlow.StartGameplay(); }, 0.6f));
            s.Add(new Step { name = null, until = () => RunState.Current != null && Overlays() != null, wait = 3f });
            s.Add(new Step { name = null, act = () =>
            {
                Cheat("zw.god");
                foreach (var id in new[] { "auto.orbit", "auto.chainlightning", "auto.chainlightning", "stat.firerate", "stat.damage" }) Cheat("zw.skill " + id);
            }, wait = 2.5f });
            s.Add(Shot("hud", null));
            s.Add(Shot("levelup", () => Overlays().ShowLevelUp()));
            s.Add(Shot("pause", () => { Call(Overlays(), "PickPerk", 0); Overlays().ShowPause(); }));
            s.Add(Shot("settings", () => Call(Overlays(), "OpenSettings")));
            s.Add(Shot("result", () => { Set(Overlays(), "settingsRoot", false); Call(Overlays(), "EndRun"); }, 2.5f));
            return s;
        }

        static Step Shot(string name, Action act, float wait = 1.6f) => new Step { name = name, act = act, wait = wait };

        static RunOverlays Overlays() => UnityEngine.Object.FindFirstObjectByType<RunOverlays>(FindObjectsInactive.Include);
        static void Cheat(string cmd) { try { BillGameCore.Bill.Cheat?.Execute(cmd); } catch (Exception e) { Debug.LogWarning(e.Message); } }
        static void Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, Any)?.Invoke(o, a);
        static void Set(object o, string f, bool on) => (o.GetType().GetField(f, Any)?.GetValue(o) as GameObject)?.SetActive(on);

        static void Tick()
        {
            if (!Application.isPlaying) { Stop("play mode ended"); return; }
            if (EditorApplication.timeSinceStartup < _settleUntil) return;
            var st = _steps[_i];
            try
            {
                if (!_acted) { st.act?.Invoke(); _acted = true; _t = EditorApplication.timeSinceStartup; }
                if (st.until != null && !st.until()) { _t = EditorApplication.timeSinceStartup; return; }
                if (EditorApplication.timeSinceStartup - _t < st.wait) return;
                if (st.name != null) { Capture(st.name); _settleUntil = EditorApplication.timeSinceStartup + 2.0; }
            }
            catch (Exception e) { Log.AppendLine($"  step {st.name ?? _i.ToString()} failed: {e.GetBaseException().Message}"); }
            _i++; _acted = false;
            if (_i >= _steps.Count) Stop("done");
        }

        static void Stop(string why)
        {
            EditorApplication.update -= Tick;
            _steps = null;
            Log.AppendLine($"[UiTour] END ({why})");
            if (_dir != null) System.IO.File.WriteAllText(System.IO.Path.Combine(_dir, "report.txt"), Log.ToString());
            Debug.Log(Log.ToString());
        }

        static void Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(_dir, name + ".png"));
            var bad = new List<string>();
            foreach (var img in UnityEngine.Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
            {
                if (!img.isActiveAndEnabled) continue;
                if (UiAudit.MisSliced(img, out var why))
                    bad.Add($"    {UiAudit.PathOf(img.transform, null)} [{img.sprite.name}] {why}");
            }
            var nested = new List<string>();
            var dark = new List<string>();
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || !canvas.isActiveAndEnabled) continue;
                nested.AddRange(UiAudit.NestedRadiusIssues(canvas.transform));
                dark.AddRange(UiAudit.DarkOutlineText(canvas.transform));
            }
            Log.AppendLine($"{name}: misSliced={bad.Count} nestedRadius={nested.Distinct().Count()} darkOutlineText={dark.Distinct().Count()}");
            foreach (var l in bad.Distinct()) Log.AppendLine(l);
            foreach (var l in nested.Distinct()) Log.AppendLine("  [radius]" + l);
            foreach (var l in dark.Distinct()) Log.AppendLine("  [text]" + l);
        }
    }
}
