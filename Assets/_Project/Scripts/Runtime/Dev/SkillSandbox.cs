using System.Collections;
using System.Collections.Generic;
using BillGameCore;
using UnityEngine;
using ZombieWar.Skills;
using ZombieWar.Stations;
using ZombieWar.Threat;

namespace ZombieWar.Dev
{
    /// Dev-only skill test bench (M8; rebuilt as the phase-A dev bench, 2026-09-29). Lives in the
    /// SkillSandbox scene, started with ZombieWar/Dev/Play Skill Sandbox.
    ///
    /// Stops the horde, makes the player immortal, and brings enemies on demand: pinned dummies
    /// (immortal or mortal) to watch an effect from the same spot, or a live horde of any enemy type
    /// that walks at the player, refilled to a set count, for DPS and stress tests. Cards are set to
    /// any rank from the panel, damage is tracked per source (DamageLedger), and the capture tab
    /// writes frame bursts and contact sheets for review. Panel: SkillSandbox.Panel.cs; capture:
    /// SkillSandbox.Capture.cs.
    public sealed partial class SkillSandbox : MonoBehaviour
    {
        public enum EnemyMode { Dummies, Horde }

        [SerializeField] private int dummyCount = 3;
        [SerializeField] private float ringRadius = 6f;

        public static SkillSandbox Instance { get; private set; }

        public EnemyMode Mode { get; private set; } = EnemyMode.Dummies;
        public int Count => dummyCount;
        /// <summary>Enemy type to bring; null = a mix of the whole roster.</summary>
        public ZombieData Enemy { get; private set; }
        /// Horde mode: replace killed enemies so the count stays up (steady DPS readings).
        public bool KeepCount { get; set; } = true;
        /// Dummies die (and stand back up 0.8 s later): for cards that need kills (Soul Burst, Reaper, Execution).
        public bool Mortal { get => SandboxDummy.Mortal; set => SandboxDummy.Mortal = value; }
        /// The player walks a slow circle: for cards that need movement (Fire Trail, Kinetic Shield, Quickstep).
        public bool Walk { get; set; }
        public bool God { get; private set; }

        private readonly List<SandboxDummy> _dummies = new();
        private readonly List<Vector3> _slots = new();
        private readonly List<ZombieBase> _horde = new();
        private readonly List<ZombieData> _roster = new();
        private ZombieSpawner _spawner;
        private string _status = "starting…";
        private float _walkAngle;
        private Component _joystick;
        private System.Reflection.MethodInfo _applyDir;
        private float _respawnAt;

        public IReadOnlyList<ZombieData> Roster => _roster;

        private void Awake()
        {
            Instance = this;
#if UNITY_EDITOR
            // ZombieWar/Dev/Play Skin Viewer: the same bench, with the gun-skin turntable on top.
            if (UnityEditor.SessionState.GetBool("zw.skinviewer", false))
            {
                UnityEditor.SessionState.SetBool("zw.skinviewer", false);
                gameObject.AddComponent<WeaponSkinViewer>();
            }
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
            DamageLedger.Enabled = false;
        }

        private IEnumerator Start()
        {
            // The run (player, skills, director) comes up through the normal world start.
            while (PlayerMovement.Instance == null || SkillRuntime.Active == null || ThreatDirector.Instance == null)
                yield return null;
            yield return null;

            ThreatDirector.Instance.enabled = false;             // no horde in the sandbox
            ClearEnemies();
            SetGod(true);

            _spawner = FindFirstObjectByType<ZombieSpawner>();
            _roster.Clear();
            ThreatDirector.Instance.CollectRoster(_roster);
            var stations = FindFirstObjectByType<StationDirector>();
            if (stations != null && stations.BossRoster != null)
                foreach (var d in stations.BossRoster) if (d != null && !_roster.Contains(d)) _roster.Add(d);
            Enemy = ThreatDirector.Instance.PickFor(0);

            DamageLedger.Enabled = true;
            DamageLedger.Reset();
            SetDummies(dummyCount);
            _status = "ready";
        }

        // ------------------------------------------------------------------ enemies

        public void SetEnemy(ZombieData data)
        {
            Enemy = data;
            Rebuild();
        }

        public void SetMode(EnemyMode mode)
        {
            Mode = mode;
            Rebuild();
        }

