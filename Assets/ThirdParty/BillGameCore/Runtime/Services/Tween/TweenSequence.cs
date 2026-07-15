using System;
using System.Collections.Generic;
using UnityEngine;

namespace BillGameCore
{
    public sealed class TweenSequence
    {
        private enum StepType : byte { Tween, Interval, Callback }

        private struct Step
        {
            public StepType Type;
            public Tween Tween;          // for Tween steps
            public float Interval;       // for Interval steps
            public Action Callback;      // for Callback steps
            public bool IsJoined;        // runs parallel with previous
        }

        private readonly List<Step> _steps = new(8);
        private int _current;
        private float _intervalElapsed;
        private bool _alive;
        private bool _useUnscaled;
        private int _loopTotal;          // 0 = play once, -1 = infinite
        private Action _onComplete;
        private Action<int> _onStepComplete;

        // Active joined tweens running in parallel
        private readonly List<Tween> _activeJoined = new(4);

        internal bool IsAlive => _alive;

        internal TweenSequence()
        {
            _alive = true;
            _current = 0;
        }

        // -------------------------------------------------------
        // Builder API
        // -------------------------------------------------------

        /// <summary>Add a tween that plays after the previous step finishes.</summary>
        public TweenSequence Append(Tween tween)
        {
            if (tween == null) return this;
            // Pause tween - sequence drives it manually
            tween.State = TweenState.Running;
            _steps.Add(new Step { Type = StepType.Tween, Tween = tween, IsJoined = false });
            return this;
        }

        /// <summary>Add a tween that plays in parallel with the previous Append.</summary>
        public TweenSequence Join(Tween tween)
        {
            if (tween == null) return this;
            tween.State = TweenState.Running;
            _steps.Add(new Step { Type = StepType.Tween, Tween = tween, IsJoined = true });
            return this;
        }

        /// <summary>Wait for a duration before the next step.</summary>
        public TweenSequence AppendInterval(float seconds)
        {
            _steps.Add(new Step { Type = StepType.Interval, Interval = seconds, IsJoined = false });
            return this;
        }

        /// <summary>Fire a callback between steps.</summary>
        public TweenSequence AppendCallback(Action callback)
        {
            _steps.Add(new Step { Type = StepType.Callback, Callback = callback, IsJoined = false });
            return this;
        }

        /// <summary>Insert a tween at a specific time position (sugar for delay).</summary>
        public TweenSequence Insert(float atTime, Tween tween)
        {
            if (tween == null) return this;
            tween.SetDelay(atTime);
            tween.State = TweenState.Delayed;
            _steps.Add(new Step { Type = StepType.Tween, Tween = tween, IsJoined = true });
            return this;
        }

        public TweenSequence SetLoops(int count)
        {
            _loopTotal = count;
            return this;
        }

        public TweenSequence SetUnscaled()
        {
            _useUnscaled = true;
            return this;
        }

        public TweenSequence OnComplete(Action cb)
        {
            _onComplete = cb;
            return this;
        }

        public TweenSequence OnStepComplete(Action<int> cb)
        {
            _onStepComplete = cb;
            return this;
        }

        public void Kill()
        {
            _alive = false;
            // Kill all tweens still referenced
            for (int i = 0; i < _steps.Count; i++)
            {
                var s = _steps[i];
                if (s.Type == StepType.Tween && s.Tween != null)
                    s.Tween.Kill();
            }
            _activeJoined.Clear();
        }

        // -------------------------------------------------------
        // Tick - called by TweenService
        // -------------------------------------------------------

        internal bool Tick(float dt)
        {
            if (!_alive) return false;
            float delta = _useUnscaled ? Time.unscaledDeltaTime : dt;

            // Tick any active joined tweens
            for (int i = _activeJoined.Count - 1; i >= 0; i--)
            {
                if (!_activeJoined[i].Tick(delta))
                    _activeJoined.RemoveAt(i);
            }

            // If we have active joined tweens and current step is waiting, keep waiting
            if (_current >= _steps.Count)
            {
                if (_activeJoined.Count > 0) return true;
                return HandleSequenceEnd();
            }

            // Process current step
            bool advance = ProcessStep(_current, delta);

            if (advance)
            {
                try { _onStepComplete?.Invoke(_current); } catch (Exception e) { Debug.LogException(e); }
                _current++;
                _intervalElapsed = 0f;

                // Immediately process callbacks and collect joined steps
                while (_current < _steps.Count)
                {
                    var next = _steps[_current];

                    if (next.Type == StepType.Callback && !next.IsJoined)
                    {
                        try { next.Callback?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                        try { _onStepComplete?.Invoke(_current); } catch (Exception e) { Debug.LogException(e); }
                        _current++;
                        continue;
                    }

                    // Collect joined tweens that follow
                    if (next.IsJoined && next.Type == StepType.Tween)
                    {
                        _activeJoined.Add(next.Tween);
                        _current++;
                        continue;
                    }

                    break;
                }
            }

            return true;
        }

        private bool ProcessStep(int index, float delta)
        {
            var step = _steps[index];

            switch (step.Type)
            {
                case StepType.Tween:
                    return !step.Tween.Tick(delta);

                case StepType.Interval:
                    _intervalElapsed += delta;
                    return _intervalElapsed >= step.Interval;

                case StepType.Callback:
                    try { step.Callback?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                    return true;

                default:
                    return true;
            }
        }

        private bool HandleSequenceEnd()
        {
            if (_loopTotal == 0)
            {
                _alive = false;
                try { _onComplete?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                return false;
            }

            // Loop: restart
            if (_loopTotal > 0) _loopTotal--;
            _current = 0;
            _intervalElapsed = 0f;

            // Reset all tweens
            for (int i = 0; i < _steps.Count; i++)
            {
                var s = _steps[i];
                if (s.Type == StepType.Tween && s.Tween != null)
                {
                    s.Tween.Elapsed = 0f;
                    s.Tween.State = TweenState.Running;
                }
            }

            return true;
        }
    }
}
