using BillGameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.Threat;

namespace ZombieWar.UI
{
    /// <summary>
    /// Horde Call telegraph (backlog #10, mockup U2 05/10). Ten seconds before a surge: Raptor's radio
    /// card names the side, red arrows pulse on that edge of the screen, the edge glows red, and the
    /// last three seconds count down big in the middle. Surviving the whole surge shows
    /// HORDE CLEARED (the director drops the chest).
    ///
    /// Authored in UI_Hud (HordeCall: Tint, Arrows/Arrow0-2, Count). Reads the director; owns no rules.
    /// </summary>
    public sealed class HordeCallView : MonoBehaviour
    {
        [SerializeField] private float edgeInset = 90f;
        [SerializeField] private float topInset = 470f;      // under the skill grid
        [SerializeField] private float bottomInset = 560f;   // above the joystick
        [SerializeField] private float arrowSpacing = 150f;
        [SerializeField] private float sideDrop = 180f;       // left/right arrows sit under the radio card

        RectTransform _arrows;
        RectTransform[] _arrow = new RectTransform[3];
        Image[] _arrowImg = new Image[3];
        Image _tint;
        TMP_Text _count;
        int _shownCount = -2, _calledFor = -1;
        float _clearedUntil, _tintAlpha;

        static readonly Color Red = new(1f, 0.23f, 0.2f, 1f);

        void Awake()
        {
            _tint = transform.Find("Tint")?.GetComponent<Image>();
            _arrows = transform.Find("Arrows") as RectTransform;
            for (int i = 0; i < 3; i++)
            {
                _arrow[i] = _arrows != null ? _arrows.Find("Arrow" + i) as RectTransform : null;
                _arrowImg[i] = _arrow[i] != null ? _arrow[i].GetComponent<Image>() : null;
            }
            _count = transform.Find("Count")?.GetComponent<TMP_Text>();
            Hide();
        }

        void OnEnable() => Bill.Events?.Subscribe<HordeClearedEvent>(OnCleared);
        void OnDisable() => Bill.Events?.Unsubscribe<HordeClearedEvent>(OnCleared);

        void OnCleared(HordeClearedEvent e)
        {
            _clearedUntil = Time.unscaledTime + 2.5f;
            if (_count == null) return;
            _count.gameObject.SetActive(true);
            _count.fontSize = 110f;
            _count.text = "HORDE CLEARED!\n<size=60%>+ CHEST</size>";
            _shownCount = -2;
            UIFx.Punch(_count.transform);
        }

        void Hide()
        {
            if (_arrows != null) _arrows.gameObject.SetActive(false);
            if (_tint != null) _tint.enabled = false;
            if (_count != null) _count.gameObject.SetActive(false);
        }

        void Update()
        {
            var d = ThreatDirector.Instance;
            var run = RunState.Current;
            bool warn = d != null && run != null && !run.IsOver && d.HordeWarning && d.HordeSide >= 0;
            bool surge = d != null && d.Surging && d.HordeSide >= 0;
            float now = Time.unscaledTime;

            // Radio card once per Horde Call, at the start of the warning.
            if (warn && _calledFor != d.HordeIndex && d.HordeSecondsUntil > 3.5f)
            {
                _calledFor = d.HordeIndex;
                FtueV3.HordeCall(ThreatDirector.SideName(d.HordeSide), d.HordeSecondsUntil - 3f);
            }

            // Arrows: on the side's edge, pointing outward at where the wall comes from.
            bool arrows = warn || surge;
            if (_arrows != null)
            {
                if (_arrows.gameObject.activeSelf != arrows) _arrows.gameObject.SetActive(arrows);
                if (arrows) PlaceArrows(d.HordeSide, now);
            }

            // Red edge: rises through the warning, holds during the surge.
            float tintTo = surge ? 0.55f : warn ? Mathf.Lerp(0.2f, 0.55f, 1f - d.HordeSecondsUntil / ThreatDirector.HordeWarnSeconds) : 0f;
            _tintAlpha = Mathf.MoveTowards(_tintAlpha, tintTo, Time.unscaledDeltaTime * 1.5f);
            if (_tint != null)
            {
                float a = _tintAlpha * (0.8f + 0.2f * Mathf.Sin(now * 6f));
                _tint.enabled = a > 0.01f;
                var c = Red; c.a = a; _tint.color = c;
            }

            // Big 3-2-1 for the last three seconds; HORDE CLEARED owns the label while it shows.
            if (_count == null || now < _clearedUntil) return;
            int n = warn && d.HordeSecondsUntil <= 3f ? Mathf.CeilToInt(d.HordeSecondsUntil) : -1;
            if (n == _shownCount) return;
            _shownCount = n;
            _count.gameObject.SetActive(n > 0);
            if (n <= 0) return;
            _count.fontSize = 300f;
            _count.text = n.ToString();
            UIFx.PopIn(_count.transform, 0f, 1.6f, 0.25f);
            if (Bill.IsReady) Bill.Audio?.PlayPitched("sfx.ui.tap", 0.8f + 0.1f * (3 - n), 0.9f);
        }

        void PlaceArrows(int side, float now)
        {
            var area = (RectTransform)_arrows.parent;
            Vector2 half = area.rect.size * 0.5f;
            Vector2 centre, along; float rot;
            switch (side)
            {
                case 0: centre = new Vector2(0f, half.y - topInset); along = Vector2.right; rot = 0f; break;
                case 1: centre = new Vector2(half.x - edgeInset, -sideDrop); along = Vector2.up; rot = -90f; break;
                case 2: centre = new Vector2(0f, -half.y + bottomInset); along = Vector2.right; rot = 180f; break;
                default: centre = new Vector2(-half.x + edgeInset, -sideDrop); along = Vector2.up; rot = 90f; break;
            }
            Vector2 outward = side switch { 0 => Vector2.up, 1 => Vector2.right, 2 => Vector2.down, _ => Vector2.left };
            for (int i = 0; i < 3; i++)
            {
                if (_arrow[i] == null) continue;
                // A ripple across the three: each bobs outward a little after its neighbour.
                float bob = Mathf.Sin(now * 7f - i * 0.9f) * 12f;
                _arrow[i].anchoredPosition = centre + along * ((i - 1) * arrowSpacing) + outward * bob;
                _arrow[i].localEulerAngles = new Vector3(0f, 0f, rot);
                if (_arrowImg[i] != null) { var c = Red; c.a = 0.75f + 0.25f * Mathf.Sin(now * 7f - i * 0.9f); _arrowImg[i].color = c; }
            }
        }
    }
}
