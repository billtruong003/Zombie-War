using System.Collections.Generic;

namespace ZombieWar.Stations
{
    /// <summary>
    /// M7.3 — the run-scoped ledger of what has happened to each anchor.
    ///
    /// This is the piece that makes chunk recycling safe. The visual station object is disposable and
    /// re-derived from <see cref="StationAnchors"/>; the FACTS about it — completed, on cooldown,
    /// destroyed, boss alive — live here, keyed by the anchor's stable id. Unloading a chunk drops the
    /// object; it cannot drop the fact. Re-entering the area rebuilds the object in its recorded
    /// state rather than as a fresh, lootable station.
    ///
    /// <b>Registered with <see cref="RunScope"/> the day it was written.</b> The skill build and the
    /// pickup registry both leaked across runs because they predated that path; nothing written in
    /// M7.3 has that excuse.
    /// </summary>
    public static class StationRegistry
    {
        public enum Status { Untouched = 0, Active, Completed, Cooldown, Destroyed }

        struct Record
        {
            public Status status;
            public float readyAt;      // for Cooldown
            public bool bossAlive;
        }

        static readonly Dictionary<long, Record> Records = new(64);

        /// <summary>Only one major encounter may own the player's attention at a time.</summary>
        public static long ActiveEncounterAnchor { get; private set; }

        static bool _registered;

        /// <summary>
        /// Hooks this registry into the single run-reset path. Idempotent, and called from the
        /// director's Awake so it cannot be forgotten.
        /// </summary>
        public static void EnsureRegisteredWithRunScope()
        {
            if (_registered) return;
            _registered = true;
            RunScope.Register(ResetAll);
        }

        public static void ResetAll()
        {
            Records.Clear();
            ActiveEncounterAnchor = 0;
        }

        public static int TrackedCount => Records.Count;

        public static Status StatusOf(long anchorId, float now)
        {
            if (!Records.TryGetValue(anchorId, out var r)) return Status.Untouched;
            if (r.status == Status.Cooldown && now >= r.readyAt) return Status.Untouched;
            return r.status;
        }

        public static void SetStatus(long anchorId, Status status, float now, float cooldown = 0f)
        {
            Records.TryGetValue(anchorId, out var r);
            r.status = status;
            if (status == Status.Cooldown) r.readyAt = now + cooldown;
            Records[anchorId] = r;
        }

        /// <summary>
        /// Claims the single "major encounter" slot. Returns false when another encounter already
        /// owns the player's attention — this is what stops two bosses running at once.
        /// </summary>
        public static bool TryClaimEncounter(long anchorId)
        {
            if (ActiveEncounterAnchor != 0 && ActiveEncounterAnchor != anchorId) return false;
            ActiveEncounterAnchor = anchorId;
            return true;
        }

        public static void ReleaseEncounter(long anchorId)
        {
            if (ActiveEncounterAnchor == anchorId) ActiveEncounterAnchor = 0;
        }

        /// <summary>
        /// A boss spawned by this anchor is alive. Tracked so that unloading the chunk cannot orphan
        /// it: the director despawns a boss whose anchor leaves the active ring, and the flag is what
        /// tells it there is one to despawn.
        /// </summary>
        public static void SetBossAlive(long anchorId, bool alive)
        {
            Records.TryGetValue(anchorId, out var r);
            r.bossAlive = alive;
            Records[anchorId] = r;
        }

        public static bool IsBossAlive(long anchorId) =>
            Records.TryGetValue(anchorId, out var r) && r.bossAlive;
    }
}
