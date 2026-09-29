using System.Collections.Generic;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Dev
{
    // The on-screen bench panel (IMGUI, dev only): Skills · Enemies · View · DPS · Capture.
    public sealed partial class SkillSandbox
    {
        enum Tab { Skills, Enemies, View, Dps, Capture }

        private bool _panelOpen = true;
        private Tab _tab = Tab.Skills;
        private Vector2 _scroll;
        private int _layerFilter = -1;               // -1 all, else (int)SkillLayer
        private string _buildText = "";
        private string _captureLabel = "skill";
        private int _burstFrames = 12;
        private float _burstGap = 0.12f;
        private float _burstScale = 0.35f;
        private readonly List<DamageLedger.Row> _rows = new(32);

        static readonly (string label, WeaponClass family)[] Families =
        {
            ("PIS", WeaponClass.Sidearm), ("SMG", WeaponClass.SMG), ("AR", WeaponClass.AssaultRifle),
            ("SG", WeaponClass.Shotgun), ("LMG", WeaponClass.LMG), ("MK", WeaponClass.Marksman), ("GL", WeaponClass.Rocket),
        };

        private void OnGUI()
        {
            if (Capturing && !_panelOpen) return;
            float s = Screen.height / 1920f * 2.2f;
            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s, h = Screen.height / s;

            if (GUI.Button(new Rect(w - 110, h - 60, 100, 40), _panelOpen ? "Hide" : "Sandbox")) _panelOpen = !_panelOpen;
            if (!_panelOpen) { GUI.matrix = m; return; }

            GUILayout.BeginArea(new Rect(6, h * 0.40f, w - 12, h * 0.52f), GUI.skin.box);
            GUILayout.Label($"SANDBOX · {_status} · {Mode} {AliveCount}/{Count} · ×{(Paused ? 0f : Speed):0.##} · {Fps:0} fps ({1000f / Mathf.Max(1f, Fps):0.0} ms, worst {WorstFrameMs:0} ms)");
            GUILayout.BeginHorizontal();
            foreach (Tab t in System.Enum.GetValues(typeof(Tab)))
                if (GUILayout.Toggle(_tab == t, t.ToString(), GUI.skin.button)) _tab = t;
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Skills: DrawSkills(w); break;
                case Tab.Enemies: DrawEnemies(); break;
                case Tab.View: DrawView(); break;
                case Tab.Dps: DrawDps(w); break;
                case Tab.Capture: DrawCapture(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = m;
        }

        private void DrawSkills(float w)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset")) ResetSkills();
            foreach (var (label, fam) in Families) if (GUILayout.Button(label)) EquipFamily(fam);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_layerFilter == -1, "All", GUI.skin.button)) _layerFilter = -1;
            foreach (SkillLayer l in System.Enum.GetValues(typeof(SkillLayer)))
                if (GUILayout.Toggle(_layerFilter == (int)l, l.ToString(), GUI.skin.button)) _layerFilter = (int)l;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            _buildText = GUILayout.TextField(_buildText, GUILayout.MinWidth(w * 0.5f));
            if (GUILayout.Button("Copy build")) _buildText = BuildString();
            if (GUILayout.Button("Apply")) ApplyBuildString(_buildText);
            GUILayout.EndHorizontal();

            var rt = SkillRuntime.Active;
            foreach (var def in SkillCatalogDefs.All)
            {
                if (_layerFilter != -1 && (int)def.layer != _layerFilter) continue;
                int rank = rt != null ? rt.RankOf(def.id) : 0;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{def.displayName}  {rank}/{def.maxRank}", GUILayout.Width(w * 0.5f));
                if (GUILayout.Button("0")) SetRank(def.id, 0);
                if (GUILayout.Button("−")) SetRank(def.id, rank - 1);
                if (GUILayout.Button("+")) { if (def.IsEvolution) Max(def.id); else SetRank(def.id, Mathf.Min(def.maxRank, rank + 1)); }
                if (GUILayout.Button("MAX")) Max(def.id);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawEnemies()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(Mode == EnemyMode.Dummies, "Dummies", GUI.skin.button) && Mode != EnemyMode.Dummies) SetMode(EnemyMode.Dummies);
            if (GUILayout.Toggle(Mode == EnemyMode.Horde, "Horde", GUI.skin.button) && Mode != EnemyMode.Horde) SetMode(EnemyMode.Horde);
            if (GUILayout.Button(Mortal ? "Mortal ✓" : "Mortal")) Mortal = !Mortal;
            if (GUILayout.Button(KeepCount ? "Refill ✓" : "Refill")) KeepCount = !KeepCount;
            if (GUILayout.Button("Clear")) ClearEnemies();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            foreach (int n in new[] { 1, 3, 10, 30, 60, 120, 200 })
                if (GUILayout.Button(n.ToString())) SetDummies(n);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Stress: 200 horde + full build")) StressTest();
            if (GUILayout.Button("Rebuild")) Rebuild();
            GUILayout.EndHorizontal();

            GUILayout.Label("Enemy type");
            if (GUILayout.Toggle(Enemy == null, "Mix (whole roster)", GUI.skin.button) && Enemy != null) SetEnemy(null);
            foreach (var d in Roster)
                if (d != null && GUILayout.Toggle(Enemy == d, d.name, GUI.skin.button) && Enemy != d) SetEnemy(d);
        }

        private void DrawView()
        {
            GUILayout.BeginHorizontal();
            foreach (float v in new[] { 1f, 0.5f, 0.25f, 0.1f })
                if (GUILayout.Button("×" + v)) SetSpeed(v);
            if (GUILayout.Button(Paused ? "Resume" : "Pause")) SetPaused(!Paused);
            GUILayout.EndHorizontal();
            SetSpeed(GUILayout.HorizontalSlider(Speed, 0.05f, 1f));

            GUILayout.BeginHorizontal();
            foreach (View v in System.Enum.GetValues(typeof(View)))
                if (GUILayout.Toggle(CurrentView == v, v.ToString(), GUI.skin.button) && CurrentView != v) SetView(v);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(God ? "God ✓" : "God")) SetGod(!God);
            if (GUILayout.Button(Walk ? "Walk ✓" : "Walk")) { if (Walk) StopWalk(); else Walk = true; }
            if (GUILayout.Button(AllowLevelUp ? "Level-up ✓" : "Level-up")) AllowLevelUp = !AllowLevelUp;
            if (GUILayout.Button("Chest") && PlayerMovement.Instance != null)
                PickupManager.Instance?.SpawnChest(PlayerMovement.Instance.transform.position + PlayerMovement.Instance.transform.forward * 2.5f);
            if (GUILayout.Button("+1 level")) RunState.Current?.AddXp(RunState.Current.XpForNextLevel);
            GUILayout.EndHorizontal();
        }

        private void DrawDps(float w)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset meter")) DamageLedger.Reset();
            foreach (float win in new[] { 3f, 5f, 10f, 30f })
                if (GUILayout.Toggle(Mathf.Approximately(DamageLedger.WindowSeconds, win), win + " s", GUI.skin.button))
                    DamageLedger.WindowSeconds = win;
            GUILayout.EndHorizontal();
            GUILayout.Label($"since reset {DamageLedger.Elapsed:0.0} s · window {DamageLedger.WindowSeconds:0} s");

            DamageLedger.Snapshot(_rows);
            float total = 0f;
            foreach (var r in _rows) total += r.windowDps;
            GUILayout.Label($"TOTAL {total:0} dps");
            foreach (var r in _rows)
            {
                var def = SkillCatalogDefs.ById(r.source);
                string name = def != null ? def.displayName : r.source;
                int rank = def != null && SkillRuntime.Active != null ? SkillRuntime.Active.RankOf(def.id) : 0;
                GUILayout.BeginHorizontal();
                GUILayout.Label(rank > 0 ? $"{name} r{rank}" : name, GUILayout.Width(w * 0.4f));
                GUILayout.Label($"{r.windowDps,7:0} dps  avg {r.averageDps:0}  total {r.total:0}  hits {r.hits}");
                GUILayout.EndHorizontal();
            }
        }

        private void DrawCapture()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Label");
            _captureLabel = GUILayout.TextField(_captureLabel);
            GUILayout.EndHorizontal();
            GUILayout.Label($"Burst: {_burstFrames} frames, every {_burstGap:0.00} s, at ×{_burstScale:0.00}");
            _burstFrames = Mathf.RoundToInt(GUILayout.HorizontalSlider(_burstFrames, 4, 24));
            _burstGap = GUILayout.HorizontalSlider(_burstGap, 0.04f, 0.5f);
            _burstScale = GUILayout.HorizontalSlider(_burstScale, 0.05f, 1f);
            GUILayout.BeginHorizontal();
            GUI.enabled = !Capturing;
            if (GUILayout.Button("Snap")) StartCoroutine(Snap(_captureLabel));
            if (GUILayout.Button("Burst + sheet")) StartCoroutine(Burst(_captureLabel, _burstFrames, _burstGap, _burstScale));
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label(Capturing ? "capturing…" : LastCaptureFolder ?? "");
        }

        /// Worst case in one click: 200 live enemies of the whole roster and every power at max.
        public void StressTest()
        {
            Enemy = null;
            Mode = EnemyMode.Horde;
            dummyCount = 200;
            KeepCount = true;
            Rebuild();
            foreach (var def in SkillCatalogDefs.All)
                if (def.layer == SkillLayer.Autonomous) Max(def.id);
            DamageLedger.Reset();
        }
    }
}