        public void SetDummies(int count)
        {
            dummyCount = Mathf.Clamp(count, 1, 250);
            Rebuild();
        }

        private ZombieData PickEnemy() =>
            Enemy != null ? Enemy : _roster.Count > 0 ? _roster[Random.Range(0, _roster.Count)] : null;

        /// Removes every enemy and brings the chosen set again (mode, type, count).
        public void Rebuild()
        {
            ClearEnemies();
            if (_spawner == null || PlayerMovement.Instance == null) { _status = "no spawner"; return; }
            if (Mode == EnemyMode.Dummies) BuildDummies();
            else for (int i = 0; i < dummyCount; i++) SpawnHordeOne();
            _status = Mode == EnemyMode.Dummies ? "dummies" : "horde";
        }

        private void BuildDummies()
        {
            var center = PlayerMovement.Instance.transform.position;
            for (int i = 0; i < dummyCount; i++)
            {
                var data = PickEnemy();
                if (data == null) break;
                _spawner.EnsureRegistered(data, dummyCount);
                var z = _spawner.Spawn(data);
                if (z == null) { _status = "spawn failed: " + data.name; continue; }
                // Always on screen (skills like Airstrike only target what the camera sees): three
                // in a row above the player, more spread over a grid of the visible ground.
                Vector3 pos = dummyCount <= 3
                    ? OnScreen(0.3f + 0.2f * i, 0.72f, center + new Vector3((i - 1) * 3f, 0f, ringRadius))
                    : OnScreen(0.14f + 0.72f * ((i % 5) + 0.5f) / 5f, 0.2f + 0.65f * ((i / 5) + 0.5f) / Mathf.Ceil(dummyCount / 5f), center);
                if ((pos - center).sqrMagnitude < 4f) pos += (pos - center).normalized * 2f + Vector3.right * 0.01f;
                _slots.Add(pos);
                _dummies.Add(Place(z, pos, center));
            }
        }

        private bool SpawnHordeOne()
        {
            var data = PickEnemy();
            if (data == null) return false;
            _spawner.EnsureRegistered(data, dummyCount);
            var z = _spawner.Spawn(data);
            if (z == null) { _status = "spawn failed: " + data.name; return false; }
            _horde.Add(z);
            return true;
        }

        public void ClearEnemies()
        {
            foreach (var d in _dummies) if (d != null) Destroy(d);
            _dummies.Clear();
            _slots.Clear();
            _horde.Clear();
            foreach (var z in FindObjectsByType<ZombieBase>(FindObjectsSortMode.None))
            {
                var dummy = z.GetComponent<SandboxDummy>();
                if (dummy != null) Destroy(dummy);
                Bill.Pool?.Return(z.gameObject);
            }
        }

        /// Ground point under a viewport position (y = 0 plane); falls back when there is no camera.
        private static Vector3 OnScreen(float vx, float vy, Vector3 fallback)
        {
            var cam = Camera.main;
            if (cam == null) return fallback;
            var ray = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return fallback;
            float t = -ray.origin.y / ray.direction.y;
            return t > 0f ? ray.origin + ray.direction * t : fallback;
        }

        private static SandboxDummy Place(ZombieBase z, Vector3 pos, Vector3 lookAt)
        {
            var d = z.gameObject.GetComponent<SandboxDummy>() ?? z.gameObject.AddComponent<SandboxDummy>();
            d.Pin(pos, lookAt);
            return d;
        }

        private void Update()
        {
            if (_spawner == null || PlayerMovement.Instance == null) return;
            if (Mode == EnemyMode.Dummies) RefillDummies();
            else RefillHorde();
            DriveWalk();
            SuppressLevelUp();
            TickFps();
        }

        private void RefillDummies()
        {
            // Mortal dummies: refill empty slots shortly after a death.
            if (Time.unscaledTime < _respawnAt) return;
            for (int i = 0; i < _dummies.Count; i++)
            {
                var d = _dummies[i];
                if (d != null && d.isActiveAndEnabled && !d.Dead) continue;
                _respawnAt = Time.unscaledTime + 0.8f;
                if (d != null) Destroy(d);
                var data = PickEnemy();
                var z = data != null ? _spawner.Spawn(data) : null;
                if (z != null) _dummies[i] = Place(z, _slots[i], PlayerMovement.Instance.transform.position);
                break;
            }
        }

