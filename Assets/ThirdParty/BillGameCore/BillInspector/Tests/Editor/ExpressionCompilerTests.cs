using NUnit.Framework;
using BillInspector.Editor;

namespace BillInspector.Tests.Editor
{
    public class ExpressionCompilerTests
    {
        private class Target
        {
            public int health = 75;
            public float speed = 3.5f;
            public bool isAlive = true;
            public bool isDead = false;
            public string name = "Hero";
            public TargetType type = TargetType.Melee;

            public bool IsReady() => true;
            public int GetDamage() => 42;
        }

        public enum TargetType { Melee, Ranged, Magic }

        [SetUp]
        public void Setup()
        {
            ExpressionCompiler.ClearCache();
        }

        // ── Literals ──

        [Test]
        public void EvaluateBool_True_ReturnsTrue()
        {
            Assert.IsTrue(ExpressionCompiler.EvaluateBool("true", new Target()));
        }

        [Test]
        public void EvaluateBool_False_ReturnsFalse()
        {
            Assert.IsFalse(ExpressionCompiler.EvaluateBool("false", new Target()));
        }

        [Test]
        public void EvaluateBool_NullExpression_ReturnsFalse()
        {
            Assert.IsFalse(ExpressionCompiler.EvaluateBool(null, new Target()));
        }

        // ── Field access ──

        [Test]
        public void Evaluate_IntField_ReturnsValue()
        {
            var target = new Target();
            Assert.AreEqual(75, ExpressionCompiler.Evaluate("health", target));
        }

        [Test]
        public void Evaluate_BoolField_ReturnsValue()
        {
            Assert.IsTrue(ExpressionCompiler.EvaluateBool("isAlive", new Target()));
        }

        [Test]
        public void Evaluate_StringField_ReturnsValue()
        {
            Assert.AreEqual("Hero", ExpressionCompiler.EvaluateString("name", new Target()));
        }

        // ── Method calls ──

        [Test]
        public void Evaluate_MethodCall_ReturnsResult()
        {
            Assert.IsTrue(ExpressionCompiler.EvaluateBool("IsReady()", new Target()));
        }

        [Test]
        public void Evaluate_IntMethodCall_ReturnsResult()
        {
            Assert.AreEqual(42, ExpressionCompiler.Evaluate("GetDamage()", new Target()));
        }

        // ── Comparisons ──

        [Test]
        public void Evaluate_GreaterThan_True()
        {
            Assert.IsTrue(ExpressionCompiler.EvaluateBool("health > 50", new Target()));
        }

        [Test]
        public void Evaluate_GreaterThan_False()
        {
            Assert.IsFalse(ExpressionCompiler.EvaluateBool("health > 100", new Target()));
        }

        [Test]
        public void Evaluate_LessThan()
        {
            Assert.IsTrue(ExpressionCompiler.EvaluateBool("health < 100", new Target()));
        }

        [Test]
        public void Evaluate_Equality_Int()
        {
            Assert.IsTrue(ExpressionCompiler.EvaluateBool("health == 75", new Target()));
        }

        [Test]
        public void Evaluate_NotEqual()
        {
            Assert.IsTrue(ExpressionCompiler.EvaluateBool("health != 0", new Target()));
        }

        [Test]
        public void Evaluate_GreaterOrEqual()
        {
            Assert.IsTrue(ExpressionCompiler.EvaluateBool("health >= 75", new Target()));
        }

        // ── Boolean operators (precedence: && > ||) ──

        [Test]
        public void Evaluate_And_BothTrue()
        {
            Assert.IsTrue(ExpressionCompiler.EvaluateBool("isAlive && health > 0", new Target()));
        }

        [Test]
        public void Evaluate_And_OneFalse()
        {
            Assert.IsFalse(ExpressionCompiler.EvaluateBool("isDead && health > 0", new Target()));
        }

        [Test]
        public void Evaluate_Or_OneTrue()
        {
            Assert.IsTrue(ExpressionCompiler.EvaluateBool("isDead || isAlive", new Target()));
        }

        [Test]
        public void Evaluate_Or_BothFalse()
        {
            Assert.IsFalse(ExpressionCompiler.EvaluateBool("isDead || health > 200", new Target()));
        }

        [Test]
        public void Evaluate_Precedence_AndBeforeOr()
        {
            // false || (true && true) => true
            // If precedence were wrong (i.e., || evaluated after &&):
            // (false || true) && true => true (same result but for wrong reason)
            // To truly test: true || false && false
            // Correct: true || (false && false) => true
            // Wrong:   (true || false) && false => false
            Assert.IsTrue(ExpressionCompiler.EvaluateBool("isAlive || isDead && isDead", new Target()));
        }

        // ── Negation ──

        [Test]
        public void Evaluate_Negation_True()
        {
            Assert.IsTrue(ExpressionCompiler.EvaluateBool("!isDead", new Target()));
        }

        [Test]
        public void Evaluate_Negation_False()
        {
            Assert.IsFalse(ExpressionCompiler.EvaluateBool("!isAlive", new Target()));
        }

        // ── Ternary ──

        [Test]
        public void Evaluate_Ternary_TrueBranch()
        {
            var result = ExpressionCompiler.EvaluateString("isAlive ? name : \"Dead\"", new Target());
            Assert.AreEqual("Hero", result);
        }

        // ── String interpolation ──

        [Test]
        public void Evaluate_Interpolation()
        {
            var result = ExpressionCompiler.EvaluateString("$\"{name} has {health} HP\"", new Target());
            Assert.AreEqual("Hero has 75 HP", result);
        }

        // ── Caching ──

        [Test]
        public void Evaluate_CachedResult_SameAsFresh()
        {
            var target = new Target();
            var first = ExpressionCompiler.Evaluate("health", target);
            var second = ExpressionCompiler.Evaluate("health", target);
            Assert.AreEqual(first, second);
        }
    }
}
