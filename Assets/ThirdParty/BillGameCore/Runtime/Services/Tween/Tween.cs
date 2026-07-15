using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace BillGameCore
{
    public enum LoopType : byte { Restart, Yoyo, Incremental }
    public enum TweenState : byte { Idle, Delayed, Running, Complete }

    public sealed class Tween
    {
        // Core
        internal float From, To, Duration;
        internal float Elapsed, DelayTime;
        internal Action<float> Setter;
        internal EaseType EaseKind;
        internal TweenState State;

        // Loop
        internal int LoopTotal;   // 0 = play once, -1 = infinite, N = repeat N more times
        internal LoopType Loop;
        internal float IncrementalOffset;
        private bool _yoyoForward;

        // Callbacks
        internal Action CbStart, CbComplete;
        internal Action<float> CbUpdate; // receives normalized 0..1
        private bool _startFired;

        // Ownership
        internal object Owner; // for KillTarget
        internal bool UseUnscaled;
        internal int Id;

        // Alive means the tween is in the active list and should be ticked
        public bool IsAlive => State == TweenState.Delayed || State == TweenState.Running;
        public bool IsComplete => State == TweenState.Complete || State == TweenState.Idle;

        // -------------------------------------------------------
        // Fluent API
        // -------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Tween SetEase(EaseType ease) { EaseKind = ease; return this; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Tween SetDelay(float delay) { DelayTime = delay; if (delay > 0f && State == TweenState.Running) State = TweenState.Delayed; return this; }

        public Tween SetLoops(int count, LoopType type = LoopType.Restart)
        {
            LoopTotal = count;
            Loop = type;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Tween SetUnscaled() { UseUnscaled = true; return this; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Tween SetTarget(object target) { Owner = target; return this; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Tween OnStart(Action cb) { CbStart = cb; return this; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Tween OnUpdate(Action<float> cb) { CbUpdate = cb; return this; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Tween OnComplete(Action cb) { CbComplete = cb; return this; }

        public void Kill()
        {
            State = TweenState.Complete;
        }

        public void Complete()
        {
            if (!IsAlive) return;
            ApplyValue(1f);
            FireComplete();
        }

        // -------------------------------------------------------
        // Internal evaluation - called by TweenService each frame
        // -------------------------------------------------------

        internal bool Tick(float dt)
        {
            if (State == TweenState.Complete || State == TweenState.Idle)
                return false;

            float delta = UseUnscaled ? Time.unscaledDeltaTime : dt;

            // Delay phase
            if (State == TweenState.Delayed)
            {
                DelayTime -= delta;
                if (DelayTime > 0f) return true;
                delta = -DelayTime; // leftover time after delay
                State = TweenState.Running;
            }

            // Fire start callback once
            if (!_startFired)
            {
                _startFired = true;
                try { CbStart?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            }

            Elapsed += delta;
            float raw = Duration > 0f ? Mathf.Clamp01(Elapsed / Duration) : 1f;
            float t = raw;

            // Yoyo: ping-pong direction
            if (Loop == LoopType.Yoyo && !_yoyoForward)
                t = 1f - t;

            ApplyValue(t);

            // Completed one cycle?
            if (raw >= 1f)
            {
                if (LoopTotal == 0)
                {
                    FireComplete();
                    return false;
                }

                // Loop handling
                Elapsed = 0f;
                if (LoopTotal > 0) LoopTotal--;

                switch (Loop)
                {
                    case LoopType.Restart:
                        break;
                    case LoopType.Yoyo:
                        _yoyoForward = !_yoyoForward;
                        break;
                    case LoopType.Incremental:
                        float range = To - From;
                        IncrementalOffset += range;
                        break;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyValue(float rawT)
        {
            float eased = Ease.Evaluate(EaseKind, rawT);
            float value = From + (To - From) * eased + IncrementalOffset;

            try { Setter?.Invoke(value); } catch (Exception e) { Debug.LogException(e); }
            try { CbUpdate?.Invoke(rawT); } catch (Exception e) { Debug.LogException(e); }
        }

        private void FireComplete()
        {
            State = TweenState.Complete;
            try { CbComplete?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }

        // -------------------------------------------------------
        // Pool reset
        // -------------------------------------------------------

        internal void Reset()
        {
            From = To = Duration = Elapsed = DelayTime = IncrementalOffset = 0f;
            Setter = null;
            EaseKind = EaseType.Linear;
            State = TweenState.Idle;
            LoopTotal = 0;
            Loop = LoopType.Restart;
            _yoyoForward = true;
            CbStart = CbComplete = null;
            CbUpdate = null;
            _startFired = false;
            Owner = null;
            UseUnscaled = false;
            Id = 0;
        }
    }
}
