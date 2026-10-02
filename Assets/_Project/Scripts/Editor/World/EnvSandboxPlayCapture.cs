using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Captures the Env Sandbox in Play mode (2026-10-01). Water, lava and toxic read the camera depth
    /// texture, which URP does not produce for a manual Camera.Render in Edit mode, so the zones are
    /// shot the way the game renders: the sandbox scene plays, each camera takes the screen in turn,
    /// then the editor returns to the scene it had open. The sandbox is a dev scene; nothing of the
    /// game flow runs in it.
    /// </summary>
    [InitializeOnLoad]
    public static class EnvSandboxPlayCapture
    {
        const string Flag = "zw.env.capture", Phase = "zw.env.capture.phase", Prev = "zw.env.capture.prev", OutDir = "Review/M8/env_sandbox/";
        // A shot: which camera, the file, and (for the pathing demo) how many seconds after the
        // demo restarts it is taken.
        struct Shot { public Camera cam; public string file; public float at; }
        static List<Shot> _cams;
        static int _index, _waitUntil;
        static bool _captured;
        static float _demoStart = -1f;
        static string _statsFor;
        static readonly float[] DemoTimes = { 0.5f, 6f, 12f, 20f };

        static EnvSandboxPlayCapture() => EditorApplication.update += Tick;

        [MenuItem("HordeCall/World/Capture Env Sandbox (Play)")]
        public static string Start()
        {
            if (EditorApplication.isPlaying) return "already playing";
            // Never raise a save dialog from an automated run: it blocks the editor. Unsaved scenes stop it.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) return "an open scene has unsaved changes; save or reload it first";
            SessionState.SetString(Prev, EditorSceneManager.GetActiveScene().path);
            EditorSceneManager.OpenScene(EnvSandboxBuilder.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(Flag, true);
            _cams = null;
            EditorApplication.EnterPlaymode();
            return "capturing";
        }

        static void Tick()
        {
            // Runs on every editor update: the cheap flag first, and no string built per call.
            if (!SessionState.GetBool(Flag, false)) return;
            int phase = SessionState.GetInt(Phase, 0);
            if (!EditorApplication.isPlaying)
            {
                if (phase != 2 || EditorApplication.isPlayingOrWillChangePlaymode) return;
                // Back in Edit mode after the run: restore the scene that was open.
                SessionState.SetBool(Flag, false);
                SessionState.SetInt(Phase, 0);
                string prev = SessionState.GetString(Prev, "");
                if (!string.IsNullOrEmpty(prev) && File.Exists(prev)) EditorSceneManager.OpenScene(prev, OpenSceneMode.Single);
                Debug.Log("[EnvCapture] done");
                return;
            }
            if (_cams == null)
            {
                Directory.CreateDirectory(OutDir);
                File.AppendAllText(OutDir + "stats.csv", "shot,batches,setpass,triangles,vertices\n");
                var all = new List<Camera>(Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None));
                all.RemoveAll(c => !c.name.StartsWith("Cam_"));
                all.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                _cams = new List<Shot>();
                foreach (var c in all)
                {
                    c.enabled = false;
                    if (!c.name.StartsWith("Cam_Nav_")) _cams.Add(new Shot { cam = c, file = c.name.Replace("Cam_", ""), at = -1f });
                }
                // Pathing demo shots last, at fixed times after the crowd restarts.
                foreach (float t in DemoTimes)
                    foreach (var c in all)
                        if (c.name.StartsWith("Cam_Nav_")) _cams.Add(new Shot { cam = c, file = c.name.Replace("Cam_", "") + "_t" + Mathf.RoundToInt(t), at = t });
                _demoStart = -1f;
                _index = -1;
                _waitUntil = Time.frameCount + 30;   // let the scene settle (shaders, particles)
                return;
            }
            if (Time.frameCount < _waitUntil) return;
            if (_index >= _cams.Count)
            {
                SessionState.SetInt(Phase, 2);
                _cams = null;
                EditorApplication.ExitPlaymode();
                return;
            }
            // A screenshot is taken at the end of the frame it is asked in, so the camera must not
            // change until a couple of frames later.
            if (_index >= 0 && !_captured)
            {
                var shot = _cams[_index];
                if (shot.at >= 0f)
                {
                    if (_demoStart < 0f)
                    {
                        foreach (var d in Object.FindObjectsByType<ZombieWar.WorldNav.EnvNavDemo>(FindObjectsSortMode.None)) d.Restart();
                        _demoStart = Time.time;
                    }
                    if (Time.time - _demoStart < shot.at) return;
                }
                ScreenCapture.CaptureScreenshot(OutDir + shot.file + ".png");
                _statsFor = shot.file;
                _captured = true;
                _waitUntil = Time.frameCount + 3;
                return;
            }
            // Render stats of the frame just shown (game view), one line per shot.
            if (_statsFor != null)
            {
                File.AppendAllText(OutDir + "stats.csv", $"{_statsFor},{UnityEditor.UnityStats.batches},{UnityEditor.UnityStats.setPassCalls},{UnityEditor.UnityStats.triangles},{UnityEditor.UnityStats.vertices}\n");
                _statsFor = null;
            }
            _captured = false;
            _index++;
            foreach (var c in _cams) c.cam.enabled = false;
            if (_index < _cams.Count) _cams[_index].cam.enabled = true;
            _waitUntil = Time.frameCount + 6;
        }
    }
}
