using System.Collections.Generic;
using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Backlog #32 (reward feel): small moments that say "that was good" without stopping play.
    /// A kill streak (15 / 30 / 60 kills inside 1.5 s) and passing your best time or best score this
    /// run each pop a gold label over the player with a pitched tick and a short buzz. Labels reuse the
    /// pickup label (DamageNumberSpawner), so no HUD layout changes. Added by PickupManager.
    /// </summary>
    public sealed class RunMoments : MonoBehaviour
    {
        public const float StreakWindow = 1.5f, StreakCooldown = 6f;
        public static readonly int[] StreakMarks = { 15, 30, 60 };
        static readonly Color Gold = new(1f, 0.82f, 0.2f);

        readonly Queue<float> _kills = new(128);
        RunState _run;
        float _streakReadyAt;
        int _lastMark;
        bool _bestTimeShown, _bestScoreShown;
        float _bestTime;
        long _bestScore;

        void OnEnable() => Bill.Events?.Subscribe<ZombieKilledEvent>(OnKilled);
        void OnDisable() => Bill.Events?.Unsubscribe<ZombieKilledEvent>(OnKilled);

        void Update()
        {
            var run = RunState.Current;
            if (run == null || run.IsOver) return;
            if (run != _run)
            {
                _run = run; _kills.Clear(); _lastMark = 0; _streakReadyAt = 0f;
                // The bests as they stood when the run began: passing them is the moment.
                _bestTime = PlayerProfile.BestSurvivalSeconds; _bestScore = PlayerProfile.BestScore;
                _bestTimeShown = _bestTime <= 30f; _bestScoreShown = _bestScore <= 0;
            }
            if (!_bestTimeShown && run.Duration > _bestTime) { _bestTimeShown = true; Pop("NEW BEST TIME!", 1.25f); }
            if (!_bestScoreShown && run.Score > _bestScore) { _bestScoreShown = true; Pop("NEW HIGH SCORE!", 1.25f); }
        }

        void OnKilled(ZombieKilledEvent e)
        {
            if (RunState.Current == null || RunState.Current.IsOver) return;
            // An elite goes down with a heavy body hit in its own material (audio coverage 06/10).
            if (e.Data != null && e.Data.isElite)
            {
                string body = SurfaceImpact.FromImpactKey(e.Data.impactSfxKey) switch
                {
                    SurfaceKind.Bone => "sfx.impact.bone.heavy",
                    SurfaceKind.Fur => "sfx.impact.fur.heavy",
                    _ => "sfx.impact.flesh.heavy",
                };
                Bill.Audio?.PlayCue(body, e.Position, SfxPriority.High, 0.9f);
            }
            float now = Time.time;
            _kills.Enqueue(now);
            while (_kills.Count > 0 && now - _kills.Peek() > StreakWindow) _kills.Dequeue();
            if (now < _streakReadyAt) return;
            int n = _kills.Count, mark = 0;
            foreach (int m in StreakMarks) if (n >= m) mark = m;
            if (mark == 0) { _lastMark = 0; return; }
            if (mark <= _lastMark) return;
            _lastMark = mark;
            _streakReadyAt = now + (mark == StreakMarks[StreakMarks.Length - 1] ? StreakCooldown : 0.5f);
            Pop($"x{mark} STREAK!", 0.9f + 0.15f * System.Array.IndexOf(StreakMarks, mark));
        }

        static void Pop(string label, float pitch)
        {
            var player = PlayerMovement.Instance;
            if (player == null) return;
            DamageNumberSpawner.SpawnLabel(label, Gold, player.transform.position + Vector3.up * 2.6f);
            Bill.Audio?.PlayPitched("sfx.ui.tap", pitch, 0.8f);
            ZombieWar.UI.UIFeedback.Haptic(ZombieWar.UI.UIFeedback.Buzz.Light);
        }
    }
}
