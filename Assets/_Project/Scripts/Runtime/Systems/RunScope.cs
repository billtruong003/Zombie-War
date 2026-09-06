using System;
using System.Collections.Generic;

namespace ZombieWar
{
    /// <summary>
    /// M7.2c — the one place run-scoped state is cleared.
    ///
    /// The bug this exists to end: statics survive scene loads inside a play session, so anything
    /// holding run state in a static silently carried into the next run. The owner hit it twice — the
    /// skill build persisted after exiting a run, and so did the pickup registry.
    ///
    /// It is called from <see cref="RunState.Begin"/> — at run START rather than at run end, because
    /// a quit, a crash or an unexpected exit path can skip an end-of-run hook, while nothing can
    /// start a run without going through Begin. Resetting on entry is the version that cannot be
    /// bypassed.
    ///
    /// Core systems are listed explicitly rather than discovered, so the list is greppable and cannot
    /// go stale silently. <see cref="Register"/> exists for systems added later that own run state the
    /// core does not know about.
    /// </summary>
    public static class RunScope
    {
        static readonly List<Action> Extra = new(8);

        /// <summary>
        /// Registers a reset for a system this class does not know about. Safe to call more than once
        /// with the same delegate — duplicates are ignored, so an OnEnable can register freely.
        /// </summary>
        public static void Register(Action reset)
        {
            if (reset == null || Extra.Contains(reset)) return;
            Extra.Add(reset);
        }

        public static void Unregister(Action reset) => Extra.Remove(reset);

        /// <summary>
        /// Clears every piece of run-scoped state. Idempotent, and safe to call when a system has not
        /// been created yet — a fresh run must be able to reset before anything exists.
        /// </summary>
        public static void ResetAll()
        {
            // ── skills ────────────────────────────────────────────────────────────────────
            // The runtime instance is deliberately KEPT and reset in place rather than nulled: the
            // driver provisions it once, and replacing the object would leave any cached reference
            // pointing at a dead build.
            Skills.SkillRuntime.Active?.Reset();
            Skills.StatusCarrier.ClearAll();
            Skills.AutonomousPower.ResetGlobalBudget();

            // ── threat ────────────────────────────────────────────────────────────────────
            // Listed explicitly rather than relying on ThreatDirector.Awake to register: objective
            // progress is a STATIC, so it accumulates whether or not a director component happens to
            // exist in the scene. Registration that depends on a component existing is not a reset.
            Threat.ThreatDirector.ResetRunState();

            // ── enemies ───────────────────────────────────────────────────────────────────
            ZombieManager.ResetRecycleCounter();
            ZombieManager.ResetAttackSlots();      // a leaked slot would silently throttle the next run
            Threat.ThreatDirector.ResetArrivals();

            // ── pickups ───────────────────────────────────────────────────────────────────
            // Coins and gems register into a static list. Without this the list accumulated entries
            // from every previous run, including destroyed ones.
            PickupManager.ClearRegistry();

            // ── anything registered later ─────────────────────────────────────────────────
            for (int i = 0; i < Extra.Count; i++)
            {
                try { Extra[i]?.Invoke(); }
                catch (Exception e)
                {
                    // One misbehaving system must not stop the rest of the run from resetting.
                    UnityEngine.Debug.LogError($"[RunScope] A registered reset threw: {e}");
                }
            }
        }
    }
}
