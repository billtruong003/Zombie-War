using System.Text;
using UnityEditor;
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
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks(outPath));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        class Callbacks : ICallbacks
        {
            readonly string _path;
            public Callbacks(string path) => _path = path;
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
                System.IO.File.WriteAllText(_path, sb.ToString());
            }
        }
    }
}
