using System;
using System.Collections.Generic;
using BillGameCore;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.Audio;

namespace ZombieWar.UI
{
    /// <summary>
    /// FTUE v3 "radio call" presentation (canvas page "FTUE v3 · radio call", boards FR_01–FR_12),
    /// built from the UI Labs components: the radio card (normal, small, with item icon), the
    /// targeting corners + chip, the hologram tapping hand, the energy ring, a dim and the spotlight
    /// hole. Everything lives on its own overlay canvas (prefab Resources/UI/RadioOverlay, built by
    /// HordeCall/UI/Radio/Build Overlay Prefab) above every screen and never takes input, so no game
    /// screen prefab is edited. Screens describe a step with a <see cref="Call"/>; positions are in
    /// the 1080×1920 reference space, top-left origin, like the mockup ×2.77.
    /// Radio lines that are not a step's own line (agent chatter, "done" and nudge lines) show as a
    /// small subtitle card where the last step card was, or under the HUD top strip.
    /// </summary>
    public sealed class FtueRadio : MonoBehaviour
    {
        public enum Size { Normal, Small, Item }

        /// One FTUE step on screen.
        public sealed class Call
        {
            public string Id, VoiceId, Agent, Context, Title, Body, Chip;
            public Size Size;
            public Sprite Icon;
            /// Top of the card in reference pixels from the top of the safe area.
            public float Y;
            /// Screen dim 0..1 (0 = none). With <see cref="Spotlight"/> the dim has a hole on the target.
            public float Dim;
            public bool Spotlight, Ring;
            /// Screen-space rect (pixels) the corners frame; null hides them. Re-read every frame.
            public Func<Rect?> Target;
            /// <summary>A second rect the spotlight also opens (e.g. the BUY button next to the gun).</summary>
            public Func<Rect?> Target2;
            /// <summary>Put the chip over the target's top edge instead of under it (a list item
            /// whose next sibling sits right below, like the suggested level-up card).</summary>
            public bool ChipAbove;
            /// Screen-space point the hand taps; null hides it. Re-read every frame.
            public Func<Vector2?> Hand;
            /// The call ends itself when this turns false (its screen closed, the step is done).
            public Func<bool> Alive;
            /// Seconds before it ends itself once shown (0 = stays until Hide or Alive turns false).
            public float Seconds;
            /// A modal step (a sheet that pauses the game or a menu screen) goes in front of any
            /// in-run step; in-run steps wait their turn in order.
            public bool Modal;
            internal int Seq;
            internal float ShownAt = -1f, QueuedAt;
        }

        [SerializeField] private RectTransform safe;
        [SerializeField] private RadioCallView cardNormal, cardSmall, cardItem, cardSub;
        [SerializeField] private Image itemIcon, dim, spot, ring, reticle;
        [SerializeField] private UISpotlightHole spotHole;
        [SerializeField] private RectTransform spotTarget, chip, handRoot;
        [SerializeField] private TMPro.TMP_Text chipText;
        [SerializeField] private string[] agentIds = new string[0];
        [SerializeField] private Sprite[] faces = new Sprite[0];
        [SerializeField] private float fade = 0.18f, subtitleTop = 282f, reticlePad = 18f;

        static FtueRadio _instance;
        static readonly Dictionary<string, (string name, string call)> Agents = new()
        {
            ["riley"] = ("RILEY", "NIGHTFIN"), ["lukas"] = ("LUKAS", "RAPTOR"), ["chen"] = ("CHEN", "DRAGON"),
            ["kaito"] = ("KAITO", "SHARK"), ["jiho"] = ("JI-HO", "SMOG"), ["mai"] = ("MAI", "TIGER"),
        };

        [Serializable] class Line { public string id, agent, en; }
        [Serializable] class Catalog { public Line[] lines; }
        readonly Dictionary<string, Line> _lines = new();

        Call _call;
        readonly List<Call> _pending = new();
        int _seq;
        float _subUntil, _lastY = -1f, _lastAt = -99f;
        bool _subscribed;
        RadioCallView _card;
        CanvasGroup _cardGroup, _subGroup;
        CanvasGroup[] _stepGroups;     // cached: this runs every frame for the whole session
        RadioCallView[] _stepViews;
        Rect _lastSafe;
        float _placedY;
        RectTransform _spotTarget2;

