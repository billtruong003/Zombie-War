#if PHOTON_FUSION
using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace BillGameCore
{
    /// <summary>
    /// Reusable contract for "a player" in a Fusion + BillGameCore VR project (Shmackle-style access pattern —
    /// see Docs/deprecated/Networking_Architecture.md §3 in the originating project). A concrete NetworkBehaviour
    /// (e.g. a networked avatar) implements this and registers itself with <see cref="BillPlayers"/> so gameplay
    /// code can reach "the local player's hands" or "player N's head" without hunting for a specific avatar type.
    /// </summary>
    public interface IBillPlayer
    {
        PlayerRef PlayerRef { get; }
        bool IsLocal { get; }
        Transform Head { get; }
        Transform HandLeft { get; }
        Transform HandRight { get; }
    }

    /// <summary>
    /// Static registry of live <see cref="IBillPlayer"/>s — the local player plus every remote player currently
    /// spawned. Game-agnostic and Fusion-only (lives beside <see cref="FusionNet"/>, the other reusable Fusion
    /// layer). A NetworkBehaviour implementation calls <see cref="Register"/> in its own <c>Spawned()</c> and
    /// <see cref="Unregister"/> in <c>Despawned()</c> — this registry does not spawn or own anything itself.
    ///
    /// Exposed on the facade as <c>Bill.Players</c> (see <c>Bill.cs</c>); usable directly as
    /// <see cref="BillPlayers"/> too.
    /// </summary>
    public static class BillPlayers
    {
        private static readonly Dictionary<PlayerRef, IBillPlayer> _byRef = new();
        private static readonly List<IBillPlayer> _all = new();

        public static IBillPlayer Local { get; private set; }
        public static IReadOnlyList<IBillPlayer> All => _all;

        public static event Action<IBillPlayer> PlayerRegistered;
        public static event Action<IBillPlayer> PlayerUnregistered;

        public static IBillPlayer Get(PlayerRef playerRef)
            => _byRef.TryGetValue(playerRef, out IBillPlayer p) ? p : null;

        public static void Register(IBillPlayer player)
        {
            if (player == null || _all.Contains(player)) return;
            if (player.IsLocal) Local = player;
            if (player.PlayerRef != PlayerRef.None) _byRef[player.PlayerRef] = player;
            _all.Add(player);
            PlayerRegistered?.Invoke(player);
        }

        public static void Unregister(IBillPlayer player)
        {
            if (player == null) return;
            if (ReferenceEquals(Local, player)) Local = null;
            if (player.PlayerRef != PlayerRef.None) _byRef.Remove(player.PlayerRef);
            _all.Remove(player);
            PlayerUnregistered?.Invoke(player);
        }
    }
}
#endif