        private void RefillHorde()
        {
            _horde.RemoveAll(z => z == null || !z.gameObject.activeInHierarchy || z.IsDead);
            if (!KeepCount) return;
            // A few per frame: a full wipe refills over a handful of frames instead of one spike.
            for (int n = 0; n < 8 && _horde.Count < dummyCount; n++)
                if (!SpawnHordeOne()) break;
        }

        public int AliveCount => Mode == EnemyMode.Dummies ? _dummies.Count : _horde.Count;

        // ------------------------------------------------------------------ player

        public void SetGod(bool on)
        {
            if (God == on) return;
            God = on;
            Bill.Cheat?.Execute("zw.god");       // the cheat is a toggle; God mirrors its state
        }

        private RunOverlays _overlays;
        private float _lastScale = 1f;

        /// Kills earn XP; a level-up would pause the bench and hand out random cards. The sandbox
        /// closes it without a pick and keeps the chosen time scale.
        private void SuppressLevelUp()
        {
            if (Time.timeScale > 0f) _lastScale = Time.timeScale;
            if (_overlays == null) _overlays = FindFirstObjectByType<RunOverlays>(FindObjectsInactive.Include);
            if (_overlays == null) return;
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var t = typeof(RunOverlays);
            var root = t.GetField("levelUpRoot", F)?.GetValue(_overlays) as GameObject;
            if (root == null || !root.activeSelf) return;
            t.GetField("_pendingLevelUps", F)?.SetValue(_overlays, 0);
            t.GetField("_skillOffer", F)?.SetValue(_overlays, null);
            root.SetActive(false);
            if (!_paused) Time.timeScale = _lastScale;
        }

        private void DriveWalk()
        {
            if (_joystick == null)
            {
                System.Type type = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    if ((type = a.GetType("BillGameCore.BillVirtualJoystick")) != null) break;
                if (type == null) return;
                _joystick = FindFirstObjectByType(type) as Component;
                _applyDir = type.GetMethod("ApplyDirection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (_joystick == null || _applyDir == null) return;
            }
            if (!Walk) return;
            _walkAngle += Time.deltaTime * 1.2f;
            _applyDir.Invoke(_joystick, new object[] { new Vector2(Mathf.Cos(_walkAngle), Mathf.Sin(_walkAngle)) * 0.8f });
        }

        public void StopWalk()
        {
            Walk = false;
            if (_joystick != null) _applyDir?.Invoke(_joystick, new object[] { Vector2.zero });
        }

        // ------------------------------------------------------------------ time

        private bool _paused;
        private float _speed = 1f;
        public float Speed => _speed;
        public bool Paused => _paused;

        public void SetSpeed(float speed)
        {
            _speed = Mathf.Clamp(speed, 0.05f, 2f);
            if (!_paused) Time.timeScale = _speed;
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : _speed;
        }

        private float _fpsSmoothed = 60f;
        private float _worstMs;
        public float Fps => _fpsSmoothed;
        /// Longest recent frame (peak hold, sinks 10 ms per second): spikes stay readable.
        public float WorstFrameMs => _worstMs;

        private void TickFps()
        {
            float dt = Mathf.Max(1e-4f, Time.unscaledDeltaTime);
            _fpsSmoothed = Mathf.Lerp(_fpsSmoothed, 1f / dt, 0.05f);
            _worstMs = Mathf.Max(dt * 1000f, _worstMs - 10f * dt);
        }

        // ------------------------------------------------------------------ camera

        public enum View { Game, Close, Side, Top }
        public View CurrentView { get; private set; } = View.Game;

        public void SetView(View view)
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow == null) return;
            CurrentView = view;
            switch (view)
            {
                case View.Game: follow.ResetView(); break;
                case View.Close: follow.SetView(new Vector3(0f, 6.5f, -4.3f), new Vector3(56f, 0f, 0f)); break;
                // High three-quarter view: low side angles got blocked by the nearest bush or rock.
                case View.Side: follow.SetView(new Vector3(7.5f, 6f, -5.5f), new Vector3(35f, -54f, 0f)); break;
                case View.Top: follow.SetView(new Vector3(0f, 15f, -0.05f), new Vector3(89.8f, 0f, 0f)); break;
            }
        }

        // ------------------------------------------------------------------ skills

