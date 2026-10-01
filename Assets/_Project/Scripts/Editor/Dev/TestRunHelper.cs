using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Runs a filtered set of tests from code (agents, automation) and writes a plain summary to
    /// Temp/test_results.txt when the run finishes.
    /// </summary>
    public static class TestRunHelper
    {
        const string Out = "Temp/test_results.txt";

        public static string Run(TestMode mode, params string[] groupNames)
        {
            if (File.Exists(Out)) File.Delete(Out);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Writer());
            api.Execute(new ExecutionSettings(new Filter { testMode = mode, groupNames = groupNames }));
            return "started";
        }

        sealed class Writer : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new StringBuilder($"passed {result.PassCount}, failed {result.FailCount}, skipped {result.SkipCount}, inconclusive {result.InconclusiveCount}\n");
                Collect(result, sb);
                File.WriteAllText(Out, sb.ToString());
            }

            static void Collect(ITestResultAdaptor r, StringBuilder sb)
            {
                if (!r.HasChildren && r.TestStatus == TestStatus.Failed)
                    sb.AppendLine($"FAIL {r.FullName}: {r.Message}");
                if (r.HasChildren) foreach (var c in r.Children) Collect(c, sb);
            }
        }
    }
}
