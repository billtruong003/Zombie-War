using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>
    /// Bomb charge accounting. Pickup has always fired BombPickedUpEvent; nothing consumed it, so
    /// walking over a bomb drop did nothing at all.
    ///
    /// These drive <see cref="BombThrower.AddBombs"/> directly rather than firing the event: EditMode
    /// has no Bill bootstrap, so Bill.Events is null and a fired event would silently go nowhere -
    /// a test built on that would pass whether or not the handler existed. The subscribe/unsubscribe
    /// wiring is therefore listed as a manual check, and only the arithmetic is asserted here.
    /// </summary>
    public class BombThrowerPickupTests
    {
        private GameObject _go;
        private BombThrower _thrower;

        // maxBombs is a private [SerializeField]; SerializedObject is the supported way to author it
        // from a test without widening the component's API for testing's sake.
        static BombThrower Make(GameObject host, int maxBombs)
        {
            var thrower = host.AddComponent<BombThrower>();
            var so = new UnityEditor.SerializedObject(thrower);
            so.FindProperty("maxBombs").intValue = maxBombs;
            so.ApplyModifiedPropertiesWithoutUndo();
            return thrower;
        }

        // Note: Awake does not run in EditMode (no [ExecuteInEditMode]), so BombsRemaining starts at
        // 0 rather than maxBombs. Every assertion below is written against the clamp, not against the
        // starting stock, so it holds either way.
        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("BombThrowerTest");
            _thrower = Make(_go, 3);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void MaxBombsIsAuthoredValue()
        {
            Assert.AreEqual(3, _thrower.MaxBombs);
        }

        [Test]
        public void AddBombs_ClampsToMax()
        {
            _thrower.AddBombs(10);
            Assert.AreEqual(3, _thrower.BombsRemaining, "a pickup at full capacity must not overfill");
        }

        [Test]
        public void AddBombs_IgnoresZeroAndNegative()
        {
            int before = _thrower.BombsRemaining;
            _thrower.AddBombs(0);
            _thrower.AddBombs(-5);
            Assert.AreEqual(before, _thrower.BombsRemaining);
        }

        [Test]
        public void AddBombs_NeverExceedsMaxAcrossRepeatedPickups()
        {
            for (int i = 0; i < 20; i++) _thrower.AddBombs(1);
            Assert.AreEqual(3, _thrower.BombsRemaining);
        }

        [Test]
        public void ZeroMaxBombsStaysAtZero()
        {
            var host = new GameObject("ZeroMax");
            try
            {
                var zero = Make(host, 0);
                zero.AddBombs(5);
                Assert.AreEqual(0, zero.BombsRemaining, "clamp must not go negative or overshoot");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
