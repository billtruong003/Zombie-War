using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ZombieWar.UI
{
    /// <summary>
    /// Preview "sống": chạy base idle liên tục, thỉnh thoảng crossfade sang 1 showcase idle rồi về.
    /// Dùng PlayableGraph (AnimationMixerPlayable) trên Animator của preview character — KHÔNG đụng
    /// PlayerAnimator gameplay, không thêm state combat. Root motion tắt (preview không di chuyển).
    /// Lịch bằng coroutine, không poll/allocate mỗi frame.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class CostumePreviewIdleDirector : MonoBehaviour
    {
        [SerializeField] private AnimationClip baseIdle;
        [SerializeField] private AnimationClip[] variations;
        [SerializeField] private float minInterval = 7f;
        [SerializeField] private float maxInterval = 12f;
        [SerializeField] private float crossfade = 0.35f;
        [SerializeField] private bool enabledDirector = true;

        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable _basePlayable;
        private AnimationClipPlayable _varPlayable;
        private int _lastVar = -1;
        private Coroutine _loop;

        private void OnEnable()
        {
            _animator = GetComponent<Animator>();
            if (baseIdle == null || !enabledDirector) { LogNoClips(); return; }

            _animator.applyRootMotion = false; // preview không drift
            BuildGraph();
            if (variations != null && ValidVariationCount() > 0)
                _loop = StartCoroutine(VariationLoop());
        }

        private void OnDisable()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            if (_graph.IsValid()) _graph.Destroy();
        }

        private void BuildGraph()
        {
            _graph = PlayableGraph.Create($"PreviewIdle_{name}");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime); // sống cả khi timeScale=0
            var output = AnimationPlayableOutput.Create(_graph, "out", _animator);
            _mixer = AnimationMixerPlayable.Create(_graph, 2);
            output.SetSourcePlayable(_mixer);

            _basePlayable = AnimationClipPlayable.Create(_graph, baseIdle);
            _graph.Connect(_basePlayable, 0, _mixer, 0);
            _mixer.SetInputWeight(0, 1f);
            _graph.Play();
        }

        private int ValidVariationCount()
        {
            int n = 0;
            if (variations != null) foreach (var v in variations) if (v != null) n++;
            return n;
        }

        private void LogNoClips()
        {
            if (baseIdle == null)
                Debug.LogWarning("[PreviewIdle] Thiếu baseIdle clip — preview đứng yên. Gán clip trong Inspector.", this);
        }

        private System.Collections.IEnumerator VariationLoop()
        {
            var valid = new List<AnimationClip>();
            foreach (var v in variations) if (v != null) valid.Add(v);

            while (true)
            {
                yield return new WaitForSecondsRealtime(Random.Range(minInterval, maxInterval));

                int pick = Random.Range(0, valid.Count);
                if (valid.Count > 1 && pick == _lastVar) pick = (pick + 1) % valid.Count; // không lặp liền
                _lastVar = pick;

                yield return PlayVariation(valid[pick]);
            }
        }

        private System.Collections.IEnumerator PlayVariation(AnimationClip clip)
        {
            if (_varPlayable.IsValid()) { _graph.Disconnect(_mixer, 1); _varPlayable.Destroy(); }
            _varPlayable = AnimationClipPlayable.Create(_graph, clip);
            _graph.Connect(_varPlayable, 0, _mixer, 1);
            _varPlayable.SetTime(0);

            // crossfade in
            yield return Blend(0f, 1f, crossfade);
            // giữ tới gần hết clip
            float hold = Mathf.Max(0.1f, clip.length - crossfade * 2f);
            yield return new WaitForSecondsRealtime(hold);
            // crossfade out
            yield return Blend(1f, 0f, crossfade);

            if (_varPlayable.IsValid()) { _graph.Disconnect(_mixer, 1); _varPlayable.Destroy(); }
        }

        private System.Collections.IEnumerator Blend(float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float w = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                _mixer.SetInputWeight(1, w);
                _mixer.SetInputWeight(0, 1f - w);
                yield return null;
            }
            _mixer.SetInputWeight(1, to);
            _mixer.SetInputWeight(0, 1f - to);
        }
    }
}
