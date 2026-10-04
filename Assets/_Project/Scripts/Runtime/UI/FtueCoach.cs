using BillGameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.Stations;
using ZombieWar.UI;

namespace ZombieWar
{
    /// <summary>
    /// FTUE v2 coach marks that play over a live run (owner-approved mockup, 03/10):
    /// <list type="bullet">
    /// <item>the XP bar glows on the first three kills of the first run;</item>
    /// <item>the first time a station of each kind is on screen, a callout under it gives its name,
    /// what to do and what it pays; it goes when the player steps in, and the step is done;</item>
    /// <item>the first pickup of each mechanic item shows a toast for three seconds.</item>
    /// </list>
    /// Widgets live in the HUD under Overlays/FtueCoach and Safe/XpBar/FtueGlow (built by
    /// HordeCall/UI/FTUE/Build In-Run). Nothing here takes input or pauses the game.
    /// </summary>
    public sealed class FtueCoach : MonoBehaviour
    {
        const float ScanEvery = 0.2f, ToastSeconds = 3f, GlowKills = 3;

        RectTransform _callout, _toast, _arrow, _canvasRect;
        CanvasGroup _calloutGroup, _toastGroup;
        Image _glow;
        Canvas _canvas;
        Station _target;
        float _nextScan, _toastUntil;
        int _killsAtStart = -1;
        bool _xpCalled;

        struct StationText { public string name, how, reward, note; public Color color, ink; }

        static StationText TextFor(StationKind kind) => kind switch
        {
            StationKind.SignalRelay => new StationText { name = "SIGNAL RELAY", color = Hex(0x59D9FF), ink = Hex(0x06324A),
                how = "Stand inside for 12 s. Monsters keep coming; step out and it drains slowly.",
                reward = "Reward: pick 1 of 3 cards", note = "Once per relay" },
            StationKind.SupplyCache => new StationText { name = "SUPPLY CACHE", color = Hex(0xFFCC59), ink = Hex(0x3B2A00),
                how = "Stand inside and pay coins. The price shows on the pad and rises each buy.",
                reward = "Reward: pick 1 of 3 cards", note = "Price 60 coins, +40 each buy" },
            StationKind.BossBeacon => new StationText { name = "BOSS BEACON", color = Hex(0xFF5A46), ink = Color.white,
                how = "Step in to call a boss. It chases you until one of you falls.",
                reward = "Reward: a chest when the boss dies", note = "One boss at a time" },
            StationKind.SupplyDrop => new StationText { name = "SUPPLY DROP", color = Hex(0x8CFF80), ink = Hex(0x123B0C),
                how = "Stand by the pod for 1.5 s to open it.",
                reward = "Reward: an item and coins, sometimes a chest", note = "Once per pod" },
            _ => new StationText { name = "HEAL ZONE", color = Hex(0x73FFA0), ink = Hex(0x0C3B1C),
                how = "Stand inside for 1.5 s to switch it on, then stay in the field.",
                reward = "Heals 5% health per second for 8 s", note = "Recharges after 75 s" },
        };

        static (string name, string desc, Color color, string icon) ItemFor(PickupEffect e) => e switch
        {
            PickupEffect.Magnet => ("MAGNET", "Pulls every coin and gem on the map to you.", Hex(0x6FB6FF), "Magnet"),
            PickupEffect.Bomb => ("BOMB", "Wipes out the monsters on screen. Elites lose 30% health.", Hex(0xFF6A4A), "Bomb"),
            _ => ("FREEZE CLOCK", "Freezes every monster on screen for 4 s.", Hex(0x7FE7FF), "Freeze"),
        };

        void Awake()
        {
            _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;
            _callout = transform.Find("StationCallout") as RectTransform;
            _toast = transform.Find("ItemToast") as RectTransform;
            _arrow = _callout != null ? _callout.Find("Arrow") as RectTransform : null;
            _calloutGroup = Group(_callout);
            _toastGroup = Group(_toast);
            _glow = _canvasRect != null ? _canvasRect.Find("Safe/XpBar/FtueGlow")?.GetComponent<Image>() : null;
            if (_callout != null) _callout.gameObject.SetActive(false);
            if (_toast != null) _toast.gameObject.SetActive(false);
            if (_glow != null) _glow.gameObject.SetActive(false);
        }

        static CanvasGroup Group(RectTransform t)
        {
            if (t == null) return null;
            // Not "??": a missing component is a fake null to Unity, which "??" does not see.
            var g = t.GetComponent<CanvasGroup>();
            if (g == null) g = t.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false; g.interactable = false;
            return g;
        }

        void OnEnable() => Bill.Events?.Subscribe<PickupCollectedEvent>(OnPickup);
        void OnDisable() => Bill.Events?.Unsubscribe<PickupCollectedEvent>(OnPickup);

        void Update()
        {
            TickGlow();
            TickToast();
            if (Time.unscaledTime >= _nextScan) { _nextScan = Time.unscaledTime + ScanEvery; Scan(); }
            Place();
        }

