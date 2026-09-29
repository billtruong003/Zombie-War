using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills
{
    /// <summary>
    /// Damage dealt, grouped by where it came from ("gun" or a card id). Off by default and a single
    /// bool test when off; the Skill Sandbox turns it on for its DPS meter and the balance bot reads it
    /// to compare every card at every rank.
    /// </summary>
    public static class DamageLedger
    {
        public const string Gun = "gun";

        public struct Row
        {
            public string source;
            public float total;
            public int hits;
            public float windowDps;   // over the last WindowSeconds
            public float averageDps;  // since the last Reset
        }

        public static bool Enabled;
        public static float WindowSeconds = 5f;

        struct Event { public float time; public float amount; }

        sealed class Source
        {
            public float total;
            public int hits;
            public readonly Queue<Event> recent = new(64);
        }

        static readonly Dictionary<string, Source> Sources = new(32);
        static float _since = -1f;

        public static void Record(string source, float amount)
        {
            if (!Enabled || amount <= 0f) return;
            if (string.IsNullOrEmpty(source)) source = "unknown";
            if (_since < 0f) _since = Time.time;
            if (!Sources.TryGetValue(source, out var s)) Sources[source] = s = new Source();
            s.total += amount;
            s.hits++;
            s.recent.Enqueue(new Event { time = Time.time, amount = amount });
        }

        public static void Reset()
        {
            Sources.Clear();
            _since = -1f;
        }

        public static float Elapsed => _since < 0f ? 0f : Time.time - _since;

        /// <summary>Fills <paramref name="into"/> with one row per source, biggest windowed DPS first.</summary>
        public static void Snapshot(List<Row> into)
        {
            into.Clear();
            float now = Time.time, cutoff = now - WindowSeconds, elapsed = Mathf.Max(0.001f, Elapsed);
            float window = Mathf.Max(0.001f, Mathf.Min(WindowSeconds, elapsed));
            foreach (var kv in Sources)
            {
                var q = kv.Value.recent;
                while (q.Count > 0 && q.Peek().time < cutoff) q.Dequeue();
                float sum = 0f;
                foreach (var e in q) sum += e.amount;
                into.Add(new Row { source = kv.Key, total = kv.Value.total, hits = kv.Value.hits,
                                   windowDps = sum / window, averageDps = kv.Value.total / elapsed });
            }
            into.Sort((a, b) => b.windowDps.CompareTo(a.windowDps));
        }

        public static float TotalOf(string source) => Sources.TryGetValue(source, out var s) ? s.total : 0f;
    }
}
