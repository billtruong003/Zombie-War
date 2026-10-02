using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Automated in-game screenshots (2026-10-01): plays from Bootstrap, starts a run on each listed
    /// map theme, keeps the run going (god mode, level-up cards auto-picked), takes a shot and the
    /// frame's render stats after a few seconds, then moves to the next theme and finally leaves Play.
    /// Output: Review/M8/game_shots/&lt;theme&gt;.png and stats.csv. No scene is saved.
    /// </summary>
    [InitializeOnLoad]
    public static class GameShot
    {
        const string Key = "zw.gameshot.queue", DebugKey = "zw.gameshot.debug", Out = "Review/M8/game_shots/";
        // Outline debug views shot after the plain frame when RunDebug started the queue:
        // depth, normals (normal prepass forced on), selection mask, edge only.
        static readonly (string name, int mode, bool normals)[] DebugViews =
            { ("depth", 1, false), ("normals", 2, true), ("mask", 5, false), ("edges", 4, false) };
        static int _view;
        const float SettleSeconds = 14f;
        static float _startedAt = -1f, _shotAt = -1f;
        static int _step;

        // Mirrored from SessionState so the idle Tick allocates nothing (see LookShot).
        static string _queue;

        static GameShot()
        {
            _queue = SessionState.GetString(Key, "");
            EditorApplication.update += Tick;
        }

        static void SetQueue(string q) { _queue = q; SessionState.SetString(Key, q); }

        public static string Run(params string[] themes)
        {
            if (EditorApplication.isPlaying) return "already playing";
            Directory.CreateDirectory(Out);
            SetQueue(string.Join(",", themes));
            SessionState.SetBool(DebugKey, false);
            File.WriteAllText(Out + "stats.csv", "theme,batches,setpass,triangles,renderMs,enemies\n");
            Begin();
            return "running " + string.Join(",", themes);
        }

        /// <summary>Same as <see cref="Run"/>, plus the outline debug views of each theme
        /// (&lt;theme&gt;_depth/_normals/_mask/_edges.png) for checking what reaches each buffer.</summary>
        public static string RunDebug(params string[] themes)
        {
            string r = Run(themes);
            SessionState.SetBool(DebugKey, true);
            return r;
        }

        static List<string> Queue() => new(_queue.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));

        static void Begin()
        {
            var q = Queue();
            if (q.Count == 0) return;
            PlayerPrefs.SetString("zw.map.theme", q[0]); PlayerPrefs.Save();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            _step = 0; _startedAt = -1f;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (string.IsNullOrEmpty(_queue)) return;
            var q = Queue();
            if (q.Count == 0) return;
            if (!EditorApplication.isPlaying)
            {
                if (_step == 4 && !EditorApplication.isPlayingOrWillChangePlaymode) { _step = 0; Begin(); }
                return;
            }
            float now = Time.realtimeSinceStartup;
            switch (_step)
            {
                case 0:   // wait for the menu, then start the run
                    if (_startedAt < 0f) _startedAt = now;
                    if (now - _startedAt < 8f || !BillGameCore.Bill.IsReady) return;
                    GameFlow.StartGameplay();
                    _step = 1; _startedAt = now;
                    return;
                case 1:   // run under way: god mode once, then keep cards picked
                    if (now - _startedAt < 4f) return;
                    BillGameCore.Bill.Cheat?.Execute("zw.god");
                    _step = 2; _startedAt = now;
                    return;
                case 2:
                    KeepPlaying();
                    if (now - _startedAt < SettleSeconds) return;
                    ScreenCapture.CaptureScreenshot(Out + q[0] + ".png");
                    _shotAt = now; _step = 3; _view = 0;
                    return;
                case 3:
                    KeepPlaying();
                    if (now - _shotAt < 0.5f) return;
                    // Even ticks switch the view, odd ticks shoot it: the capture lands at the end of
                    // the frame, so switching in the same tick would shoot the next view instead.
                    if (SessionState.GetBool(DebugKey, false) && _view <= DebugViews.Length * 2)
                    {
                        int i = _view / 2;
                        if (_view % 2 == 1) ScreenCapture.CaptureScreenshot($"{Out}{q[0]}_{DebugViews[i].name}.png");
                        else SetOutlineDebug(i < DebugViews.Length ? DebugViews[i] : ("", 0, false));
                        _view++; _shotAt = now;
                        return;
                    }
                    File.AppendAllText(Out + "stats.csv", $"{q[0]},{UnityEditor.UnityStats.batches},{UnityEditor.UnityStats.setPassCalls},{UnityEditor.UnityStats.triangles},{UnityEditor.UnityStats.renderTime * 1000f:F1},{PlanarSteeringWorld.AgentCount}\n");
                    q.RemoveAt(0);
                    SetQueue(string.Join(",", q));
                    _step = 4;
                    EditorApplication.ExitPlaymode();
                    if (q.Count == 0) Debug.Log("[GameShot] done");
                    return;
            }
        }

        // Writes only to the play-session clone of each volume profile (OutlineProfileRuntimeGuard),
        // so the outline asset on disk never changes. Reflection: the outline lives in Assembly-CSharp,
        // which this editor assembly cannot reference.
        static bool? _origNormals;
        static void SetOutlineDebug((string name, int mode, bool normals) v)
        {
            foreach (var vol in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
            {
                if (!vol.HasInstantiatedProfile()) continue;
                var o = vol.profile.components.Find(c => c != null && c.GetType().Name == "OutlineVolume");
                if (o == null) continue;
                var t = o.GetType();
                var dbg = t.GetField("debugMode").GetValue(o);
                var norm = (UnityEngine.Rendering.VolumeParameter<bool>)t.GetField("useNormals").GetValue(o);
                _origNormals ??= norm.value;
                var dbgType = dbg.GetType();
                dbgType.GetMethod("Override", new[] { dbgType.GetGenericArguments()[0] })
                       .Invoke(dbg, new[] { System.Enum.ToObject(dbgType.GetGenericArguments()[0], v.mode) });
                norm.Override(v.normals || _origNormals.Value);
            }
            if (v.mode == 0) _origNormals = null;
        }

        static void KeepPlaying()
        {
            if (Time.timeScale != 0f) return;
            var o = Object.FindFirstObjectByType<RunOverlays>();
            var pick = typeof(RunOverlays).GetMethod("PickPerk", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (o != null && pick != null) pick.Invoke(o, new object[] { 0 });
        }
    }
}
