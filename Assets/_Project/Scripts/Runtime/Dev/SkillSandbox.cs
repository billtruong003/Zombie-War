using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BillGameCore;
using UnityEngine;
using ZombieWar.Skills;
using ZombieWar.Threat;

namespace ZombieWar.Dev
{
    /// Dev-only skill test bench (M8 review, owner request 2026-09-26). Lives in the
    /// SkillSandbox scene, which is started with ZombieWar/Dev/Play Skill Sandbox.
    ///
    /// Stops the horde, makes the player immortal, and stands a few immortal dummies in front of
    /// the player so every skill can be granted, maxed and watched from the same spot. A small
    /// on-screen panel grants or maxes any card, changes the dummy count (3 / 10 / 30 to see chain
    /// and airstrike on a crowd) and slows time to study an effect.
    public sealed class SkillSandbox : MonoBehaviour
    {
        [SerializeField] private int dummyCount = 3;
        [SerializeField] private float ringRadius = 6f;

        private readonly List<SandboxDummy> _dummies = new();
        private ZombieSpawner _spawner;
        private ZombieData _dummyData;
        private Vector2 _scroll;
        private bool _panelOpen = true;
        private string _status = "starting…";
        private readonly List<Vector3> _slots = new();
        private float _walkAngle;
        private Component _joystick;
        private System.Reflection.MethodInfo _applyDir;

        /// Dummies die (and stand back up 0.8 s later): for cards that need kills (Soul Burst, Reaper, Execution).
        public bool Mortal { get => SandboxDummy.Mortal; set => SandboxDummy.Mortal = value; }
        /// The player walks a slow circle: for cards that need movement (Fire Trail, Kinetic Shield, Quickstep).
        public bool Walk { get; set; }

        public static SkillSandbox Instance { get; private set; }

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; Time.timeScale = 1f; }

        private IEnumerator Start()
        {
            // The run (player, skills, director) comes up through the normal world start.
            while (PlayerMovement.Instance == null || SkillRuntime.Active == null || ThreatDirector.Instance == null)
                yield return null;
            yield return null;

            ThreatDirector.Instance.enabled = false;             // no horde in the sandbox
            foreach (var z in FindObjectsByType<ZombieBase>(FindObjectsSortMode.None))
                if (z.GetComponent<SandboxDummy>() == null) Bill.Pool?.Return(z.gameObject);
            Bill.Cheat?.Execute("zw.god");

            _spawner = FindFirstObjectByType<ZombieSpawner>();
            _dummyData = ThreatDirector.Instance.PickFor(0);
            SetDummies(dummyCount);
            _status = "ready";
        }