        // ------------------------------------------------------------ public API

        /// Queues a step; the one on screen is the newest modal step, else the oldest in-run step.
        public static void Show(Call c)
        {
            var r = Ensure();
            if (r == null || c == null) return;
            r._pending.RemoveAll(p => p.Id == c.Id);
            c.Seq = ++r._seq;
            c.QueuedAt = Time.unscaledTime;
            r._pending.Add(c);
        }

        public static void Hide(string id)
        {
            if (_instance == null) return;
            _instance._pending.RemoveAll(p => p.Id == id);
        }

        public static bool Showing(string id) => _instance != null && _instance._call != null && _instance._call.Id == id;

        // Owner decision 04/10 (A): radio subtitles stay hidden while a modal is up (revive, level-up,
        // chest, pause, unlock popups) — the voice still plays. Modal owners register their root once;
        // the radio checks activeInHierarchy, so no show/hide bookkeeping can be forgotten.
        static readonly List<GameObject> Modals = new();

        public static void RegisterModal(GameObject root)
        {
            if (root != null && !Modals.Contains(root)) Modals.Add(root);
        }

        static bool AnyModalOpen()
        {
            for (int i = Modals.Count - 1; i >= 0; i--)
            {
                var m = Modals[i];
                if (m == null) { Modals.RemoveAt(i); continue; }
                if (m.activeInHierarchy) return true;
            }
            return false;
        }

        static readonly Vector3[] Corners = new Vector3[4];

