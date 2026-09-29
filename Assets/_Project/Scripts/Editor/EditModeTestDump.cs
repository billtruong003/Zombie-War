using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>Runs every EditMode test and writes "pass/fail/skip" plus each failure to a text
    /// file, so an agent without the Test Runner window can read the result.</summary>
    public static class EditModeTestDump
    {
        public static void Run(string outPath)
        {
            System.IO.File.WriteAllText(outPath, "running");
            string note = SaveScenesThatOnlyLookDirty();
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks(outPath, note));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        /// <summary>
        /// The test runner asks, in a modal dialog, whether to save dirty scenes, and nobody is there
        /// to answer when an agent starts the run. Edit-time UI components ([ExecuteAlways], e.g.
        /// UIBarClip re-applying the same anchors on reload) flag Bootstrap dirty without changing it.
        /// A scene whose in-memory copy is byte-identical to the file on disk is saved (a no-op for
        /// git); a scene with real unsaved edits is left alone and named in the result.
        /// </summary>
        static string SaveScenesThatOnlyLookDirty()
        {
            var note = new StringBuilder();
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !scene.isDirty || string.IsNullOrEmpty(scene.path)) continue;
                const string probe = "Temp/zw_scene_probe.unity";
                if (!EditorSceneManager.SaveScene(scene, probe, true)) continue;
                bool same = Normalize(System.IO.File.ReadAllText(probe)) == Normalize(System.IO.File.ReadAllText(scene.path));
                System.IO.File.Delete(probe);
                if (same) EditorSceneManager.SaveScene(scene);
                else note.AppendLine($"note: {scene.path} has real unsaved changes; the test runner may ask to save it");
            }
            return note.ToString();
        }

        static string Normalize(string s) => s.Replace("\r\n", "\n");

        class Callbacks : ICallbacks
        {
            readonly string _path, _note;
            public Callbacks(string path, string note) { _path = path; _note = note; }
            public void RunStarted(ITestAdaptor t) { }
            public void TestStarted(ITestAdaptor t) { }
            public void TestFinished(ITestResultAdaptor r) { }

            public void RunFinished(ITestResultAdaptor r)
            {
                var sb = new StringBuilder($"pass {r.PassCount} fail {r.FailCount} skip {r.SkipCount}\n");
                void Walk(ITestResultAdaptor x)
                {
                    if (!x.HasChildren && x.TestStatus == TestStatus.Failed) sb.AppendLine(x.FullName + ": " + x.Message);
                    if (x.HasChildren) foreach (var c in x.Children) Walk(c);
                }
                Walk(r);
                sb.Append(_note);
                System.IO.File.WriteAllText(_path, sb.ToString());
            }
        }
    }
}
