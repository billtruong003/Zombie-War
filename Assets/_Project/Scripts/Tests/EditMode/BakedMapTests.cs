using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.World;

namespace ZombieWar.Tests
{
    /// <summary>
    /// Asset truth for the baked maps (2026-10-01): every theme the game can load has all its chunks,
    /// a spawn spot on the map, monsters of its own that are real VAT enemies, and its own light.
    /// A rebake that drops a chunk or a roster entry fails here, not as a hole in the world in play.
    /// </summary>
    public class BakedMapTests
    {
        static readonly string[] Themes = { "meadow", "forest", "swamp", "volcano", "tundra" };

        [Test]
        public void EveryTheme_Loads_WithAllItsChunks()
        {
            foreach (var id in Themes)
            {
                var t = MapTheme.Load(id);
                Assert.IsNotNull(t, $"MapTheme_{id} is missing from Resources/MapThemes");
                Assert.AreEqual(id, t.id);
                int n = t.chunksPerSide;
                Assert.Greater(n, 0, id);
                Assert.AreEqual(n * n, t.chunks.Length, $"{id}: chunk list size");
                Assert.IsTrue(t.chunks.All(c => c != null), $"{id}: a chunk prefab is missing (not baked or not committed)");
            }
        }

        [Test]
        public void ChunkAt_Wraps_BothWays()
        {
            var t = MapTheme.Load("meadow");
            int n = t.chunksPerSide;
            Assert.AreSame(t.ChunkAt(0, 0), t.ChunkAt(n, n));
            Assert.AreSame(t.ChunkAt(n - 1, n - 1), t.ChunkAt(-1, -1));
            Assert.AreSame(t.ChunkAt(2, 1), t.ChunkAt(2 - 3 * n, 1 + 5 * n));
        }

        [Test]
        public void SpawnPoint_IsInsideTheMap()
        {
            foreach (var id in Themes)
            {
                var t = MapTheme.Load(id);
                Assert.That(t.spawnPoint.x, Is.InRange(0f, t.MapSize), id);
                Assert.That(t.spawnPoint.y, Is.InRange(0f, t.MapSize), id);
            }
        }

        [Test]
        public void EveryTheme_HasItsOwnMonsters_AndThemAreRealEnemies()
        {
            foreach (var id in Themes)
            {
                var t = MapTheme.Load(id);
                Assert.Greater(t.crowd.Length, 0, $"{id}: no crowd of its own");
                Assert.Greater(t.elites.Length, 0, $"{id}: no elite of its own");
                foreach (var z in t.crowd.Concat(t.later).Concat(t.elites))
                {
                    Assert.IsNotNull(z, $"{id}: an empty roster slot");
                    Assert.IsNotNull(z.prefab, $"{id}: {z.name} has no prefab");
                    Assert.IsNotNull(z.vatData, $"{id}: {z.name} has no VAT data");
                }
                Assert.IsTrue(t.elites.All(e => e.isElite), $"{id}: an elite slot holds a non-elite");
            }
        }

        [Test]
        public void EveryTheme_LightsItself()
        {
            foreach (var id in Themes)
            {
                var t = MapTheme.Load(id);
                Assert.Greater(t.sunIntensity, 0.5f, id);
                // Ambient brighter than the scene's dark default sky (0.21, 0.23, 0.26), or the map
                // goes back to looking dark everywhere.
                Assert.Greater(t.ambientSky.grayscale, 0.4f, id);
                Assert.GreaterOrEqual(t.ambientSky.grayscale, t.ambientGround.grayscale, $"{id}: lit from below");
            }
        }
    }
}
