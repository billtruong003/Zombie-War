using System;
using System.Collections.Generic;
using UnityEngine;

namespace BillGameCore
{
    // -------------------------------------------------------
    // ITweenService interface
    // -------------------------------------------------------

    public interface ITweenService : IService
    {
        Tween Play(float from, float to, float duration, Action<float> setter);
        TweenSequence Sequence();
        void Kill(Tween tween);
        void KillTarget(object target);
        void KillAll();
        void CompleteAll();
        int ActiveCount { get; }
    }

    // -------------------------------------------------------
    // TweenService - ITickable, object pooled
    // -------------------------------------------------------

    public class TweenService : ITweenService, IInitializable, ITickable, IDisposableService
    {
        private readonly List<Tween> _active = new(64);
        private readonly Stack<Tween> _pool = new(64);
        private readonly List<TweenSequence> _sequences = new(16);
        private int _idCounter;

        private const int PoolWarmCount = 32;

        public int ActiveCount => _active.Count;

        public void Initialize()
        {
            for (int i = 0; i < PoolWarmCount; i++)
                _pool.Push(new Tween());
        }

        // -------------------------------------------------------
        // Public API
        // -------------------------------------------------------

        public Tween Play(float from, float to, float duration, Action<float> setter)
        {
            var t = Rent();
            t.From = from;
            t.To = to;
            t.Duration = Mathf.Max(duration, 0f);
            t.Setter = setter;
            t.State = TweenState.Running;
            _active.Add(t);
            return t;
        }

        public TweenSequence Sequence()
        {
            var seq = new TweenSequence();
            _sequences.Add(seq);
            return seq;
        }

        public void Kill(Tween tween)
        {
            if (tween != null) tween.Kill();
        }

