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
        const string Key = "zw.gameshot.queue", Out = "Review/M8/game_shots/";
        const float SettleSeconds = 14f;
        static float _startedAt = -1f, _shotAt = -1f;
        static int _step;

        static GameShot() => EditorApplication.update += Tick;

        public static string Run(params string[] themes)
        {
            if (EditorApplication.isPlaying) return "already playing";
            Directory.CreateDirectory(Out);
            SessionState.SetString(Key, string.Join(",", themes));
            File.WriteAllText(Out + "stats.csv", "theme,batches,setpass,triangles,renderMs,enemies\n");
            Begin();
            return "running " + string.Join(",", themes);
        }

        static List<string> Queue() => new(SessionState.GetString(Key, "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));

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
                    _shotAt = now; _step = 3;
                    return;
                case 3:
                    KeepPlaying();
                    if (now - _shotAt < 0.5f) return;
                    File.AppendAllText(Out + "stats.csv", $"{q[0]},{UnityEditor.UnityStats.batches},{UnityEditor.UnityStats.setPassCalls},{UnityEditor.UnityStats.triangles},{UnityEditor.UnityStats.renderTime * 1000f:F1},{PlanarSteeringWorld.AgentCount}\n");
                    q.RemoveAt(0);
                    SessionState.SetString(Key, string.Join(",", q));
                    _step = 4;
                    EditorApplication.ExitPlaymode();
                    if (q.Count == 0) Debug.Log("[GameShot] done");
                    return;
            }
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
