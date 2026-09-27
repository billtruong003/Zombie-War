#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar.Dev
{
    /// <summary>
    /// M8 user-scenario audit, run inside a development build (owner: check from a real build, not
    /// through the editor). Start the player with <c>-zwtour &lt;folder&gt;</c>; without it this does
    /// nothing. A standalone build keeps its own PlayerPrefs, so the tour starts from a brand-new
    /// save, which is exactly the first-launch scenario.
    ///
    /// For every menu screen it captures the screen, lists every button with its handler count, then
    /// taps each button once and records what appeared (screen change, newly shown panels). Then it
    /// plays a run through the real PLAY button, captures the HUD, a level-up, pause and the result,
    /// and goes HOME. Everything lands in &lt;folder&gt;/report.txt plus one PNG per step.
    /// </summary>
    public sealed class BuildTour : MonoBehaviour
    {
        string _dir;
        bool _runOnly;   // ?zwtour=run: skip the menu sweep and never pause
        readonly StringBuilder _log = new();
        int _shot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string dir = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: open the page with ?zwtour. No files: the report goes to the browser console and
            // each screenshot step waits for the N key so whoever drives the browser can capture it.
            if (string.IsNullOrEmpty(Application.absoluteURL) || !Application.absoluteURL.Contains("zwtour")) return;
#else
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-zwtour");
            if (i < 0 || i + 1 >= args.Length) return;
            dir = args[i + 1];
#endif
            var go = new GameObject("~BuildTour");
            DontDestroyOnLoad(go);
            var tour = go.AddComponent<BuildTour>();
            tour._dir = dir;
            tour._runOnly = (Application.absoluteURL ?? "").Contains("zwtour=run") || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-zwtour-run") >= 0;
        }

        IEnumerator Start()
        {
            if (_dir != null) System.IO.Directory.CreateDirectory(_dir);
            _log.AppendLine($"[BuildTour] {System.DateTime.Now:yyyy-MM-dd HH:mm} screen {Screen.width}x{Screen.height} v{Application.version}");
            float t0 = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => UIManager.Instance != null && UIManager.Instance.Top != null || Time.realtimeSinceStartup - t0 > 30f);
            _log.AppendLine($"boot to first screen: {Time.realtimeSinceStartup - t0:0.0}s (top={UIManager.Instance?.Top?.GetType().Name ?? "none"})");
            yield return new WaitForSecondsRealtime(3f);   // loader fade + first-launch popups
            _log.AppendLine("at boot: " + CanvasState());
            yield return Shot("first_launch");
            Section("FIRST LAUNCH WALLET", Wallet());

            if (!_runOnly)
            foreach (var screen in new[] { typeof(HubScreen), typeof(LoadoutScreen), typeof(ShopScreen), typeof(CostumeScreen), typeof(PassScreen) })
                yield return ExploreScreen(screen);

            yield return PlayRun();
            Section("END WALLET", Wallet());
            _log.AppendLine("[BuildTour] END");
            if (_dir != null) System.IO.File.WriteAllText(System.IO.Path.Combine(_dir, "report.txt"), _log.ToString());
            foreach (var chunk in Chunks(_log.ToString(), 3000)) Debug.Log("[BuildTour.report]\n" + chunk);
            yield return new WaitForSecondsRealtime(1f);
            if (_dir != null) Application.Quit();
        }

        // ───────────────────────────────────────────── menu screens
        IEnumerator ExploreScreen(System.Type type)
        {
            yield return OpenScreen(type);
            var screen = UIManager.Instance?.Top;
            if (screen == null || screen.GetType() != type) { _log.AppendLine($"\n## {type.Name}: could not open"); yield break; }
            _log.AppendLine($"\n## {type.Name}");
            yield return Shot(type.Name);
            var buttons = Buttons(screen.transform);
            _log.AppendLine($"buttons: {buttons.Count}");
            int n = 0;
            foreach (var path in buttons.Select(b => UIPath(b.transform, screen.transform)).ToList())
            {
                if (n++ >= 30) { _log.AppendLine("  (more buttons not tapped)"); break; }
                yield return OpenScreen(type);
                var root = UIManager.Instance.Top.transform;
                var btn = root.Find(path)?.GetComponent<Selectable>();
                if (btn == null || !btn.IsActive() || !btn.IsInteractable()) { _log.AppendLine($"  - {path}: hidden or disabled after reopening"); continue; }
                int handlers = HandlerCount(btn);
                if ((Label(btn) ?? btn.name).ToUpperInvariant().Contains("PLAY")) { _log.AppendLine($"  - {path} \"{Label(btn)}\" handlers={handlers} -> starts a run (checked in RUN below)"); continue; }
                var before = ActiveSet();
                var topBefore = UIManager.Instance.Top;
                Press(btn);
                yield return new WaitForSecondsRealtime(0.8f);
                if (RunState.Current != null && !RunState.Current.IsOver)
                {
                    _log.AppendLine($"  - {path} \"{Label(btn)}\" handlers={handlers} -> started a run; back to the menu");
                    GameFlow.ReturnToMenu();
                    yield return new WaitForSecondsRealtime(4f);
                    continue;
                }
                var topAfter = UIManager.Instance?.Top;
                var appeared = ActiveSet().Except(before).Where(p => p.Split('/').Length <= 6).Take(5).ToList();
                string what = topAfter != topBefore ? $"-> screen {topAfter?.GetType().Name ?? "none"}" :
                              appeared.Count > 0 ? "-> shows " + string.Join(", ", appeared.Select(Short)) : "-> no visible change";
                string label = Label(btn);
                _log.AppendLine($"  - {path}{(label != null ? $" \"{label}\"" : "")} handlers={handlers} {what}");
                if (topAfter != topBefore || appeared.Count > 0) yield return Shot($"{type.Name}__{Safe(path)}", false);
            }
        }

        IEnumerator OpenScreen(System.Type type)
        {
            var ui = UIManager.Instance;
            if (ui == null) yield break;
            ui.Replace<HubScreen>();
            if (type != typeof(HubScreen) && Object.FindFirstObjectByType(type, FindObjectsInactive.Include) is UIScreen screen)
                ui.Push(screen);
            yield return new WaitForSecondsRealtime(0.6f);
        }

        // ───────────────────────────────────────────── a real run
        IEnumerator PlayRun()
        {
            _log.AppendLine("\n## RUN");
            yield return OpenScreen(typeof(HubScreen));
            var play = Buttons(UIManager.Instance.Top.transform).FirstOrDefault(b => (Label(b) ?? b.name).ToUpperInvariant().Contains("PLAY"));
            if (play == null) { _log.AppendLine("no PLAY button on the Hub"); yield break; }
            float t0 = Time.realtimeSinceStartup;
            Press(play);
            yield return new WaitUntil(() => RunState.Current != null || Time.realtimeSinceStartup - t0 > 30f);
            _log.AppendLine($"PLAY to run: {Time.realtimeSinceStartup - t0:0.0}s");
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("run_start");
            var overlays = Object.FindFirstObjectByType<RunOverlays>(FindObjectsInactive.Include);
            // Stand still and let the run happen; capture the first level-up the game offers.
            float until = Time.realtimeSinceStartup + 90f;
            bool levelShot = false;
            while (Time.realtimeSinceStartup < until && RunState.Current != null && !RunState.Current.IsOver)
            {
                var lvl = overlays != null ? Field<GameObject>(overlays, "levelUpRoot") : null;
                if (lvl != null && lvl.activeInHierarchy)
                {
                    if (!levelShot) { levelShot = true; yield return Shot("run_levelup"); _log.AppendLine($"first level-up at {RunState.Current.Duration:0}s"); }
                    var card = lvl.transform.Find("Perk0")?.GetComponentInChildren<Button>();
                    if (card != null) Press(card);
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            if (RunState.Current == null) yield break;
            _log.AppendLine($"after watching: t={RunState.Current.Duration:0}s kills={RunState.Current.Kills} coin={RunState.Current.Coin} level={RunState.Current.Level} over={RunState.Current.IsOver}");
            yield return Shot("run_hud");
            if (!RunState.Current.IsOver && overlays != null)
            {
                overlays.ShowPause();
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("run_pause");
                _log.AppendLine("pause buttons: " + string.Join(", ", Buttons(overlays.transform).Select(b => Label(b) ?? b.name)));
                overlays.GetType().GetMethod("EndRun", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(overlays, null);
            }
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("run_result");
            if (overlays != null)
            {
                var resultButtons = Buttons(overlays.transform);
                _log.AppendLine("result buttons: " + string.Join(", ", resultButtons.Select(b => $"{Label(b) ?? b.name}(handlers={HandlerCount(b)})")));
                var home = resultButtons.FirstOrDefault(b => (Label(b) ?? b.name).ToUpperInvariant().Contains("HOME"));
                if (home != null)
                {
                    t0 = Time.realtimeSinceStartup;
                    Press(home);
                    yield return new WaitUntil(() => UIManager.Instance != null && UIManager.Instance.Top != null && RunState.Current == null || Time.realtimeSinceStartup - t0 > 20f);
                    _log.AppendLine($"HOME to hub: {Time.realtimeSinceStartup - t0:0.0}s");
                    yield return new WaitForSecondsRealtime(2f);
                    var hub = UIManager.Instance?.Top;
                    var cg = hub != null ? hub.GetComponent<CanvasGroup>() : null;
                    var menu = UnityEngine.SceneManagement.SceneManager.GetSceneByName(GameFlow.MenuScene);
                    _log.AppendLine("after HOME: " + CanvasState());
                    _log.AppendLine($"after HOME: top={hub?.GetType().Name ?? "none"} shown={(hub != null && hub.gameObject.activeInHierarchy)} alpha={(cg != null ? cg.alpha : -1):0.00} " +
                        $"menu={(menu.isLoaded ? string.Join(",", menu.GetRootGameObjects().Select(r => r.name + (r.activeSelf ? "+" : "-"))) : "not loaded")} " +
                        $"cams={string.Join(",", Camera.allCameras.Select(c => c.gameObject.scene.name + "/" + c.name))} active={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
                        $"loader={(LoadingScreen.Instance != null ? LoadingScreen.Instance.GetComponentInChildren<CanvasGroup>(true)?.alpha.ToString("0.00") : "none")}");
                    yield return Shot("hub_after_run");
                }
            }
        }

        // ───────────────────────────────────────────── helpers
        IEnumerator Shot(string name, bool key = true)
        {
            yield return new WaitForEndOfFrame();
            string file = $"{++_shot:00}_{name}";
            _log.AppendLine($"  [shot] {file}");
            if (_dir != null)
            {
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(_dir, file + ".png"), 2);
                yield return null;
                yield break;
            }
            if (!key || _runOnly) yield break;   // WebGL: per-button taps are logged, not paused on
            Debug.Log($"[BuildTour] SHOT {file} (press N to continue)");
            float until = Time.realtimeSinceStartup + 90f;
            while (!Input.GetKeyDown(KeyCode.N) && Time.realtimeSinceStartup < until) yield return null;
            yield return null;
        }

        static IEnumerable<string> Chunks(string s, int size)
        {
            for (int i = 0; i < s.Length; i += size) yield return s.Substring(i, Mathf.Min(size, s.Length - i));
        }

        void Section(string title, string body) => _log.AppendLine($"\n## {title}\n{body}");

        static string CanvasState()
        {
            var ui = UIManager.Instance;
            if (ui == null) return "no UIManager";
            var canvas = ui.GetComponent<Canvas>();
            var safe = ui.Top != null ? ui.Top.transform.Find("Safe") as RectTransform : null;
            return $"screen={Screen.width}x{Screen.height} scaleFactor={(canvas != null ? canvas.scaleFactor : -1):0.000} root={((RectTransform)ui.transform).rect.size} safe={(safe != null ? safe.rect.size.ToString() : "-")} safeArea={Screen.safeArea}";
        }

        static string Wallet() =>
            $"coin={PlayerProfile.Coin} gem={PlayerProfile.Gem} best={PlayerProfile.BestSurvivalSeconds:0}s";

        static List<Selectable> Buttons(Transform root) =>
            root.GetComponentsInChildren<Selectable>(false).Where(s => (s is Button || s is Toggle) && s.IsActive() && s.IsInteractable()).ToList();

        static void Press(Selectable s)
        {
            if (s is Button b) b.onClick.Invoke();
            else if (s is Toggle t) t.isOn = !t.isOn;
        }

        static int HandlerCount(Selectable s)
        {
            UnityEngine.Events.UnityEventBase ev = s is Button b ? b.onClick : s is Toggle t ? t.onValueChanged : null;
            if (ev == null) return 0;
            int persistent = ev.GetPersistentEventCount();
            var calls = typeof(UnityEngine.Events.UnityEventBase).GetField("m_Calls", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(ev);
            var runtime = calls?.GetType().GetField("m_RuntimeCalls", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(calls) as System.Collections.IList;
            return persistent + (runtime?.Count ?? 0);
        }

        static string Label(Component c)
        {
            var t = c.GetComponentInChildren<TMPro.TMP_Text>(false);
            if (t == null || string.IsNullOrWhiteSpace(t.text)) return null;
            var s = System.Text.RegularExpressions.Regex.Replace(t.text, "<.*?>", "").Replace("\n", " ").Trim();
            return s.Length > 28 ? s.Substring(0, 28) : s;
        }

        static HashSet<string> ActiveSet()
        {
            var set = new HashSet<string>();
            var root = UIManager.Instance != null ? UIManager.Instance.transform : null;
            if (root == null) return set;
            foreach (var t in root.GetComponentsInChildren<RectTransform>(false)) set.Add(UIPath(t, root));
            return set;
        }

        static string UIPath(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var c = t; c != null && c != root; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static string Short(string path) { var p = path.Split('/'); return p.Length <= 2 ? path : string.Join("/", p.Skip(p.Length - 2)); }
        static string Safe(string path) => System.Text.RegularExpressions.Regex.Replace(path.Length > 40 ? path.Substring(path.Length - 40) : path, "[^A-Za-z0-9]+", "_");

        static T Field<T>(object o, string name) where T : class =>
            o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(o) as T;
    }
}
#endif
