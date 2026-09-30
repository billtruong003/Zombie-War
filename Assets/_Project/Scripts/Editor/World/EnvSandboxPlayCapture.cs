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
        const string Flag = "zw.env.capture", Prev = "zw.env.capture.prev", OutDir = "Review/M8/env_sandbox/";
        static List<Camera> _cams;
        static int _index, _waitUntil;
        static bool _captured;

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
            int phase = SessionState.GetInt(Flag + ".phase", 0);
            if (!SessionState.GetBool(Flag, false)) return;
            if (!EditorApplication.isPlaying)
            {
                if (phase != 2 || EditorApplication.isPlayingOrWillChangePlaymode) return;
                // Back in Edit mode after the run: restore the scene that was open.
                SessionState.SetBool(Flag, false);
                SessionState.SetInt(Flag + ".phase", 0);
                string prev = SessionState.GetString(Prev, "");
                if (!string.IsNullOrEmpty(prev) && File.Exists(prev)) EditorSceneManager.OpenScene(prev, OpenSceneMode.Single);
                Debug.Log("[EnvCapture] done");
                return;
            }
            if (_cams == null)
            {
                Directory.CreateDirectory(OutDir);
                _cams = new List<Camera>(Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None));
                _cams.RemoveAll(c => !c.name.StartsWith("Cam_"));
                _cams.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                foreach (var c in _cams) c.enabled = false;
                _index = -1;
                _waitUntil = Time.frameCount + 30;   // let the scene settle (shaders, particles)
                return;
            }
            if (Time.frameCount < _waitUntil) return;
            if (_index >= _cams.Count)
            {
                SessionState.SetInt(Flag + ".phase", 2);
                _cams = null;
                EditorApplication.ExitPlaymode();
                return;
            }
            // A screenshot is taken at the end of the frame it is asked in, so the camera must not
            // change until a couple of frames later.
            if (_index >= 0 && !_captured)
            {
                ScreenCapture.CaptureScreenshot(OutDir + _cams[_index].name.Replace("Cam_", "") + ".png");
                _captured = true;
                _waitUntil = Time.frameCount + 3;
                return;
            }
            _captured = false;
            _index++;
            foreach (var c in _cams) c.enabled = false;
            if (_index < _cams.Count) _cams[_index].enabled = true;
            _waitUntil = Time.frameCount + 6;
        }
    }
}