        /// Puts the best playable gun of a family in the player's hands, so that family's signature
        /// cards can be tested.
        public static bool EquipFamily(WeaponClass family)
        {
            var weapon = FindFirstObjectByType<Weapon>();
            var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.AllData() : null;
            if (weapon == null || all == null) return false;
            WeaponData best = null;
            foreach (var d in all)
                if (d != null && d.IsPlayable && d.weaponClass == family && (best == null || d.tier > best.tier)) best = d;
            return best != null && weapon.Equip(best);
        }

        /// Clears every card and every live skill effect, like a fresh run (no leftover shield or drones).
        public static void ResetSkills()
        {
            SkillRuntime.Active?.Reset();
            SkillArsenal.Instance?.ResetForRun();
        }

        /// Sets one card to an exact rank (0 removes it) by rebuilding the whole build in a valid
        /// order: base cards first, evolutions last (they need their power maxed and partner owned).
        public static void SetRank(string id, int rank)
        {
            var run = SkillRuntime.Active;
            if (run == null) return;
            var build = new Dictionary<string, int>(run.Ranks);
            if (rank <= 0) build.Remove(id); else build[id] = rank;
            ApplyBuild(build);
        }

        public static void ApplyBuild(IReadOnlyDictionary<string, int> build)
        {
            ResetSkills();
            var run = SkillRuntime.Active;
            if (run == null) return;
            foreach (var kv in build)
            {
                var def = SkillCatalogDefs.ById(kv.Key);
                if (def == null || def.IsEvolution) continue;
                for (int r = 0; r < kv.Value; r++) if (!run.Take(kv.Key)) break;
            }
            foreach (var kv in build)
            {
                var def = SkillCatalogDefs.ById(kv.Key);
                if (def == null || !def.IsEvolution) continue;
                if (run.Take(kv.Key)) SkillCombatDriver.Instance?.OnEvolutionTaken();
            }
        }

        /// Maxes a card; for an evolution, first maxes its power and takes its partner.
        public static void Max(string id)
        {
            var def = SkillCatalogDefs.ById(id);
            var run = SkillRuntime.Active;
            if (def == null || run == null) return;
            if (def.IsEvolution)
            {
                while (run.Take(def.evolvesFrom)) { }
                run.Take(def.partner);
            }
            while (run.Take(id)) { }
            if (def.IsEvolution) SkillCombatDriver.Instance?.OnEvolutionTaken();
        }

        public static string BuildString()
        {
            var run = SkillRuntime.Active;
            if (run == null) return "";
            var parts = new List<string>();
            foreach (var kv in run.Ranks) parts.Add(kv.Key + ":" + kv.Value);
            return string.Join(",", parts);
        }

        public static void ApplyBuildString(string s)
        {
            var build = new Dictionary<string, int>();
            foreach (var part in (s ?? "").Split(','))
            {
                var kv = part.Trim().Split(':');
                if (kv.Length == 2 && int.TryParse(kv[1], out int r) && SkillCatalogDefs.ById(kv[0]) != null) build[kv[0]] = r;
            }
            ApplyBuild(build);
        }
    }

    /// An enemy that never dies and never moves: health refills on every hit (after the hit flash
    /// and damage number have played) and its position is pinned each frame.
    public sealed class SandboxDummy : MonoBehaviour
    {
        public static bool Mortal;
        public bool Dead => _health != null && _health.IsDead;
        private Vector3 _pos;
        private Quaternion _rot;
        private Health _health;
        private System.Action<float> _refill;

        public void Pin(Vector3 pos, Vector3 lookAt)
        {
            _pos = pos;
            var dir = lookAt - pos; dir.y = 0f;
            _rot = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : Quaternion.identity;
            transform.SetPositionAndRotation(_pos, _rot);
            if (_health == null)
            {
                _health = GetComponent<Health>();
                _refill = _ => { if (!Mortal) _health.ResetHealth(); };
                if (_health != null) _health.OnDamaged += _refill;
            }
            _health?.ResetHealth();
        }

        private void LateUpdate() => transform.SetPositionAndRotation(_pos, _rot);

        // A pooled enemy must not stay immortal once it leaves the sandbox.
        private void OnDestroy()
        {
            if (_health != null && _refill != null) _health.OnDamaged -= _refill;
        }
    }
}
