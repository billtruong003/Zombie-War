using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Dev
{
    /// DPS bench (phase A2/A10): each power alone at a rank, against the same immortal dummies, for
    /// the same time, damage read from the ledger. One CSV per run under Review/M8/bench/, so a
    /// refactor or a balance pass can be compared power by power against the last run.
    public sealed partial class SkillSandbox
    {
        /// Powers the bench measures by default: every autonomous power and every evolution.
        public static IEnumerable<string> BenchIds()
        {
            foreach (var d in SkillCatalogDefs.All)
                if (d.layer == SkillLayer.Autonomous || d.IsEvolution) yield return d.id;
        }

        public bool Benching { get; private set; }
        public string LastBenchFile { get; private set; }

        /// Runs <see cref="BenchIds"/> (or <paramref name="ids"/>) at <paramref name="rank"/> for
        /// <paramref name="seconds"/> of game time each, at <paramref name="timeScale"/>.
        public IEnumerator Bench(string label, int rank = 5, float seconds = 20f, float timeScale = 2f,
                                 int dummies = 8, IEnumerable<string> ids = null, bool crowd = false)
        {
            Benching = true;
            var list = new List<string>(ids ?? BenchIds());
            var csv = new StringBuilder("id,rank,seconds,dps,hits,gun_dps\n");
            float before = Time.timeScale;
            bool walk = Walk, mortal = Mortal;
            // Pinned dummies spread over the screen, or (A10) an immortal horde that swarms the player.
            ImmortalHorde = crowd;
            SetMode(crowd ? EnemyMode.Horde : EnemyMode.Dummies);
            SetDummies(dummies);

            foreach (var id in list)
            {
                var def = SkillCatalogDefs.ById(id);
                if (def == null) continue;
                var build = new Dictionary<string, int>();
                if (def.IsEvolution) { build[def.evolvesFrom] = rank; build[def.partner] = 5; build[id] = 1; }
                else build[id] = rank;
                ApplyBuild(build);
                // Cards that need movement or kills to fire at all.
                // Turning Walk off alone leaves the stick held: the player walked off the dummies.
                if (id == SkillCatalogDefs.AutoFireTrail) Walk = true; else StopWalk();
                Mortal = id == SkillCatalogDefs.AutoSoulBurst || id == SkillCatalogDefs.EvoReaper;
                Rebuild();
                Time.timeScale = timeScale;
                // Let the first proc happen off the clock (a crowd also needs time to arrive).
                yield return new WaitForSeconds(crowd ? 4f : 1.5f);

                DamageLedger.Reset();
                float start = Time.time;
                while (Time.time - start < seconds) yield return null;
                float dt = Time.time - start;

                // A power's damage is booked under its own id and, once evolved, the evolution's.
                float total = 0f; int hits = 0;
                var rows = new List<DamageLedger.Row>();
                DamageLedger.Snapshot(rows);
                float gun = 0f;
                foreach (var r in rows)
                {
                    if (r.source == DamageLedger.Gun) { gun = r.total; continue; }
                    if (r.source == id || (def.IsEvolution && r.source == def.evolvesFrom) ||
                        (!def.IsEvolution && SkillCatalogDefs.EvolutionOf(id)?.id == r.source))
                    { total += r.total; hits += r.hits; }
                }
                csv.Append($"{id},{rank},{dt:0.0},{total / dt:0.0},{hits},{gun / dt:0.0}\n");
                Debug.Log($"[Bench] {id} r{rank}: {total / dt:0.0} dps ({hits} hits)");
            }

            ApplyBuild(new Dictionary<string, int>());
            ImmortalHorde = false;
            if (walk) Walk = true; else StopWalk();
            Mortal = mortal;
            Time.timeScale = before;
            string dir = System.IO.Path.Combine(Application.dataPath, "..", "Review", "M8", "bench");
            System.IO.Directory.CreateDirectory(dir);
            LastBenchFile = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, System.DateTime.Now.ToString("MMdd_HHmmss") + "_" + label + ".csv"));
            System.IO.File.WriteAllText(LastBenchFile, csv.ToString());
            Debug.Log("[Bench] " + LastBenchFile);
            Benching = false;
        }
    }
}