        // ------------------------------------------------------------ XP glow
        void TickGlow()
        {
            if (_glow == null || Ftue.Done(Ftue.XpGlow)) { if (_glow != null && _glow.gameObject.activeSelf) _glow.gameObject.SetActive(false); return; }
            var run = RunState.Current;
            if (run == null) return;
            if (_killsAtStart < 0) _killsAtStart = run.Kills;
            int kills = run.Kills - _killsAtStart;
            if (kills >= GlowKills) { _glow.gameObject.SetActive(false); Ftue.Complete(Ftue.XpGlow); return; }
            if (kills >= 1 && !_xpCalled) { _xpCalled = true; FtueV3.Xp(_glow.rectTransform.parent as RectTransform); }
            if (!_glow.gameObject.activeSelf) _glow.gameObject.SetActive(true);
            var c = _glow.color; c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)); _glow.color = c;
        }

        // ------------------------------------------------------------ stations
        void Scan()
        {
            var player = PlayerMovement.Instance;
            if (_callout == null || player == null || Time.timeScale <= 0f) { Hide(); return; }
            if (_target != null)
            {
                bool inside = _target.Signal != null && _target.Signal.Contains(player.transform.position);
                if (inside || _target.Finished || _target.Gone)
                {
                    Ftue.Complete(Ftue.Station(_target.Anchor.kind));
                    _target = null; Hide();
                }
                else if (!OnScreen(_target.transform.position)) { _target = null; Hide(); }
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
            if (FtueV3.On)
            {
                var s = best;
                FtueV3.NewStation(s, () => _target == s);
                return;
            }
            Bind(TextFor(best.Anchor.kind));
            _callout.gameObject.SetActive(true);
            UIFx.PopIn(_callout, 0f, 0.85f, 0.25f);
        }

        void Bind(StationText t)
        {
            Set(_callout, "Head/Title", t.name);
            Set(_callout, "Head/Tag/Label", "NEW STATION");
            var tag = _callout.Find("Head/Tag")?.GetComponent<Image>(); if (tag != null) tag.color = t.color;
            var tagLabel = _callout.Find("Head/Tag/Label")?.GetComponent<TMP_Text>(); if (tagLabel != null) tagLabel.color = t.ink;
            Set(_callout, "How", t.how);
            Set(_callout, "Reward", t.reward);
            var reward = _callout.Find("Reward")?.GetComponent<TMP_Text>(); if (reward != null) reward.color = t.color;
            Set(_callout, "Note", t.note);
            var border = _callout.Find("Border")?.GetComponent<Image>(); if (border != null) border.color = t.color;
            var arrow = _arrow != null ? _arrow.GetComponent<Image>() : null; if (arrow != null) arrow.color = t.color;
        }

        /// The callout hangs under the station: its top a little below the pad's near edge, the
        /// arrow pointing back up at the pad, kept inside the screen.
        void Place()
        {
            if (_target == null || _callout == null || !_callout.gameObject.activeSelf || _canvasRect == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            float r = Station.RadiusFor(_target.Anchor.kind);
            Vector2 near = ToCanvas(cam, _target.transform.position + new Vector3(0f, 0f, -r * 1.15f));
            Vector2 centre = ToCanvas(cam, _target.transform.position);
            var size = _canvasRect.rect.size;
            float half = _callout.rect.width * 0.5f;
            float x = Mathf.Clamp(centre.x, -size.x * 0.5f + half + 24f, size.x * 0.5f - half - 24f);
            // Never over the hero: the card starts below the pad and below the player's feet.
            float top = near.y - 40f;
            var player = PlayerMovement.Instance;
            if (player != null) top = Mathf.Min(top, ToCanvas(cam, player.transform.position).y - 70f);
            float y = Mathf.Clamp(top, -size.y * 0.5f + _callout.rect.height + 260f, size.y * 0.5f - 300f);
            _callout.anchoredPosition = new Vector2(x, y);
            if (_arrow != null) _arrow.anchoredPosition = new Vector2(Mathf.Clamp(centre.x - x, -half + 60f, half - 60f), _arrow.anchoredPosition.y);
        }

        Vector2 ToCanvas(Camera cam, Vector3 world)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            var uiCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, sp, uiCam, out var local);
            return local;
        }

        static bool OnScreen(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var v = cam.WorldToViewportPoint(world);
            return v.z > 0f && v.x > 0.08f && v.x < 0.92f && v.y > 0.3f && v.y < 0.95f;
        }

        void Hide() { if (_callout != null && _callout.gameObject.activeSelf) _callout.gameObject.SetActive(false); }

        // ------------------------------------------------------------ items
        void OnPickup(PickupCollectedEvent e)
        {
            if (_toast == null) return;
            if (e.Effect != PickupEffect.Magnet && e.Effect != PickupEffect.Bomb && e.Effect != PickupEffect.Freeze) return;
            string step = Ftue.Item(e.Effect);
            if (Ftue.Done(step)) return;
            Ftue.Complete(step);
            var (name, desc, color, icon) = ItemFor(e.Effect);
            if (FtueV3.On)
            {
                FtueV3.Item(e.Effect, _toast.Find("Icon/" + icon)?.GetComponent<Image>()?.sprite);
                return;
            }
            Set(_toast, "Head/Title", name);
            Set(_toast, "Head/Tag/Label", "NEW ITEM");
            Set(_toast, "Desc", desc);
            var tag = _toast.Find("Head/Tag")?.GetComponent<Image>(); if (tag != null) tag.color = color;
            var border = _toast.Find("Border")?.GetComponent<Image>(); if (border != null) border.color = color;
            var icons = _toast.Find("Icon");
            if (icons != null) foreach (Transform c in icons) c.gameObject.SetActive(c.name == icon);
            _toast.gameObject.SetActive(true);
            UIFx.PopIn(_toast, 0f, 0.85f, 0.25f);
            _toastUntil = Time.unscaledTime + ToastSeconds;
        }

        void TickToast()
        {
            if (_toast == null || !_toast.gameObject.activeSelf) return;
            float left = _toastUntil - Time.unscaledTime;
            if (_toastGroup != null) _toastGroup.alpha = Mathf.Clamp01(left / 0.3f);
            if (left <= 0f) _toast.gameObject.SetActive(false);
        }

        static void Set(Transform root, string path, string value)
        {
            var t = root.Find(path)?.GetComponent<TMP_Text>();
            if (t != null) t.text = value;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    }
}
