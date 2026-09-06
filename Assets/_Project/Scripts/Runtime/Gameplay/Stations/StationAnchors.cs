using UnityEngine;

namespace ZombieWar.Stations
{
    /// <summary>
    /// M7.3 — deterministic, GLOBAL station anchors.
    ///
    /// Anchors are a pure function of <c>(worldSeed, cell, slot)</c>. They are never chunk-local
    /// random, which is what makes them survive chunk recycling: a chunk that unloads and reloads
    /// re-derives exactly the same anchor rather than rolling a new one. Nothing is stored per chunk,
    /// so nothing can drift.
    ///
    /// The claim/complete ledger lives in <see cref="StationRegistry"/>, keyed by the same stable id.
    /// </summary>
    public static class StationAnchors
    {
        /// <summary>
        /// Metres per anchor cell. One candidate anchor per cell keeps spacing predictable.
        ///
        /// TUNING, and lowered from 60 m after measuring the real game: at 60 m the nearest station
        /// sat 42-96 m from the player, which at the player's 5 m/s is 8-20 seconds of running
        /// through a horde with no compass. Stations that far apart are effectively invisible.
        /// </summary>
        public const float CellSize = 28f;      // TUNING (was 60)

        /// <summary>Not every cell gets a station — this is the fraction that do.</summary>
        public const float Density = 0.40f;     // TUNING (was 0.55; smaller cells need lower density)

        public readonly struct Anchor
        {
            public readonly long id;
            public readonly Vector3 position;
            public readonly StationKind kind;

            public Anchor(long id, Vector3 position, StationKind kind)
            {
                this.id = id; this.position = position; this.kind = kind;
            }
            public bool IsValid => id != 0;
        }

        /// <summary>Stable id for a cell. Same seed + same cell = same id, forever.</summary>
        public static long IdFor(int cellX, int cellZ) =>
            ((long)cellX << 32) ^ (uint)cellZ;

        static uint Hash(int seed, int x, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 2166136261u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)z) * 16777619u;
                h = (h ^ (uint)salt) * 16777619u;
                h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
                return h;
            }
        }

        static float Unit(int seed, int x, int z, int salt) => Hash(seed, x, z, salt) / (float)uint.MaxValue;

        /// <summary>
        /// The anchor for a cell, or an invalid anchor when this cell has none. Deterministic and
        /// allocation-free, so it is safe to call while streaming.
        /// </summary>
        public static Anchor ForCell(int worldSeed, int cellX, int cellZ)
        {
            if (Unit(worldSeed, cellX, cellZ, 11) > Density) return default;

            // Jitter inside the cell so anchors do not form a visible grid, but stay clear of edges.
            float jx = Mathf.Lerp(0.25f, 0.75f, Unit(worldSeed, cellX, cellZ, 23));
            float jz = Mathf.Lerp(0.25f, 0.75f, Unit(worldSeed, cellX, cellZ, 31));

            var pos = new Vector3((cellX + jx) * CellSize, 0f, (cellZ + jz) * CellSize);

            // Type mix: Relay is the backbone (it is what teaches the player to travel), Cache is
            // frequent enough for Coin to matter, Beacon is rare so it stays an event. TUNING.
            float roll = Unit(worldSeed, cellX, cellZ, 47);
            StationKind kind = roll < 0.45f ? StationKind.SignalRelay
                             : roll < 0.80f ? StationKind.SupplyCache
                             : StationKind.BossBeacon;

            return new Anchor(IdFor(cellX, cellZ), pos, kind);
        }

        /// <summary>Cell coordinate containing a world position.</summary>
        public static void CellOf(Vector3 world, out int cellX, out int cellZ)
        {
            cellX = Mathf.FloorToInt(world.x / CellSize);
            cellZ = Mathf.FloorToInt(world.z / CellSize);
        }

        /// <summary>
        /// Fills <paramref name="results"/> with every anchor in the square ring of cells around a
        /// position. Caller-supplied buffer: no allocation on the streaming path.
        /// </summary>
        public static int Around(int worldSeed, Vector3 world, int cellRadius, Anchor[] results)
        {
            CellOf(world, out int cx, out int cz);
            int n = 0;
            for (int x = cx - cellRadius; x <= cx + cellRadius && n < results.Length; x++)
                for (int z = cz - cellRadius; z <= cz + cellRadius && n < results.Length; z++)
                {
                    var a = ForCell(worldSeed, x, z);
                    if (a.IsValid) results[n++] = a;
                }
            return n;
        }
    }
}
