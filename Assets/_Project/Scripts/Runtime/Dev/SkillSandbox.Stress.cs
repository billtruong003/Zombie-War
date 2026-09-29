using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Dev
{
    /// Stress run (phase A10): a crowd of <c>count</c> enemies swarming the player while a full late
    /// build fires every effect it has. Frame time (average, 95th percentile, worst), draw calls,
    /// batches, triangles and GC per frame, sampled after a warm-up. The numbers are the Editor's —
    /// a device run is the one that decides — but a regression shows up here first.
    public sealed partial class SkillSandbox
    {
        public bool Stressing { get; private set; }
        public string LastStressReport { get; private set; }

        /// A late-run build of the heaviest effects: six evolved powers whose partners are exactly the
        /// four stat slots (Damage, Area, Cooldown, Max Health), so every card fits.
        public static readonly string[] StressBuild =
        {
            SkillCatalogDefs.EvoCarpetBomb, SkillCatalogDefs.EvoMeteorStorm, SkillCatalogDefs.EvoPlague,
            SkillCatalogDefs.EvoBlizzard, SkillCatalogDefs.EvoSingularity, SkillCatalogDefs.EvoFortress,
        };

        public IEnumerator Stress(string label, int count = 200, float seconds = 20f, bool fullBuild = true, string[] build = null)
        {
            build ??= StressBuild;
            Stressing = true;
            _panelOpen = false;
            bool immortal = ImmortalHorde;
            ImmortalHorde = true;
            SetMode(EnemyMode.Horde);
            SetDummies(count);
            ApplyBuild(new Dictionary<string, int>());
            if (fullBuild) foreach (var id in build) Max(id);
            Time.timeScale = 1f;
            yield return new WaitForSeconds(6f);   // the crowd arrives and every power has fired once

            var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var frames = new List<float>(4096);
            long drawSum = 0, batchSum = 0, triSum = 0, gcSum = 0; int samples = 0, alive = 0;
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000f);
                drawSum += draws.LastValue; batchSum += batches.LastValue; triSum += tris.LastValue; gcSum += gc.LastValue;
                samples++;
                alive = Mathf.Max(alive, AliveCount);
            }
            draws.Dispose(); batches.Dispose(); tris.Dispose(); gc.Dispose();

            frames.Sort();
            float avg = 0f; foreach (var f in frames) avg += f; avg /= Mathf.Max(1, frames.Count);
            float p95 = frames.Count > 0 ? frames[Mathf.Min(frames.Count - 1, (int)(frames.Count * 0.95f))] : 0f;
            float worst = frames.Count > 0 ? frames[frames.Count - 1] : 0f;
            int n = Mathf.Max(1, samples);
            var sb = new StringBuilder();
            sb.Append($"label,{label}\nenemies,{alive}\nbuild,{(fullBuild ? string.Join(" ", build) : "none")}\n");
            sb.Append($"frames,{frames.Count}\navg_ms,{avg:0.00}\np95_ms,{p95:0.00}\nworst_ms,{worst:0.00}\n");
            sb.Append($"draw_calls,{drawSum / n}\nbatches,{batchSum / n}\ntriangles,{triSum / n}\ngc_bytes_per_frame,{gcSum / n}\n");
            LastStressReport = sb.ToString();

            string dir = System.IO.Path.Combine(Application.dataPath, "..", "Review", "M8", "bench");
            System.IO.Directory.CreateDirectory(dir);
            LastBenchFile = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, System.DateTime.Now.ToString("MMdd_HHmmss") + "_" + label + ".csv"));
            System.IO.File.WriteAllText(LastBenchFile, LastStressReport);
            Debug.Log("[Stress] " + LastBenchFile + "\n" + LastStressReport);

            ApplyBuild(new Dictionary<string, int>());
            ImmortalHorde = immortal;
            SetMode(EnemyMode.Dummies);
            SetDummies(8);
            Stressing = false;
        }

        /// Every power and evolution alone in the same held crowd: frame time and draw calls per card,
        /// one CSV. Finds the one effect that spends the frame (A10 found Blizzard at 650 draw calls).
        public IEnumerator StressSweep(string label, int count = 200, float seconds = 5f, IEnumerable<string> ids = null)
        {
            Stressing = true;
            _panelOpen = false;
            bool immortal = ImmortalHorde;
            ImmortalHorde = true;
            SetMode(EnemyMode.Horde);
            SetDummies(count);
            Time.timeScale = 1f;
            yield return new WaitForSeconds(6f);
            var csv = new StringBuilder("id,avg_ms,p95_ms,worst_ms,loop_avg_ms,loop_p95_ms,draw_calls,gc_bytes\n");
            var loops = new List<float>(1024);
            var frames = new List<float>(1024);
            foreach (var id in new List<string>(ids ?? BenchIds()))
            {
                ApplyBuild(new Dictionary<string, int>());
                Max(id);
                if (id == SkillCatalogDefs.AutoFireTrail) Walk = true; else StopWalk();
                yield return new WaitForSeconds(1.5f);
                var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                // The game's own CPU time: the Editor's repaints and stalls are outside PlayerLoop.
                var loop = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "PlayerLoop");
                frames.Clear(); loops.Clear(); long d = 0, g = 0;
                float end = Time.unscaledTime + seconds;
                while (Time.unscaledTime < end)
                {
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime * 1000f); d += draws.LastValue; g += gc.LastValue;
                    if (loop.Valid && loop.LastValue > 0) loops.Add(loop.LastValue / 1e6f);
                }
                draws.Dispose(); gc.Dispose(); loop.Dispose();
                frames.Sort(); loops.Sort();
                float lavg = 0f; foreach (var l in loops) lavg += l; lavg /= Mathf.Max(1, loops.Count);
                float lp95 = loops.Count > 0 ? loops[Mathf.Min(loops.Count - 1, (int)(loops.Count * 0.95f))] : 0f;
                float avg = 0f; foreach (var f in frames) avg += f; avg /= Mathf.Max(1, frames.Count);
                int n = Mathf.Max(1, frames.Count);
                csv.Append($"{id},{avg:0.0},{frames[Mathf.Min(frames.Count - 1, (int)(frames.Count * 0.95f))]:0.0},{frames[frames.Count - 1]:0.0},{lavg:0.0},{lp95:0.0},{d / n},{g / n}\n");
            }
            StopWalk();
            ApplyBuild(new Dictionary<string, int>());
            ImmortalHorde = immortal;
            SetMode(EnemyMode.Dummies);
            SetDummies(8);
            string dir = System.IO.Path.Combine(Application.dataPath, "..", "Review", "M8", "bench");
            System.IO.Directory.CreateDirectory(dir);
            LastBenchFile = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, System.DateTime.Now.ToString("MMdd_HHmmss") + "_" + label + ".csv"));
            System.IO.File.WriteAllText(LastBenchFile, csv.ToString());
            Debug.Log("[StressSweep] " + LastBenchFile);
            Stressing = false;
        }

        /// Style review (phase A10): every power and evolution alone on the same swarming crowd,
        /// three frames each into one folder (Review/M8/sandbox/..._style/<id>_<n>.png), so the whole
        /// set can be laid side by side and checked for one visual language.
        public IEnumerator StyleSheet(string label, IEnumerable<string> ids = null, int crowd = 18)
        {
            Stressing = true;
            _panelOpen = false;
            bool immortal = ImmortalHorde;
            ImmortalHorde = true;
            SetMode(EnemyMode.Horde);
            SetDummies(crowd);
            string folder = NewFolder(label);
            Time.timeScale = 1f;
            yield return new WaitForSeconds(3f);
            foreach (var id in new List<string>(ids ?? BenchIds()))
            {
                ApplyBuild(new Dictionary<string, int>());
                Max(id);
                if (id == SkillCatalogDefs.AutoFireTrail) Walk = true; else StopWalk();
                yield return new WaitForSeconds(1.6f);
                for (int i = 0; i < 3; i++)
                {
                    yield return CaptureFrame(System.IO.Path.Combine(folder, $"{id}_{i}.png"), null);
                    yield return new WaitForSeconds(0.45f);
                }
            }
            StopWalk();
            ApplyBuild(new Dictionary<string, int>());
            ImmortalHorde = immortal;
            SetMode(EnemyMode.Dummies);
            SetDummies(8);
            LastCaptureFolder = folder;
            Stressing = false;
        }
    }
}