        /// Screen rect of a UI element (any canvas), or null when it is missing or hidden.
        public static Rect? ScreenRect(RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return null;
            var c = Corners;
            rt.GetWorldCorners(c);
            var canvas = rt.GetComponentInParent<Canvas>()?.rootCanvas;
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// Screen rect around a flat world disc (a station pad), or null when off screen.
        public static Rect? WorldDisc(Vector3 centre, float radius)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var p = cam.WorldToScreenPoint(centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
                if (p.z <= 0f) return null;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// Where the hand taps on a UI element: a little right of and below its centre.
        public static Vector2? TapPoint(RectTransform rt)
        {
            var r = ScreenRect(rt);
            return r.HasValue ? new Vector2(r.Value.center.x + r.Value.width * 0.18f, r.Value.center.y - r.Value.height * 0.12f) : (Vector2?)null;
        }

        // ------------------------------------------------------------ lifetime
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _instance = null; Modals.Clear(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => Ensure();

        static FtueRadio Ensure()
        {
            if (_instance != null) return _instance;
            var prefab = Resources.Load<GameObject>("UI/RadioOverlay");
            if (prefab == null) { Debug.LogWarning("[FtueRadio] Resources/UI/RadioOverlay is missing."); return null; }
            var go = Instantiate(prefab);
            go.name = "[ZombieWar.FtueRadio]";
            DontDestroyOnLoad(go);
            return go.GetComponent<FtueRadio>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            var json = Resources.Load<TextAsset>("VO/vo_subtitles");
            if (json != null) foreach (var l in JsonUtility.FromJson<Catalog>(json.text).lines) _lines[l.id] = l;
            foreach (var v in new[] { cardNormal, cardSmall, cardItem, cardSub }) if (v != null) Group(v).alpha = 0f;
            _subGroup = cardSub != null ? Group(cardSub) : null;
            _stepGroups = new[] { cardNormal != null ? Group(cardNormal) : null, cardSmall != null ? Group(cardSmall) : null, cardItem != null ? Group(cardItem) : null };
            _stepViews = new[] { cardNormal, cardSmall, cardItem };
            // The second spotlight hole follows its own target rect, cloned from the first at runtime
            // so the prefab stays as built.
            if (spotTarget != null && spotHole != null)
            {
                _spotTarget2 = Instantiate(spotTarget, spotTarget.parent);
                _spotTarget2.name = "SpotTarget2";
            }
            HideMarkers();
            ApplySafeArea();
        }

        void OnDestroy()
        {
            if (_subscribed && Bill.IsReady) Bill.Events.Unsubscribe<RadioLineEvent>(OnLine);
            if (_instance == this) _instance = null;
        }

        static CanvasGroup Group(Component c)
        {
            var g = c.GetComponent<CanvasGroup>();
            if (g == null) g = c.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false; g.interactable = false;
            return g;
        }

        // ------------------------------------------------------------ calls
        void Begin(Call c)
        {
            HideMarkers();
            _call = c;
            _card = c.Size == Size.Small ? cardSmall : c.Size == Size.Item ? cardItem : cardNormal;
            if (_card == null) return;
            _cardGroup = Group(_card);
            Place((RectTransform)_card.transform, c.Y);
            if (c.Size == Size.Item && itemIcon != null) { itemIcon.sprite = c.Icon; itemIcon.enabled = c.Icon != null; }
            var (_, call) = Agents.TryGetValue(c.Agent ?? "", out var a) ? a : ("", "");
            string channel = "● HQ" + (call != "" ? " · " + call : "") + (string.IsNullOrEmpty(c.Context) ? "" : " · " + c.Context);
            // The card and its voice start together (owner 04/10): the body waits for the voice line
            // to begin (OnLine), unless it is already playing or will never come.
            _card.CharsPerSecond = DefaultCps;
            bool playing = c.VoiceId != null && RadioVoice.PlayingId == c.VoiceId;
            _card.Say(channel, c.Title, c.Body, Face(c.Agent), c.VoiceId, waitForVoice: c.VoiceId != null && !playing);
            if (playing) _card.StartTyping(CpsFor(c.Body, RadioVoice.PlayingTimeLeft));
            _waitSince = Time.unscaledTime;
            if (c.ShownAt < 0f) c.ShownAt = Time.unscaledTime;
            _placedY = c.Y;
            _lastY = c.Y; _lastAt = Time.unscaledTime;
            if (_subGroup != null) _subGroup.alpha = 0f;   // the step card replaces any subtitle
            _subUntil = 0f;
            if (chipText != null) chipText.text = c.Chip ?? "";
        }

        static bool Alive(Call c)
        {
            try
            {
                if (c.Alive != null && !c.Alive()) return false;
                if (c.Seconds <= 0f) return true;
                // A timed call (an item tip) that waited too long behind another step is stale.
                if (c.ShownAt < 0f) return Time.unscaledTime < c.QueuedAt + 6f;
                return Time.unscaledTime < c.ShownAt + c.Seconds;
            }
            catch (MissingReferenceException) { return false; }
        }

        void End()
        {
            _call = null;
            HideMarkers();
        }

        void HideMarkers()
        {
            if (dim != null) dim.enabled = false;
            if (spot != null) spot.enabled = false;
            if (ring != null) ring.enabled = false;
            if (reticle != null) reticle.enabled = false;
            if (chip != null) chip.gameObject.SetActive(false);
            if (handRoot != null) handRoot.gameObject.SetActive(false);
        }

        const float DefaultCps = 38f, NoVoiceGrace = 0.35f, VoiceTimeout = 6f;
        float _waitSince;

        /// Typing speed that ends the text a little before the voice does.
        static float CpsFor(string text, float seconds) => Mathf.Max(12f, (text?.Length ?? 0) / Mathf.Max(0.5f, seconds * 0.85f));

        /// A step card waiting for its voice types anyway once the voice is not coming (already said,
        /// no clip) or failed to start in time.
        void ReleaseWaitingCard()
        {
            if (_card == null || !_card.WaitingForVoice || _call == null) return;
            float waited = Time.unscaledTime - _waitSince;
            bool coming = RadioVoice.Pending(_call.VoiceId);
            if ((!coming && waited > NoVoiceGrace) || waited > VoiceTimeout) _card.StartTyping(DefaultCps);
        }

        /// Subtitles for every radio line that is not the current step's own line.
        void OnLine(RadioLineEvent e)
        {
            // The step card's own voice started: its text types along with it.
            if (_call != null && _card != null && _call.VoiceId == e.Id) _card.StartTyping(CpsFor(_call.Body, e.Duration));
            if (cardSub == null || !_lines.TryGetValue(e.Id, out var line)) return;
            // A step card on screen already speaks for its FTUE moment (its line, nudge, "done").
            if (_call != null && (_call.VoiceId == e.Id || e.Id.Contains("_ftue_"))) return;
            string agent = string.IsNullOrEmpty(line.agent) ? e.Agent : line.agent;
            var (name, call) = Agents.TryGetValue(agent, out var a) ? a : (agent.ToUpperInvariant(), "");
            // Under the step card that just ended (done/nudge lines), else under the HUD top strip.
            float y = _call == null && Time.unscaledTime - _lastAt < 30f && _lastY >= 0f ? _lastY : subtitleTop;
            if (_call != null && Mathf.Abs(_call.Y - subtitleTop) < 260f) y = _call.Y + 300f;
            Place((RectTransform)cardSub.transform, y);
            cardSub.CharsPerSecond = CpsFor(line.en, e.Duration);
            cardSub.Say("● HQ · " + call, name, line.en, Face(agent), e.Id);
            _subUntil = Time.unscaledTime + e.Duration + 0.6f;
        }

        Sprite Face(string agent)
        {
            int i = Array.IndexOf(agentIds, agent ?? "");
            return i >= 0 && i < faces.Length ? faces[i] : null;
        }

        // ------------------------------------------------------------ per frame
        void Update()
        {
            if (!_subscribed && Bill.IsReady) { _subscribed = true; Bill.Events.Subscribe<RadioLineEvent>(OnLine); }
            if (Screen.safeArea != _lastSafe) ApplySafeArea();
            float dt = Time.unscaledDeltaTime / Mathf.Max(0.01f, fade);

            _pending.RemoveAll(p => !Alive(p));
            Call top = null;
            foreach (var p in _pending)
                if (top == null || (p.Modal && (!top.Modal || p.Seq > top.Seq)) || (!p.Modal && !top.Modal && p.Seq < top.Seq)) top = p;
            if (top == null) { if (_call != null) End(); }
            else if (top != _call) Begin(top);
            for (int i = 0; i < _stepGroups.Length; i++)
            {
                var g = _stepGroups[i];
                if (g == null) continue;
                float to = _call != null && _stepViews[i] == _card ? 1f : 0f;
                if (g.alpha != to) g.alpha = Mathf.MoveTowards(g.alpha, to, dt);
            }
            if (_subGroup != null)
            {
                float to = Time.unscaledTime < _subUntil && !AnyModalOpen() ? 1f : 0f;
                if (_subGroup.alpha != to) _subGroup.alpha = Mathf.MoveTowards(_subGroup.alpha, to, dt);
            }
            if (_call != null) { ReleaseWaitingCard(); Track(_call); }
        }

        void Track(Call c)
        {
            Rect? target = null;
            try { target = c.Target?.Invoke(); } catch (MissingReferenceException) { }
            Vector2? hand = null;
            try { hand = c.Hand?.Invoke(); } catch (MissingReferenceException) { }

            if (dim != null)
            {
                dim.enabled = c.Dim > 0f && !c.Spotlight;
                if (dim.enabled) dim.color = new Color(0.03f, 0.05f, 0.09f, c.Dim);
            }
            if (spot != null) spot.enabled = c.Spotlight && target.HasValue;
            if (target.HasValue)
            {
                var local = ToLocal(target.Value);
                if (spotTarget != null) { spotTarget.anchoredPosition = local.center; spotTarget.sizeDelta = local.size; }
                Rect? second = null;
                if (c.Spotlight && c.Target2 != null) try { second = c.Target2(); } catch (MissingReferenceException) { }
                if (_spotTarget2 != null && spotHole != null)
                {
                    _spotTarget2.gameObject.SetActive(second.HasValue);
                    if (second.HasValue) { var l2 = ToLocal(second.Value); _spotTarget2.anchoredPosition = l2.center; _spotTarget2.sizeDelta = l2.size; }
                    spotHole.Target2 = second.HasValue ? _spotTarget2 : null;
                }
                if (reticle != null)
                {
                    reticle.enabled = true;
                    var rt = reticle.rectTransform;
                    rt.anchoredPosition = local.center;
                    rt.sizeDelta = local.size + Vector2.one * reticlePad * 2f;
                }
                if (ring != null)
                {
                    ring.enabled = c.Ring;
                    if (c.Ring) { ring.rectTransform.anchoredPosition = local.center; ring.rectTransform.sizeDelta = Vector2.one * Mathf.Min(local.width, local.height); }
                }
                if (chip != null)
                {
                    chip.gameObject.SetActive(!string.IsNullOrEmpty(c.Chip));
                    float chipY = c.ChipAbove ? local.yMax + reticlePad   // straddles the corners' top edge, a tag on the card
                                              : local.yMin - reticlePad - 6f - chip.sizeDelta.y * 0.5f;
                    // Kept on screen: a station at the screen edge pushed its chip half off it (QA 04/10).
                    float halfW = safe != null ? safe.rect.width * 0.5f : 540f, chipHalf = chip.sizeDelta.x * 0.5f;
                    float chipX = Mathf.Clamp(local.xMin - reticlePad + chipHalf, -halfW + chipHalf + 12f, halfW - chipHalf - 12f);
                    chip.anchoredPosition = new Vector2(chipX, chipY);
                }
            }
            else
            {
                if (reticle != null) reticle.enabled = false;
                if (ring != null) ring.enabled = false;
                if (chip != null) chip.gameObject.SetActive(false);
            }
            if (handRoot != null)
            {
                handRoot.gameObject.SetActive(hand.HasValue);
                if (hand.HasValue && RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, hand.Value, null, out var p)) handRoot.anchoredPosition = p;
            }
            KeepCardClear(c, target, hand);
        }

        const float ClearPad = 24f, HandReach = 70f;

        /// The board height is a starting point, not a promise: when the card would cover what the
        /// step points at (the Arsenal link under the result gift card, QA 04/10) it moves above the
        /// target, or below it when there is no room above.
        void KeepCardClear(Call c, Rect? target, Vector2? hand)
        {
            if (_card == null || (!target.HasValue && !hand.HasValue)) return;
            var card = (RectTransform)_card.transform;
            var parent = card.parent as RectTransform;
            if (parent == null) return;

            Rect keep = default; bool any = false;
            if (target.HasValue) { keep = ToLocalIn(parent, target.Value); any = true; }
            if (hand.HasValue && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, hand.Value, null, out var hp))
            {
                var h = new Rect(hp.x - HandReach, hp.y - HandReach, HandReach * 2f, HandReach * 2f);
                keep = any ? Rect.MinMaxRect(Mathf.Min(keep.xMin, h.xMin), Mathf.Min(keep.yMin, h.yMin), Mathf.Max(keep.xMax, h.xMax), Mathf.Max(keep.yMax, h.yMax)) : h;
                any = true;
            }
            if (!any) return;

            float top = parent.rect.yMax, height = card.rect.height;
            float y = c.Y;
            float cardTop = top - y, cardBottom = cardTop - height;
            bool covers = cardBottom < keep.yMax + ClearPad && cardTop > keep.yMin - ClearPad;
            if (covers)
            {
                float above = top - (keep.yMax + ClearPad + height);   // card bottom just over the target
                float below = top - (keep.yMin - ClearPad);            // card top just under it
                y = above >= ClearPad ? above : below;
            }
            if (Mathf.Abs(y - _placedY) < 1f) return;
            _placedY = y;
            Place(card, y);
        }

        static Rect ToLocalIn(RectTransform space, Rect screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen.min, null, out var a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen.max, null, out var b);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// Screen-pixel rect → rect in the safe area's local space (centre-anchored children).
        Rect ToLocal(Rect screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen.min, null, out var a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen.max, null, out var b);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// Cards are anchored top-centre; y is the card's top edge below the safe area top.
        static void Place(RectTransform card, float y)
        {
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = new Vector2(0f, -y);
        }

        void ApplySafeArea()
        {
            _lastSafe = Screen.safeArea;
            if (safe == null || Screen.width <= 0 || Screen.height <= 0) return;
            var r = Screen.safeArea;
            safe.anchorMin = new Vector2(r.xMin / Screen.width, r.yMin / Screen.height);
            safe.anchorMax = new Vector2(r.xMax / Screen.width, r.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }
    }
}
