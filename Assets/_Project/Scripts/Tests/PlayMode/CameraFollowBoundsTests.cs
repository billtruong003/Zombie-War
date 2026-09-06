using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZombieWar.Tests
{
    /// PlayMode M5.1 CP5: the camera's displacement contract.
    ///
    /// The defect these guard against was measured live before the fix: sustained per-shot shake
    /// walked the camera 76 m off the player in 15 s, and a shake frozen by a pause (bomb landing
    /// on the same frame as a level-up) re-added one constant offset every frame - 150 m became
    /// 654 m while the game was PAUSED. The contract is: the camera never sits further from the
    /// follow pose than the authored shake amplitude, and it returns to the follow pose when the
    /// shake ends, under any timeScale.
    public class CameraFollowBoundsTests
    {
        private GameObject _camGo;
        private Transform _target;
        private CameraFollow _follow;
        private Vector3 _offset;
        private float _maxShakeOffset;

        [SetUp]
        public void SetUp()
        {
            _camGo = new GameObject("TestCameraFollow");
            _follow = _camGo.AddComponent<CameraFollow>();
            _target = new GameObject("TestCameraTarget").transform;
            _target.position = new Vector3(3f, 0f, 5f);
            _follow.Target = _target;

            var f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            _offset = (Vector3)typeof(CameraFollow).GetField("offset", f).GetValue(_follow);
            _maxShakeOffset = (float)typeof(CameraFollow).GetField("maxShakeOffset", f).GetValue(_follow);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            if (_target != null) Object.DestroyImmediate(_target.gameObject);
        }

        private float Deviation() =>
            Vector3.Distance(_camGo.transform.position, _target.position + _offset);

        /// <summary>The shake offset is per-axis noise on X and Y, so the offset VECTOR can reach
        /// maxShakeOffset×√2 when both axes peak together. The contract bound is that magnitude.</summary>
        private float MaxShakeMagnitude => _maxShakeOffset * 1.41422f;

        /// Settles the SmoothDamp follow onto a stationary target so deviation measures shake only.
        private IEnumerator SettleOntoTarget()
        {
            for (int i = 0; i < 60; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator SustainedShake_NeverExceedsAuthoredAmplitude()
        {
            yield return SettleOntoTarget();

            for (int frame = 0; frame < 120; frame++)
            {
                _follow.Shake(0.4f);   // re-traumatize every frame, like automatic fire
                yield return null;
                Assert.LessOrEqual(Deviation(), MaxShakeMagnitude + 0.05f,
                    $"frame {frame}: camera drifted beyond the authored shake amplitude");
            }
        }

        [UnityTest]
        public IEnumerator ShakeWhilePaused_DoesNotAccumulate()
        {
            yield return SettleOntoTarget();

            Time.timeScale = 0f;
            _follow.Shake(1f);
            for (int frame = 0; frame < 90; frame++)
            {
                yield return null;
                Assert.LessOrEqual(Deviation(), MaxShakeMagnitude + 0.05f,
                    $"paused frame {frame}: shake accumulated into permanent displacement");
            }
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator AfterShakeEnds_CameraReturnsToFollowPose()
        {
            yield return SettleOntoTarget();

            _follow.Shake(1f);
            // Trauma decays at traumaDecayPerSecond (default 1.5/s) in unscaled time; 1.5 s clears it.
            yield return new WaitForSecondsRealtime(1.5f);
            for (int i = 0; i < 30; i++) yield return null;   // let SmoothDamp finish settling

            Assert.LessOrEqual(Deviation(), 0.05f,
                "camera did not recover the follow pose after the shake ended");
        }
    }
}
