using System;
using System.Collections.Generic;
using UnityEngine;

namespace BillGameCore
{
    public class TimerHandle
    {
        internal float Interval, Elapsed;
        internal Action Callback;
        internal int Remaining; // -1 = infinite
        internal bool Unscaled;
        public bool IsCancelled { get; internal set; }
        public bool IsActive => !IsCancelled;
        public void Cancel() => IsCancelled = true;
    }

    public class TimerService : ITimerService, IInitializable, ITickable, IDisposableService
    {
        private readonly List<TimerHandle> _active = new(32);
        private readonly List<TimerHandle> _pending = new(8);
        private readonly List<TimerHandle> _remove = new(8);

        public void Initialize() { }
        public int ActiveCount => _active.Count;

        public TimerHandle Delay(float seconds, Action cb) => Add(seconds, cb, 1, false);
        public TimerHandle Delay(float seconds, Action cb, bool unscaled) => Add(seconds, cb, 1, unscaled);
        public TimerHandle Repeat(float interval, Action cb) => Add(interval, cb, -1, false);
        public TimerHandle Repeat(float interval, Action cb, int count) => Add(interval, cb, count, false);
        public void Cancel(TimerHandle h) { if (h != null) h.IsCancelled = true; }
        public void CancelAll() { foreach (var t in _active) t.IsCancelled = true; }

        private TimerHandle Add(float interval, Action cb, int repeat, bool unscaled)
        {
            var h = new TimerHandle { Interval = interval, Callback = cb, Remaining = repeat, Unscaled = unscaled };
            _pending.Add(h);
            return h;
        }

        public void Tick(float dt)
        {
            if (_pending.Count > 0) { _active.AddRange(_pending); _pending.Clear(); }

            for (int i = 0; i < _active.Count; i++)
            {
                var t = _active[i];
                if (t.IsCancelled) { _remove.Add(t); continue; }

                t.Elapsed += t.Unscaled ? Time.unscaledDeltaTime : dt;
                if (t.Elapsed < t.Interval) continue;

                t.Elapsed -= t.Interval;
                try { t.Callback?.Invoke(); } catch (Exception e) { Debug.LogException(e); }

                if (t.Remaining > 0) { t.Remaining--; if (t.Remaining <= 0) { t.IsCancelled = true; _remove.Add(t); } }
            }

            if (_remove.Count > 0) { foreach (var r in _remove) _active.Remove(r); _remove.Clear(); }
        }

        public void Cleanup() { _active.Clear(); _pending.Clear(); }
    }
}
