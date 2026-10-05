using NUnit.Framework;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.Tests
{
    /// <summary>Backlog #14a: the run's kill-based score and its record (owner 27/09).</summary>
    public class RunScoreTests
    {
        static ZombieData Enemy(bool elite)
        {
            var d = ScriptableObject.CreateInstance<ZombieData>();
            d.isElite = elite;
            return d;
        }

        [Test]
        public void KillsScoreTenAndElitesFifty()
        {
            var run = RunState.Begin();
            var normal = Enemy(false); var elite = Enemy(true);
            run.RecordKill(normal, bankCoin: false, grantXp: false);
            run.RecordKill(normal, bankCoin: false, grantXp: false);
            run.RecordKill(elite, bankCoin: false, grantXp: false);
            Assert.AreEqual(70, run.Score);
            Assert.AreEqual(70, run.Snapshot().Score);
            RunState.Abandon();
            Object.DestroyImmediate(normal); Object.DestroyImmediate(elite);
        }

        [Test]
        public void TheResultPillNamesTheRecord()
        {
            Assert.AreEqual("NEW BEST · 4,120", RunEndV2.ScoreLine(4120, 4120, true, false));
            Assert.AreEqual("SCORE 900 · BEST 4,120", RunEndV2.ScoreLine(900, 4120, false, false));
            Assert.AreEqual("FIRST RUN · SCORE 300", RunEndV2.ScoreLine(300, 300, true, true));
        }
    }
}
