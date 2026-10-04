using BillGameCore;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.Stations;
using ZombieWar.UI;

namespace ZombieWar
{
    /// <summary>
    /// In-run first-time steps (FTUE v3 radio call, boards FR_02b, FR_04, FR_05):
    /// <list type="bullet">
    /// <item>the XP bar glows on the first three kills of the first run, with Kaito's XP call;</item>
    /// <item>the first time a station of each kind is on screen, the radio names it
    /// (<see cref="FtueV3.NewStation"/>); the step is done when the player steps in;</item>
    /// <item>the first pickup of each mechanic item gets its radio card (<see cref="FtueV3.Item"/>).</item>
    /// </list>
    /// The v2 callout and toast widgets stay in the HUD prefab (owner-owned) but are kept hidden; the
    /// toast still holds the item icons the radio card shows. Switches itself off once every step is done.
    /// </summary>
    public sealed class FtueCoach : MonoBehaviour
    {
        const float ScanEvery = 0.2f, GlowKills = 3;

        [Tooltip("G12.8: the XP bar glow, the item icons the radio card shows, and the retired v2 widgets (kept hidden).")]
        [SerializeField] Image xpGlow;
        [SerializeField] Image magnetIcon, bombIcon, freezeIcon;
        [SerializeField] GameObject itemToast, stationCallout;
        Station _target;
        float _nextScan;
        int _killsAtStart = -1;
        bool _xpCalled;

        static readonly StationKind[] StationKinds = (StationKind[])System.Enum.GetValues(typeof(StationKind));
        static readonly PickupEffect[] ItemKinds = { PickupEffect.Magnet, PickupEffect.Bomb, PickupEffect.Freeze };

        Image IconFor(PickupEffect e) => e switch
        {
            PickupEffect.Magnet => magnetIcon,
            PickupEffect.Bomb => bombIcon,
            _ => freezeIcon,
        };

        void Awake()
        {
            if (stationCallout != null) stationCallout.SetActive(false);   // v2 widget
            if (itemToast != null) itemToast.SetActive(false);             // v2 widget; icons only
            if (xpGlow != null) xpGlow.gameObject.SetActive(false);
            if (AllDone()) enabled = false;
        }

        static bool AllDone()
        {
            if (!Ftue.Done(Ftue.XpGlow)) return false;
            foreach (var k in StationKinds) if (!Ftue.Done(Ftue.Station(k))) return false;
            foreach (var e in ItemKinds) if (!Ftue.Done(Ftue.Item(e))) return false;
            return true;
        }

        // Subscribed in Start/OnDestroy rather than OnEnable: the component disables itself when the
        // in-run steps are done, but a first pickup must still reach it until then.
        void Start() => Bill.Events?.Subscribe<PickupCollectedEvent>(OnPickup);
        void OnDestroy() => Bill.Events?.Unsubscribe<PickupCollectedEvent>(OnPickup);

        void Update()
        {
            TickGlow();
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanEvery;
            Scan();
            if (_target == null && AllDone()) enabled = false;
        }

        // ------------------------------------------------------------ XP glow
        void TickGlow()
        {
            if (xpGlow == null || Ftue.Done(Ftue.XpGlow)) { if (xpGlow != null && xpGlow.gameObject.activeSelf) xpGlow.gameObject.SetActive(false); return; }
            var run = RunState.Current;
            if (run == null) return;
            if (_killsAtStart < 0) _killsAtStart = run.Kills;
            int kills = run.Kills - _killsAtStart;
            if (kills >= GlowKills) { xpGlow.gameObject.SetActive(false); Ftue.Complete(Ftue.XpGlow); return; }
            if (kills >= 1 && !_xpCalled) { _xpCalled = true; FtueV3.Xp(xpGlow.rectTransform.parent as RectTransform); }
            if (!xpGlow.gameObject.activeSelf) xpGlow.gameObject.SetActive(true);
            var c = xpGlow.color; c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)); xpGlow.color = c;
        }

        // ------------------------------------------------------------ stations
        void Scan()
        {
            var player = PlayerMovement.Instance;
            if (player == null || Time.timeScale <= 0f) return;
            if (_target != null)
            {
                bool inside = _target.Signal != null && _target.Signal.Contains(player.transform.position);
                if (inside || _target.Finished || _target.Gone)
                {
                    Ftue.Complete(Ftue.Station(_target.Anchor.kind));
                    _target = null;
                }
                else if (!OnScreen(_target.transform.position)) _target = null;   // the radio card closes with it
                return;
            }
            Station best = null; float bestD = float.MaxValue;
            var stations = Station.Active;
            for (int i = 0; i < stations.Count; i++)
            {
                var s = stations[i];
                if (s == null || s.Finished || s.Gone || Ftue.Done(Ftue.Station(s.Anchor.kind))) continue;
                if (!OnScreen(s.transform.position)) continue;
                float d = (s.transform.position - player.transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            if (best == null) return;
            _target = best;
            ZombieWar.Audio.FtueVoice.StationCallout(best.Anchor.kind);
            var station = best;
            FtueV3.NewStation(station, () => _target == station);
        }

        static bool OnScreen(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var v = cam.WorldToViewportPoint(world);
            return v.z > 0f && v.x > 0.08f && v.x < 0.92f && v.y > 0.3f && v.y < 0.95f;
        }

        // ------------------------------------------------------------ items
        void OnPickup(PickupCollectedEvent e)
        {
            if (e.Effect != PickupEffect.Magnet && e.Effect != PickupEffect.Bomb && e.Effect != PickupEffect.Freeze) return;
            string step = Ftue.Item(e.Effect);
            if (Ftue.Done(step)) return;
            Ftue.Complete(step);
            var icon = IconFor(e.Effect);
            FtueV3.Item(e.Effect, icon != null ? icon.sprite : null);
        }
    
#if UNITY_EDITOR
        /// G12.8: wires what the coach used to find by path; returns the paths not found.
        public System.Collections.Generic.List<string> EditorWire()
        {
            var m = new System.Collections.Generic.List<string>();
            var canvas = GetComponentInParent<Canvas>(true);
            var root = canvas != null ? canvas.rootCanvas.transform : transform.root;
            xpGlow = WireUtil.Find<Image>(root, "Safe/XpBar/FtueGlow", m);
            itemToast = WireUtil.Node(transform, "ItemToast", m);
            magnetIcon = WireUtil.Find<Image>(transform, "ItemToast/Icon/Magnet", m);
            bombIcon = WireUtil.Find<Image>(transform, "ItemToast/Icon/Bomb", m);
            freezeIcon = WireUtil.Find<Image>(transform, "ItemToast/Icon/Freeze", m);
            var callout = transform.Find("StationCallout");
            stationCallout = callout != null ? callout.gameObject : null;   // optional: a retired widget
            return m;
        }

        public System.Collections.Generic.List<string> EditorUnwired() => WireUtil.Nulls(
            ("xpGlow", xpGlow), ("itemToast", itemToast), ("magnetIcon", magnetIcon), ("bombIcon", bombIcon), ("freezeIcon", freezeIcon));
#endif
}
}
