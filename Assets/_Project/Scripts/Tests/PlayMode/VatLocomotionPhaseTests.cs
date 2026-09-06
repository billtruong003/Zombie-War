using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M5.1.3 CP6 - looping locomotion must be desynchronized; gameplay-timed clips must not be.
    ///
    /// A batch-spawned wave entered idle/move at phase zero together, so the crowd bobbed in
    /// lockstep and amplified the "hopping" the playtest reported. Locomotion now starts at a
    /// stable per-instance phase. Attack, hit and death still start at frame zero, because their
    /// damage and telegraph windows are measured from there.
    /// </summary>
    public class VatLocomotionPhaseTests
    {
        private readonly List<GameObject> _spawned = new();
        private readonly List<Object> _createdAssets = new();

        private VAT_Animator MakeAnimator(out VAT_AnimationData data)
        {
            var go = new GameObject("VatHost", typeof(MeshFilter), typeof(MeshRenderer));
            _spawned.Add(go);

            data = ScriptableObject.CreateInstance<VAT_AnimationData>();
            // A complete fixture: the animator only initializes (and only ticks its shader
            // properties safely) when the data has real baked mesh + position texture.
            data.bakedMesh = new Mesh { name = "TestBaked" };
            data.positionTexture = new Texture2D(4, 4);
            _createdAssets.Add(data.bakedMesh);
            _createdAssets.Add(data.positionTexture);
            data.animationClips = new List<VAT_AnimationData.ClipInfo>
            {
                new VAT_AnimationData.ClipInfo { name = "Move", duration = 1f, wrapMode = WrapMode.Loop },
                new VAT_AnimationData.ClipInfo { name = "Idle", duration = 1.2f, wrapMode = WrapMode.Loop },
                new VAT_AnimationData.ClipInfo { name = "Attack", duration = 0.8f, wrapMode = WrapMode.Once },
                new VAT_AnimationData.ClipInfo { name = "Death", duration = 1.4f, wrapMode = WrapMode.Once },
            };

            var animator = go.AddComponent<VAT_Animator>();
            animator.animationData = data;
            return animator;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var asset in _createdAssets) if (asset != null) Object.DestroyImmediate(asset);
            _createdAssets.Clear();
        }

        [Test]
        public void LoopingClip_StartsAtTheRequestedPhase()
        {
            var animator = MakeAnimator(out _);
            animator.Play("Move", 0.37f);
            Assert.AreEqual(0.37f, animator.CurrentNormalizedPhase, 0.01f);
        }

        [Test]
        public void OneShotClip_AlwaysStartsAtZero_EvenIfAPhaseIsRequested()
        {
            var animator = MakeAnimator(out _);
            foreach (var clip in new[] { "Attack", "Death" })
            {
                animator.Play(clip, 0.8f);
                Assert.AreEqual(0f, animator.CurrentNormalizedPhase, 0.001f,
                    $"{clip} must begin at frame zero - its gameplay timing is measured from there");
            }
        }

        [Test]
        public void CrossFadeIntoLocomotion_KeepsTheRequestedPhase()
        {
            var animator = MakeAnimator(out _);
            animator.Play("Idle", 0.1f);
            animator.CrossFade("Move", 0.2f, 0.62f);
            Assert.AreEqual("Move", animator.CurrentClipName);
            Assert.AreEqual(0.62f, animator.CurrentNormalizedPhase, 0.01f);
        }

        [Test]
        public void CrossFadeIntoAttack_StartsAtZero()
        {
            var animator = MakeAnimator(out _);
            animator.Play("Move", 0.5f);
            animator.CrossFade("Attack", 0.2f, 0.5f);
            Assert.AreEqual(0f, animator.CurrentNormalizedPhase, 0.001f);
        }

        /// <summary>
        /// Kiểm ĐÚNG nguồn pha mà production dùng, không dựng lại công thức.
        ///
        /// Bản cũ tự dựng 24 GameObject rồi tự nhân GetInstanceID với tỉ lệ vàng — tức là nó chép lại
        /// phần cài đặt thay vì kiểm hợp đồng. Điều đó khiến nó vừa bỏ sót lỗi thật (pha phụ thuộc thứ
        /// tự tạo object) vừa đỏ vì một lý do không liên quan tới quái: chỉ cần một hệ thống khác tạo
        /// thêm một GameObject giữa lúc spawn là bước nhảy id đổi và dãy pha bị alias.
        /// </summary>
        [Test]
        public void ConsecutiveOrdinals_SpreadAcrossDistinctPhases()
        {
            var distinct = new HashSet<int>();
            float min = 1f, max = 0f;

            for (uint i = 0; i < 24; i++)
            {
                float p = LocomotionPhase.ForOrdinal(i);
                Assert.GreaterOrEqual(p, 0f, "phase left [0,1)");
                Assert.Less(p, 1f, "phase left [0,1)");
                distinct.Add(Mathf.RoundToInt(p * 100f));
                min = Mathf.Min(min, p);
                max = Mathf.Max(max, p);
            }

            Assert.GreaterOrEqual(distinct.Count, 20, "the crowd shares too few locomotion phases");
            Assert.GreaterOrEqual(max - min, 0.75f, "phase spread is too narrow to break up the bob");
        }

        [Test]
        public void TheSameOrdinalAlwaysGivesTheSamePhase()
        {
            for (uint i = 0; i < 8; i++)
                Assert.AreEqual(LocomotionPhase.ForOrdinal(i), LocomotionPhase.ForOrdinal(i),
                    "phase source is not deterministic");
        }

        /// <summary>
        /// Đây là hồi quy thật đã xảy ra: một hệ thống KHÁC tạo GameObject giữa lúc spawn quái làm
        /// hỏng dãy pha. Nguồn pha mới phải hoàn toàn miễn nhiễm với điều đó.
        /// </summary>
        [Test]
        public void UnrelatedGameObjectCreation_DoesNotDisturbThePhaseSequence()
        {
            LocomotionPhase.ResetForNewSession();
            var before = new List<float>();
            for (int i = 0; i < 12; i++) before.Add(LocomotionPhase.Next());

            LocomotionPhase.ResetForNewSession();
            var after = new List<float>();
            for (int i = 0; i < 12; i++)
            {
                // chen đúng thứ đã gây ra hồi quy: một object không liên quan ra đời giữa chừng
                var noise = new GameObject($"UnrelatedObject{i}");
                _spawned.Add(noise);
                after.Add(LocomotionPhase.Next());
            }

            CollectionAssert.AreEqual(before, after,
                "dãy pha đổi khi có object không liên quan được tạo — nguồn pha lại phụ thuộc thứ tự tạo object.");
        }

        [Test]
        public void ResetForNewSession_PreventsStaticLeakageBetweenSessions()
        {
            LocomotionPhase.ResetForNewSession();
            float firstOfSessionA = LocomotionPhase.Next();
            LocomotionPhase.Next();
            LocomotionPhase.Next();

            LocomotionPhase.ResetForNewSession();
            float firstOfSessionB = LocomotionPhase.Next();

            Assert.AreEqual(firstOfSessionA, firstOfSessionB,
                "bộ đếm mang trạng thái sang phiên sau — Disable Domain Reload sẽ làm hai lần chạy khác nhau.");
        }

        [UnityTest]
        public IEnumerator PhaseAdvancesAndWrapsWithoutJumpingBackToZero()
        {
            var animator = MakeAnimator(out _);
            animator.Play("Move", 0.9f);
            float first = animator.CurrentNormalizedPhase;

            yield return null;
            yield return null;

            Assert.AreNotEqual(first, animator.CurrentNormalizedPhase,
                "the clip is not advancing from its start phase");
        }
    }
}
