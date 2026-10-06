using NUnit.Framework;
using UnityEngine;
using ZombieWar.WorldNav;

namespace ZombieWar.Tests
{
    public class FlowFieldTests
    {
        GameObject _wall;

        [TearDown]
        public void TearDown()
        {
            if (_wall != null) Object.DestroyImmediate(_wall);
        }

        static FlowField Make() => new FlowField(24f, 0.5f, LayerMask.GetMask("NavObstacle"));

        void AddWall()
        {
            // A wall across the field with a gap at one end: the routed answer differs from "go straight".
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.layer = LayerMask.NameToLayer("NavObstacle");
            _wall.transform.position = new Vector3(-2f, 0f, 4f);
            _wall.transform.localScale = new Vector3(16f, 2f, 1f);
            Physics.SyncTransforms();
        }

        static void AssertSameAnswers(FlowField a, FlowField b)
        {
            for (float x = -10f; x <= 10f; x += 1.7f)
                for (float z = -10f; z <= 10f; z += 1.7f)
                {
                    var p = new Vector3(x, 0f, z);
                    Assert.AreEqual(a.Direction(p), b.Direction(p), $"direction at {p}");
                }
        }

        [Test]
        public void SlicedSolve_MatchesTheOneGoSolve()
        {
            AddWall();
            var target = new Vector3(0f, 0f, 9f);
            var whole = Make(); whole.Rasterize(Vector3.zero); whole.Solve(target);
            var sliced = Make(); sliced.Rasterize(Vector3.zero);
            sliced.BeginSolve(target);
            int frames = 0;
            while (!sliced.StepSolve(150)) { frames++; Assert.Less(frames, 10000); }
            Assert.Greater(frames, 1, "the budget should spread the solve over several steps");
            Assert.IsTrue(sliced.HasSolution);
            Assert.AreEqual(target, sliced.SolvedTarget);
            AssertSameAnswers(whole, sliced);
        }

        [Test]
        public void DuringASlicedSolve_ThePreviousSolutionStillAnswers()
        {
            var field = Make(); field.Rasterize(Vector3.zero);
            var first = new Vector3(0f, 0f, 9f);
            field.Solve(first);
            var probe = new Vector3(0f, 0f, -5f);
            Vector3 before = field.Direction(probe);

            field.BeginSolve(new Vector3(0f, 0f, -9f));
            field.StepSolve(10);
            Assert.IsTrue(field.Solving);
            Assert.AreEqual(before, field.Direction(probe));
            Assert.AreEqual(first, field.SolvedTarget);
        }

        [Test]
        public void Rasterize_CancelsASolveInProgress()
        {
            var field = Make(); field.Rasterize(Vector3.zero);
            field.BeginSolve(Vector3.forward);
            field.Rasterize(new Vector3(3f, 0f, 3f));
            Assert.IsFalse(field.Solving);
            Assert.IsFalse(field.HasSolution);
            Assert.IsFalse(field.StepSolve(100));
        }
    }
}