        public void SetDummies(int count)
        {
            dummyCount = Mathf.Clamp(count, 1, 60);
            foreach (var d in _dummies) if (d != null) { Destroy(d); Bill.Pool?.Return(d.gameObject); }
            _dummies.Clear();
            _slots.Clear();
            if (_spawner == null || _dummyData == null) { _status = "no spawner / data"; return; }

            _spawner.EnsureRegistered(_dummyData, dummyCount);
            var center = PlayerMovement.Instance.transform.position;
            for (int i = 0; i < dummyCount; i++)
            {
                var z = _spawner.Spawn(_dummyData);
                if (z == null) continue;
                // Three in a row in front of the player; more fill rings, so a chain has hops.
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

        private SandboxDummy Place(ZombieBase z, Vector3 pos, Vector3 lookAt)
        {
            var d = z.gameObject.GetComponent<SandboxDummy>() ?? z.gameObject.AddComponent<SandboxDummy>();
            d.Pin(pos, lookAt);
            return d;
        }

        private float _respawnAt;

        private void Update()
        {
            if (_spawner == null || PlayerMovement.Instance == null) return;
            // Mortal dummies: refill empty slots shortly after a death.
            if (Time.unscaledTime >= _respawnAt)
                for (int i = 0; i < _dummies.Count; i++)
                {
                    var d = _dummies[i];
                    if (d != null && d.isActiveAndEnabled && !d.Dead) continue;
                    _respawnAt = Time.unscaledTime + 0.8f;
                    if (d != null) Destroy(d);
                    var z = _spawner.Spawn(_dummyData);
                    if (z != null) _dummies[i] = Place(z, _slots[i], PlayerMovement.Instance.transform.position);
                    break;
                }
            DriveWalk();
            SuppressLevelUp();
        }

        private RunOverlays _overlays;
        private float _lastScale = 1f;

        /// Kills in Mortal mode earn XP; a level-up would pause the bench and hand out random cards.
        /// The sandbox closes it without a pick and keeps the chosen time scale.
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
            Time.timeScale = _lastScale;
        }

        private void DriveWalk()
        {
            if (_joystick == null)
            {
                var type = System.AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("BillGameCore.BillVirtualJoystick")).FirstOrDefault(t => t != null);
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

        // ------------------------------------------------------------------ review capture

        /// <summary>
        /// Captures every card in <paramref name="specs"/> to PNGs for review. Spec format:
        /// "cardId|family|dummies|mortal|walk|waitSeconds" (family: - for the default SMG).
        /// Frames are taken at <paramref name="timeScale"/> every <paramref name="gap"/> real seconds.
        /// Emergency Detonation turns god mode off for its own capture (it needs the player hurt).
        /// </summary>
        public IEnumerator Capture(string[] specs, string folder, int frames, float gap, float timeScale)
        {
            _panelOpen = false;
            System.IO.Directory.CreateDirectory(folder);
            while (ThreatDirector.Instance == null || ThreatDirector.Instance.enabled) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (var spec in specs)
            {
                var f = spec.Split('|');
                string id = f[0];
                ResetSkills(); StopWalk(); Mortal = false; Time.timeScale = 1f;
                EquipFamily(f.Length > 1 && f[1] != "-" ? (WeaponClass)System.Enum.Parse(typeof(WeaponClass), f[1]) : WeaponClass.SMG);
                SetDummies(f.Length > 2 ? int.Parse(f[2]) : 10);
                yield return new WaitForSecondsRealtime(1.2f);
                Mortal = f.Length > 3 && f[3] == "1";
                Walk = f.Length > 4 && f[4] == "1";
                Bill.Cheat?.Execute("zw.skill.max " + id);
                Health hp = null;
                if (id == SkillCatalogDefs.AutoEmergency)
                {
                    Bill.Cheat?.Execute("zw.god");
                    hp = PlayerMovement.Instance.GetComponent<Health>();
                    hp.TakeDamage(hp.Max * 0.85f);
                }
                yield return new WaitForSecondsRealtime(f.Length > 5 ? float.Parse(f[5], System.Globalization.CultureInfo.InvariantCulture) : 0.5f);
                Time.timeScale = timeScale;
                for (int i = 0; i < frames; i++)
                {
                    yield return new WaitForEndOfFrame();
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    var rt = RenderTexture.GetTemporary(540, 960);
                    Graphics.Blit(tex, rt);
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var sm = new Texture2D(540, 960, TextureFormat.RGB24, false);
                    sm.ReadPixels(new Rect(0, 0, 540, 960), 0, 0); sm.Apply();
                    RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, $"{id}_{i:00}.png"), sm.EncodeToPNG());
                    Destroy(tex); Destroy(sm);
                    yield return new WaitForSecondsRealtime(gap);
                }
                Time.timeScale = 1f;
                if (hp != null) { Bill.Cheat?.Execute("zw.god"); hp.ResetHealth(); }
                Debug.Log("[SandboxCapture] " + id);
            }
            StopWalk(); Mortal = false;
            Debug.Log("[SandboxCapture] END");
        }

