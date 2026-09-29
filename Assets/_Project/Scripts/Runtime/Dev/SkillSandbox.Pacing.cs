using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ZombieWar.Skills;
using ZombieWar.Threat;

namespace ZombieWar.Dev
{
    /// Pacing run (phase A10): the real horde (ThreatDirector on), the player circling in god mode,
    /// every level-up answered at once with the offer's first card and every chest claimed. One row
    /// per level: when it came, how full the build is. Answers "at what minute is a build complete?"
    /// without a person playing for twenty minutes. CSV under Review/M8/bench/.
    public sealed partial class SkillSandbox
    {
        public bool Pacing { get; private set; }

        public IEnumerator PacingRun(string label, float minutes = 15f, float timeScale = 4f)
        {
            Pacing = true;
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var overlaysType = typeof(RunOverlays);
            var pick = overlaysType.GetMethod("PickPerk", F);
            var claim = overlaysType.GetMethod("ClaimChest", F);
            var offerField = overlaysType.GetField("_skillOffer", F);
            var chestOpen = overlaysType.GetProperty("ChestOpen", F);
            var levelRootField = overlaysType.GetField("levelUpRoot", F);

            bool walk = Walk, allow = AllowLevelUp;
            ApplyBuild(new Dictionary<string, int>());
            ClearEnemies();
            SetGod(true);
            Walk = true;
            AllowLevelUp = true;
            var threat = ThreatDirector.Instance;
            if (threat != null) threat.enabled = true;

            var run = RunState.Current;
            var skills = SkillRuntime.Active;
            float start = Time.time;
            int startLevel = run != null ? run.Level : 1;
            int lastLevel = startLevel;
            int kills0 = run != null ? run.Kills : 0;
            float fullAt = -1f;
            var csv = new StringBuilder("level,seconds,kills,skills,stats,rank_sum,maxed,evolved,picked\n");
            string picked = "";

            while (Time.time - start < minutes * 60f && run != null && !run.IsOver)
            {
                if (_overlays == null) _overlays = FindFirstObjectByType<RunOverlays>(FindObjectsInactive.Include);
                if (_overlays != null)
                {
                    var root = levelRootField?.GetValue(_overlays) as GameObject;
                    if (root != null && root.activeSelf && offerField?.GetValue(_overlays) is List<SkillDef> offer && offer.Count > 0)
                    {
                        picked = offer[0].id;
                        pick?.Invoke(_overlays, new object[] { 0 });
                    }
                    if (chestOpen != null && (bool)chestOpen.GetValue(_overlays)) claim?.Invoke(_overlays, null);
                }
                Time.timeScale = timeScale;

                if (run.Level != lastLevel)
                {
                    lastLevel = run.Level;
                    int maxed = 0, rankSum = 0, evolved = 0;
                    foreach (var kv in skills.Ranks)
                    {
                        var def = SkillCatalogDefs.ById(kv.Key);
                        if (def == null) continue;
                        if (def.IsEvolution) { evolved++; continue; }
                        if (def.Slot == SkillSlot.None) continue;
                        rankSum += kv.Value;
                        if (skills.IsMaxRank(kv.Key)) maxed++;
                    }
                    int slots = skills.SlotsUsed(SkillSlot.Skill), stats = skills.SlotsUsed(SkillSlot.Stat);
                    float t = Time.time - start;
                    csv.Append($"{run.Level},{t:0},{run.Kills - kills0},{slots},{stats},{rankSum},{maxed},{evolved},{picked}\n");
                    if (fullAt < 0f && maxed >= SkillCatalogDefs.MaxSkillSlots + SkillCatalogDefs.MaxStatSlots) fullAt = t;
                }
                yield return null;
            }

            csv.Append($"# full build at {(fullAt < 0 ? "never" : fullAt.ToString("0") + " s")}, {minutes} min run, level {startLevel}->{lastLevel}\n");
            Time.timeScale = 1f;
            if (threat != null) threat.enabled = false;
            ClearEnemies();
            ApplyBuild(new Dictionary<string, int>());
            if (walk) Walk = true; else StopWalk();
            AllowLevelUp = allow;
            string dir = System.IO.Path.Combine(Application.dataPath, "..", "Review", "M8", "bench");
            System.IO.Directory.CreateDirectory(dir);
            LastBenchFile = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, System.DateTime.Now.ToString("MMdd_HHmmss") + "_" + label + ".csv"));
            System.IO.File.WriteAllText(LastBenchFile, csv.ToString());
            Debug.Log("[Pacing] " + LastBenchFile);
            Pacing = false;
        }
    }
}