        public void KillTarget(object target)
        {
            if (target == null) return;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_active[i].Owner, target))
                    _active[i].Kill();
            }
        }

        public void KillAll()
        {
            for (int i = 0; i < _active.Count; i++)
                _active[i].Kill();
            for (int i = 0; i < _sequences.Count; i++)
                _sequences[i].Kill();
        }

        public void CompleteAll()
        {
            for (int i = 0; i < _active.Count; i++)
                _active[i].Complete();
        }

        // -------------------------------------------------------
        // Tick - main update loop
        // -------------------------------------------------------

        public void Tick(float dt)
        {
            // Tick standalone tweens - reverse iterate, swap-back remove
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (!_active[i].Tick(dt))
                {
                    Return(_active[i]);
                    // Swap with last element for O(1) remove
                    int last = _active.Count - 1;
                    if (i < last) _active[i] = _active[last];
                    _active.RemoveAt(last);
                }
            }

            // Tick sequences
            for (int i = _sequences.Count - 1; i >= 0; i--)
            {
                if (!_sequences[i].Tick(dt))
                {
                    int last = _sequences.Count - 1;
                    if (i < last) _sequences[i] = _sequences[last];
                    _sequences.RemoveAt(last);
                }
            }
        }

        // -------------------------------------------------------
        // Pool management
        // -------------------------------------------------------

        internal Tween Rent()
        {
            var t = _pool.Count > 0 ? _pool.Pop() : new Tween();
            t.Reset();
            t.Id = ++_idCounter;
            return t;
        }

        internal void Return(Tween t)
        {
            t.Reset();
            _pool.Push(t);
        }

        public void Cleanup()
        {
            KillAll();
            _active.Clear();
            _sequences.Clear();
            _pool.Clear();
        }
    }

    // -------------------------------------------------------
    // BillTween - static facade (zero-alloc shortcut API)
    // -------------------------------------------------------

    public static class BillTween
    {
        private static ITweenService Svc
        {
            get
            {
                if (ServiceLocator.TryGet<ITweenService>(out var s)) return s;
                Debug.LogError("[BillTween] TweenService not registered. Is BillBootstrap running?");
                return null;
            }
        }

        // -------------------------------------------------------
        // Core float tween
        // -------------------------------------------------------

        /// <summary>Tween a float value from → to over duration, calling setter each frame.</summary>
        public static Tween Float(float from, float to, float duration, Action<float> setter)
            => Svc?.Play(from, to, duration, setter);

        /// <summary>Tween from current value (via getter) to target.</summary>
        public static Tween To(Func<float> getter, Action<float> setter, float to, float duration)
            => Svc?.Play(getter(), to, duration, setter);

        // -------------------------------------------------------
        // Transform
        // -------------------------------------------------------

        public static Tween MoveX(Transform t, float to, float dur)
            => Float(t.position.x, to, dur, v => { var p = t.position; p.x = v; t.position = p; })?.SetTarget(t);

        public static Tween MoveY(Transform t, float to, float dur)
            => Float(t.position.y, to, dur, v => { var p = t.position; p.y = v; t.position = p; })?.SetTarget(t);

        public static Tween MoveZ(Transform t, float to, float dur)
            => Float(t.position.z, to, dur, v => { var p = t.position; p.z = v; t.position = p; })?.SetTarget(t);

        public static Tween LocalMoveX(Transform t, float to, float dur)
            => Float(t.localPosition.x, to, dur, v => { var p = t.localPosition; p.x = v; t.localPosition = p; })?.SetTarget(t);

        public static Tween LocalMoveY(Transform t, float to, float dur)
            => Float(t.localPosition.y, to, dur, v => { var p = t.localPosition; p.y = v; t.localPosition = p; })?.SetTarget(t);

        public static Tween LocalMoveZ(Transform t, float to, float dur)
            => Float(t.localPosition.z, to, dur, v => { var p = t.localPosition; p.z = v; t.localPosition = p; })?.SetTarget(t);

        public static Tween ScaleX(Transform t, float to, float dur)
            => Float(t.localScale.x, to, dur, v => { var s = t.localScale; s.x = v; t.localScale = s; })?.SetTarget(t);

        public static Tween ScaleY(Transform t, float to, float dur)
            => Float(t.localScale.y, to, dur, v => { var s = t.localScale; s.y = v; t.localScale = s; })?.SetTarget(t);

        public static Tween ScaleZ(Transform t, float to, float dur)
            => Float(t.localScale.z, to, dur, v => { var s = t.localScale; s.z = v; t.localScale = s; })?.SetTarget(t);

        /// <summary>Uniform scale all axes.</summary>
        public static Tween Scale(Transform t, float to, float dur)
            => Float(t.localScale.x, to, dur, v => t.localScale = new Vector3(v, v, v))?.SetTarget(t);

        public static Tween RotateZ(Transform t, float to, float dur)
            => Float(t.eulerAngles.z, to, dur, v => { var e = t.eulerAngles; e.z = v; t.eulerAngles = e; })?.SetTarget(t);

        // -------------------------------------------------------
        // Move/Scale Vector3 (multi-axis)
        // -------------------------------------------------------

        /// <summary>Move transform to target position. Returns a sequence of 3 joined tweens.</summary>
        public static TweenSequence Move(Transform t, Vector3 to, float dur)
        {
            var seq = Sequence();
            seq.Append(MoveX(t, to.x, dur));
            seq.Join(MoveY(t, to.y, dur));
            seq.Join(MoveZ(t, to.z, dur));
            return seq;
        }

        public static TweenSequence LocalMove(Transform t, Vector3 to, float dur)
        {
            var seq = Sequence();
            seq.Append(LocalMoveX(t, to.x, dur));
            seq.Join(LocalMoveY(t, to.y, dur));
            seq.Join(LocalMoveZ(t, to.z, dur));
            return seq;
        }

        public static TweenSequence ScaleTo(Transform t, Vector3 to, float dur)
        {
            var seq = Sequence();
            seq.Append(ScaleX(t, to.x, dur));
            seq.Join(ScaleY(t, to.y, dur));
            seq.Join(ScaleZ(t, to.z, dur));
            return seq;
        }

        // -------------------------------------------------------
        // UI - CanvasGroup, Image, SpriteRenderer
        // -------------------------------------------------------

        public static Tween Fade(CanvasGroup cg, float to, float dur)
            => Float(cg.alpha, to, dur, v => cg.alpha = v)?.SetTarget(cg);

        public static Tween Fade(SpriteRenderer sr, float to, float dur)
            => Float(sr.color.a, to, dur, v => { var c = sr.color; c.a = v; sr.color = c; })?.SetTarget(sr);

        public static Tween Fade(UnityEngine.UI.Image img, float to, float dur)
            => Float(img.color.a, to, dur, v => { var c = img.color; c.a = v; img.color = c; })?.SetTarget(img);

        public static Tween Fade(UnityEngine.UI.Text txt, float to, float dur)
            => Float(txt.color.a, to, dur, v => { var c = txt.color; c.a = v; txt.color = c; })?.SetTarget(txt);

        public static Tween FillAmount(UnityEngine.UI.Image img, float to, float dur)
            => Float(img.fillAmount, to, dur, v => img.fillAmount = v)?.SetTarget(img);

        // -------------------------------------------------------
        // Color (single channel tweens for zero-alloc)
        // -------------------------------------------------------

        public static Tween ColorR(SpriteRenderer sr, float to, float dur)
            => Float(sr.color.r, to, dur, v => { var c = sr.color; c.r = v; sr.color = c; })?.SetTarget(sr);

        public static Tween ColorG(SpriteRenderer sr, float to, float dur)
            => Float(sr.color.g, to, dur, v => { var c = sr.color; c.g = v; sr.color = c; })?.SetTarget(sr);

        public static Tween ColorB(SpriteRenderer sr, float to, float dur)
            => Float(sr.color.b, to, dur, v => { var c = sr.color; c.b = v; sr.color = c; })?.SetTarget(sr);

        // -------------------------------------------------------
        // Sequence shortcut
        // -------------------------------------------------------

        public static TweenSequence Sequence() => Svc?.Sequence();

        // -------------------------------------------------------
        // Kill API
        // -------------------------------------------------------

        public static void Kill(Tween tween) => Svc?.Kill(tween);
        public static void KillTarget(object target) => Svc?.KillTarget(target);
        public static void KillAll() => Svc?.KillAll();
        public static void CompleteAll() => Svc?.CompleteAll();

        // -------------------------------------------------------
        // Utility
        // -------------------------------------------------------

        /// <summary>Simple delay callback (no value interpolation).</summary>
        public static Tween DelayedCall(float delay, Action callback)
            => Float(0f, 0f, 0f, null)?.SetDelay(delay).OnComplete(callback);

        public static int ActiveCount => ServiceLocator.TryGet<ITweenService>(out var s) ? s.ActiveCount : 0;
    }
}