        /// <summary>
        /// Fires a power right now (bypassing its cooldown) and captures the frames after it, so a
        /// review never misses the moment. Spec: "cardToGrant|powerId|targets|radius" (radius: a
        /// number, or "value" for the card's own value, or "frost" for the Frost Nova radius).
        /// </summary>
        public IEnumerator ForceCapture(string[] specs, string folder, int frames, float gap, float timeScale)
        {
            _panelOpen = false;
            System.IO.Directory.CreateDirectory(folder);
            while (ThreatDirector.Instance == null || ThreatDirector.Instance.enabled) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            var apply = typeof(SkillCombatDriver).GetMethod("Apply", F);
            var value = typeof(SkillRuntime).GetMethod("Value", F);
            foreach (var spec in specs)
            {
                var f = spec.Split('|');
                ResetSkills(); Time.timeScale = 1f; SetDummies(10);
                yield return new WaitForSecondsRealtime(1f);
                Bill.Cheat?.Execute("zw.skill.max " + f[0]);
                yield return new WaitForSecondsRealtime(0.2f);
                var run = SkillRuntime.Active;
                float radius = f[3] == "value" ? (float)value.Invoke(run, new object[] { f[1] })
                             : f[3] == "frost" ? run.FrostRadius
                             : float.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture);
                var proc = new SkillRuntime.PowerProc { skillId = f[1], targets = int.Parse(f[2]), radius = radius };
                Time.timeScale = timeScale;
                apply.Invoke(SkillCombatDriver.Instance, new object[] { run, proc, PlayerMovement.Instance.transform.position });
                for (int i = 0; i < frames; i++)
                {
                    yield return CaptureFrame(System.IO.Path.Combine(folder, $"{f[0]}_{i:00}.png"));
                    yield return new WaitForSecondsRealtime(gap);
                }
                Time.timeScale = 1f;
            }
            Debug.Log("[SandboxCapture] END");
        }

        private static IEnumerator CaptureFrame(string file)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var rt = RenderTexture.GetTemporary(540, 960);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var sm = new Texture2D(540, 960, TextureFormat.RGB24, false);
            sm.ReadPixels(new Rect(0, 0, 540, 960), 0, 0); sm.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            System.IO.File.WriteAllBytes(file, sm.EncodeToPNG());
            Destroy(tex); Destroy(sm);
        }

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

        // ------------------------------------------------------------------ panel

        private void OnGUI()
        {
            float s = Screen.height / 1920f * 2.2f;
            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s, h = Screen.height / s;

            if (GUI.Button(new Rect(w - 110, h - 60, 100, 40), _panelOpen ? "Hide" : "Sandbox")) _panelOpen = !_panelOpen;
            if (!_panelOpen) { GUI.matrix = m; return; }

            GUILayout.BeginArea(new Rect(6, h * 0.42f, w - 12, h * 0.5f), GUI.skin.box);
            GUILayout.Label($"SKILL SANDBOX · {_status} · dummies {dummyCount} · time ×{Time.timeScale:0.##}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("3")) SetDummies(3);
            if (GUILayout.Button("10")) SetDummies(10);
            if (GUILayout.Button("30")) SetDummies(30);
            if (GUILayout.Button("×1")) Time.timeScale = 1f;
            if (GUILayout.Button("×0.25")) Time.timeScale = 0.25f;
            if (GUILayout.Button("Reset")) ResetSkills();
            if (GUILayout.Button(Mortal ? "Mortal ✓" : "Mortal")) Mortal = !Mortal;
            if (GUILayout.Button(Walk ? "Walk ✓" : "Walk")) { if (Walk) StopWalk(); else Walk = true; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (var (label, fam) in new[] { ("PIS", WeaponClass.Sidearm), ("SMG", WeaponClass.SMG), ("AR", WeaponClass.AssaultRifle),
                                                 ("SG", WeaponClass.Shotgun), ("LMG", WeaponClass.LMG), ("MK", WeaponClass.Marksman) })
                if (GUILayout.Button(label)) EquipFamily(fam);
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll);
            var rt = SkillRuntime.Active;
            foreach (var group in SkillCatalogDefs.All.GroupBy(d => d.layer))
            {
                GUILayout.Label(group.Key.ToString().ToUpperInvariant());
                foreach (var def in group)
                {
                    GUILayout.BeginHorizontal();
                    int rank = rt != null ? rt.RankOf(def.id) : 0;
                    GUILayout.Label($"{def.displayName}  {rank}/{def.maxRank}", GUILayout.Width(w * 0.55f));
                    if (GUILayout.Button("+1")) Bill.Cheat?.Execute("zw.skill " + def.id);
                    if (GUILayout.Button("MAX")) Bill.Cheat?.Execute("zw.skill.max " + def.id);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = m;
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
