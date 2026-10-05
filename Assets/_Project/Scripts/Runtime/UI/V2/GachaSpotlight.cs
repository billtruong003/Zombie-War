using System;
using System.Collections;
using BillGameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Gacha chest show (owner 2026-09-28: "the gacha animation is too simple, add VFX").
    /// One chest in the spotlight: it drops with a dust puff, charges up (rarity beam, pulsing glow,
    /// rising sparks, rays for Epic and up, shaking harder and harder), bursts (white flash,
    /// shock rings, star burst, confetti for Epic+, a second gold wave for Legendary, screen shake)
    /// and flips into the prize card. <see cref="MiniBurst"/> is the small pop for the x10 grid.
    /// All in unscaled time with UI particles (overlay canvas).
    /// </summary>
    public sealed class GachaSpotlight : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform shakeTarget;
        [SerializeField] private Image rays;
        [SerializeField] private Image beam;
        [SerializeField] private Image glow;
        [SerializeField] private Image chest;
        [SerializeField] private RectTransform card;
        [SerializeField] private Image cardBg;
        [SerializeField] private Image cardIcon;
        [SerializeField] private TMP_Text cardName;
        [SerializeField] private TMP_Text cardNote;
        [SerializeField] private TMP_Text cardTier;
        [SerializeField] private Image[] rings = new Image[2];
        [SerializeField] private Image flash;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private Button tapArea;
        [SerializeField] private UIParticleBurst particles;
        [SerializeField] private UIParticleBurst gridParticles;
        [SerializeField] private Sprite spark, star, confetti, dust;

        [Header("Shader stage (backlog #12, mockup U5 05/10)")]
        [Tooltip("Full-screen graphic with the GachaStage material: rays, glow, dust and the burst " +
                 "wave are drawn by the shader. When set, the old sprite rays / beam / glow stay off.")]
        [SerializeField] private Graphic stageFx;
        [SerializeField] private Image tierPill;
        [SerializeField] private Image[] stars = new Image[5];
        [SerializeField] private GameObject newTag;
        [SerializeField] private Image namePanel;
        [Tooltip("WEAR NOW / LATER under an outfit prize (backlog #13): the gift's last step is wearing it.")]
        [SerializeField] private Button wearNow;
        [SerializeField] private Button later;

        /// <summary>True when the last show ended on WEAR NOW.</summary>
        public bool WearChosen { get; private set; }

        public static readonly Color[] TierGlow =
        {
            new(0.85f, 0.9f, 1f), new(0.45f, 1f, 0.6f), new(0.4f, 0.75f, 1f), new(0.8f, 0.5f, 1f), new(1f, 0.78f, 0.25f),
        };
        static readonly string[] TierName = { "COMMON", "UNCOMMON", "RARE", "EPIC", "LEGENDARY" };
        static readonly Color[] Confetti = { new(1f, 0.35f, 0.4f), new(1f, 0.82f, 0.25f), new(0.3f, 0.85f, 1f), new(0.7f, 0.45f, 1f), new(0.4f, 1f, 0.55f) };

        bool _tapped;
        Vector2 _shakeRest;
        Material _stage;
        Vector2? _nameRest, _noteRest;
        float _spin, _stageT;
        static readonly int TierColorId = Shader.PropertyToID("_TierColor"), ChargeId = Shader.PropertyToID("_Charge"),
            RaysId = Shader.PropertyToID("_Rays"), SpinId = Shader.PropertyToID("_Spin"), GlowId = Shader.PropertyToID("_Glow"),
            BurstId = Shader.PropertyToID("_Burst"), TId = Shader.PropertyToID("_T"), AspectId = Shader.PropertyToID("_Aspect");

        /// <summary>Stage values while the show runs; the shader does the drawing.</summary>
        float _charge, _rays, _glow, _burst = 1f;

        void LateUpdate()
        {
            if (_stage == null || root == null || !root.activeSelf) return;
            float dt = Dt();
            _stageT += dt;
            _spin += dt * (0.12f + 0.5f * _charge);
            var rt = stageFx.rectTransform.rect;
            _stage.SetFloat(AspectId, rt.height > 1f ? rt.width / rt.height : 0.5625f);
            _stage.SetFloat(ChargeId, _charge); _stage.SetFloat(RaysId, _rays); _stage.SetFloat(GlowId, _glow);
            _stage.SetFloat(BurstId, _burst); _stage.SetFloat(SpinId, _spin); _stage.SetFloat(TId, _stageT);
        }

        void Awake()
        {
            if (tapArea != null) tapArea.onClick.AddListener(() => _tapped = true);
            if (wearNow != null) wearNow.onClick.AddListener(() => { WearChosen = true; _tapped = true; UIFeedback.Confirm(); });
            if (later != null) later.onClick.AddListener(() => { _tapped = true; UIFeedback.Back(); });
            if (root != null) root.SetActive(false);
            FtueRadio.RegisterModal(root);   // radio subtitles wait while a chest opens (QA 05/10)
            if (stageFx != null && stageFx.material != null)
            {
                _stage = new Material(stageFx.material) { name = stageFx.material.name + " (show)" };
                stageFx.material = _stage;
            }
        }

        public bool Showing => root != null && root.activeSelf;

        /// <summary>The whole show for one chest. <paramref name="skip"/> jumps to the card.</summary>
        public IEnumerator Play(int tier, Sprite chestSprite, Sprite prize, string prizeName, string note, Func<bool> skip, bool offerWear = false)
        {
            WearChosen = false;
            ShowWear(false);
            tier = Mathf.Clamp(tier, 0, 4);
            var col = TierGlow[tier];
            // A tap during the build-up hurries to the card; the card then waits for its own tap.
            var outer = skip;
            skip = () => outer() || _tapped;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            _tapped = false;
            if (shakeTarget != null) _shakeRest = shakeTarget.anchoredPosition;
            Alpha(rays, 0); Alpha(beam, 0); Alpha(glow, 0); Alpha(flash, 0);
            foreach (var r in rings) Alpha(r, 0);
            card.gameObject.SetActive(false);
            if (hint != null) hint.gameObject.SetActive(false);
            Tint(rays, col); Tint(beam, col); Tint(glow, col);
            bool shader = _stage != null;
            if (shader)
            {
                _stage.SetColor(TierColorId, col);
                _charge = 0f; _rays = 0.12f; _glow = 0.25f; _burst = 1f;
            }
            ShowCardChrome(false, tier, note);
            chest.sprite = chestSprite; chest.gameObject.SetActive(true);
            var ct = chest.rectTransform;
            ct.localRotation = Quaternion.identity; ct.localScale = Vector3.one;

            // ---- drop
            for (float t = 0f; t < 0.5f && !skip(); t += Dt())
            {
                ct.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(900f, 0f, OutBounce(t / 0.5f)));
                yield return null;
            }
            ct.anchoredPosition = Vector2.zero;
            if (!skip())
            {
                Emit(particles, new Vector2(0, -110), dust, 10, new Color(0.9f, 0.85f, 0.8f, 0.8f), new Color(0.7f, 0.65f, 0.6f, 0.6f), new Vector2(160, 320), new Vector2(60, 110), new Vector2(0.5f, 0.9f), -60f, 3f, 60f, 70f, 0f, 1.8f, new Vector2(60, 10));
                Emit(particles, new Vector2(0, -110), dust, 10, new Color(0.9f, 0.85f, 0.8f, 0.8f), new Color(0.7f, 0.65f, 0.6f, 0.6f), new Vector2(160, 320), new Vector2(60, 110), new Vector2(0.5f, 0.9f), -60f, 3f, 60f, 70f, 180f, 1.8f, new Vector2(60, 10));
                Sfx("sfx.ui.card"); UIFeedback.Haptic(UIFeedback.Buzz.Light);
                yield return Shake(0.15f, 8f, skip);
            }

            // ---- charge
            float charge = 0.8f + tier * 0.3f;
            float sparkTimer = 0f;
            for (float t = 0f; t < charge && !skip(); t += Dt())
            {
                float k = t / charge;
                if (shader)
                {
                    _charge = k;
                    _rays = Mathf.Lerp(0.1f, 0.45f + tier * 0.1f, k);
                    _glow = Mathf.Lerp(0.2f, 0.8f + tier * 0.1f, k);
                }
                else
                {
                    Alpha(beam, Mathf.SmoothStep(0f, 0.55f + tier * 0.1f, k));
                    Alpha(glow, (0.25f + 0.2f * tier) * k * (0.8f + 0.2f * Mathf.Sin(t * 18f)));
                    if (tier >= 3) { Alpha(rays, 0.5f * k); rays.rectTransform.localRotation = Quaternion.Euler(0, 0, -t * 40f); }
                }
                ct.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * (20f + 30f * k)) * (3f + 9f * k * (1 + tier * 0.4f)));
                ct.localScale = Vector3.one * (1f + 0.15f * k);
                if ((sparkTimer -= Dt()) <= 0f)
                {
                    sparkTimer = Mathf.Lerp(0.12f, 0.03f, k);
                    Emit(particles, new Vector2(0, -40), spark, 2, col, Color.white, new Vector2(160, 360), new Vector2(14, 30), new Vector2(0.5f, 0.9f), -120f, 0.5f, 0f, 40f, 90f, 0.3f, new Vector2(90, 20));
                }
                yield return null;
            }

            // ---- burst
            if (!skip())
            {
                chest.gameObject.SetActive(false);
                StartCoroutine(Flash(0.35f + tier * 0.05f));
                if (shader) StartCoroutine(StageBurst(tier));
                else
                {
                    StartCoroutine(Ring(rings[0], 0f, 2.6f + tier * 0.3f));
                    if (rings.Length > 1) StartCoroutine(Ring(rings[1], 0.1f, 3.4f + tier * 0.4f));
                }
                int stars = 16 + tier * 10;
                Emit(particles, Vector2.zero, star, stars, col, Color.white, new Vector2(420, 1150), new Vector2(40, 90), new Vector2(0.6f, 1.2f), 700f, 1.6f, 400f, 360f, 90f, 0.4f, Vector2.zero);
                Emit(particles, Vector2.zero, spark, 24, col, Color.white, new Vector2(300, 900), new Vector2(20, 40), new Vector2(0.4f, 0.9f), 300f, 2f, 0f, 360f, 90f, 0.2f, Vector2.zero);
                if (tier >= 3) ConfettiBurst(60 + (tier - 3) * 40);
                if (tier >= 4) StartCoroutine(SecondWave());
                if (tier >= 4) { Sfx("sfx.player.bomb.explode"); Sfx("stinger.victory"); UIFeedback.Haptic(UIFeedback.Buzz.Medium); }
                else if (tier == 3) { UIFeedback.LevelUp(); }
                else { Sfx("sfx.pickup.gem"); UIFeedback.Haptic(UIFeedback.Buzz.Light); }
                StartCoroutine(Shake(0.3f + tier * 0.05f, 10f + tier * 6f, skip));
                if (!shader) Alpha(beam, 0.35f);
            }
            else chest.gameObject.SetActive(false);
            if (shader) { _charge = 0f; _rays = 0.4f + tier * 0.1f; _glow = 0.6f + tier * 0.1f; }

            // ---- the card flips in
            card.gameObject.SetActive(true);
            if (cardBg != null)
            {
                // On the shader stage the prize floats in the light (mockup U5): no box behind it.
                cardBg.enabled = !shader;
                if (!shader) { cardBg.color = TierGlow[tier] * 0.55f + new Color(0.15f, 0.15f, 0.2f, 1f); ItemTileFx.On(cardBg, tier); }
            }
            ShowCardChrome(shader, tier, note);
            if (cardIcon != null) { cardIcon.sprite = prize; cardIcon.enabled = prize != null; }
            if (cardName != null) cardName.text = prizeName;
            if (cardNote != null) cardNote.text = shader ? SubLine(note) : note;
            if (cardTier != null) { cardTier.text = TierName[tier]; cardTier.color = shader ? Color.white : TierGlow[tier]; }
            for (float t = 0f; t < 0.4f && !skip(); t += Dt())
            {
                float k = OutBack(t / 0.4f);
                card.localScale = new Vector3(Mathf.Max(0.01f, k), Mathf.Lerp(0.6f, 1f, t / 0.4f), 1f);
                yield return null;
            }
            card.localScale = Vector3.one;

            // ---- wait for a tap (a trickle of sparkles for Epic and up)
            _tapped = false;
            skip = outer;
            if (hint != null) { hint.gameObject.SetActive(!offerWear); hint.text = "TAP TO CONTINUE"; }
            ShowWear(offerWear);
            // With the two buttons up, only they end the show: a stray tap must not skip WEAR NOW.
            if (tapArea != null) tapArea.interactable = !offerWear;
            float rain = 0f;
            while (!_tapped && !skip())
            {
                if (tier >= 3 && (rain -= Dt()) <= 0f)
                {
                    rain = 0.12f;
                    Emit(particles, new Vector2(0, 700), star, 1, col, Color.white, new Vector2(60, 160), new Vector2(18, 36), new Vector2(1.2f, 2f), 120f, 0.3f, 90f, 30f, -90f, 0.3f, new Vector2(420, 0));
                }
                if (tier >= 3 && !shader) rays.rectTransform.localRotation *= Quaternion.Euler(0, 0, -20f * Dt());
                yield return null;
            }
            ShowWear(false);
            if (tapArea != null) tapArea.interactable = true;
            for (float t = 0f; t < 0.18f; t += Dt()) { SetGroupAlpha(1f - t / 0.18f); yield return null; }
            SetGroupAlpha(1f);
            particles.Clear();
            root.SetActive(false);
        }

        void ShowWear(bool on)
        {
            if (wearNow != null) { wearNow.gameObject.SetActive(on); if (on) UIFx.PopIn(wearNow.transform); }
            if (later != null) later.gameObject.SetActive(on);
        }

        // The shock wave and flash of the burst, drawn by the stage shader.
        IEnumerator StageBurst(int tier)
        {
            float seconds = 0.6f + tier * 0.08f;
            for (float t = 0f; t < seconds; t += Dt()) { _burst = t / seconds; yield return null; }
            _burst = 1f;
        }

        /// <summary>Mockup U5 chrome on the stage: rarity pill, stars (one per rarity step), the NEW
        /// tag and the dark name panel. Hidden on the old sprite stage.</summary>
        void ShowCardChrome(bool on, int tier, string note)
        {
            if (tierPill != null)
            {
                tierPill.gameObject.SetActive(on);
                var c = TierGlow[tier] * 0.55f; c.a = 1f; tierPill.color = c;   // dark enough for white text
            }
            if (stars != null)
                for (int i = 0; i < stars.Length; i++)
                {
                    if (stars[i] == null) continue;
                    stars[i].gameObject.SetActive(on && i <= tier);
                    // The shown stars are centred: one star for Common sits in the middle.
                    var sr = stars[i].rectTransform;
                    sr.anchoredPosition = new Vector2((i - tier * 0.5f) * 54f, sr.anchoredPosition.y);
                }
            if (newTag != null) newTag.SetActive(on && IsNew(note));
            if (namePanel != null) namePanel.gameObject.SetActive(on);
            // Inside the panel: one line sits in its middle; two lines stack name over note.
            if (cardName != null && namePanel != null)
            {
                if (_nameRest == null) _nameRest = cardName.rectTransform.anchoredPosition;
                if (_noteRest == null && cardNote != null) _noteRest = cardNote.rectTransform.anchoredPosition;
                if (!on)
                {
                    cardName.rectTransform.anchoredPosition = _nameRest.Value;
                    if (cardNote != null) cardNote.rectTransform.anchoredPosition = _noteRest.Value;
                }
                else
                {
                    var pr = namePanel.rectTransform;
                    float bottom = pr.anchoredPosition.y, h = pr.rect.height;
                    bool single = string.IsNullOrEmpty(SubLine(note));
                    CentreAt(cardName.rectTransform, bottom + h * (single ? 0.5f : 0.62f));
                    if (cardNote != null) CentreAt(cardNote.rectTransform, bottom + h * 0.27f);
                }
            }
        }

        static void CentreAt(RectTransform t, float y) =>
            t.anchoredPosition = new Vector2(t.anchoredPosition.x, y - t.rect.height * (0.5f - t.pivot.y));

        public static bool IsNew(string note) => !string.IsNullOrEmpty(note) && (note == "NEW" || note.EndsWith(" · NEW"));

        /// <summary>The panel's second line: the note without the NEW the tag already says.</summary>
        public static string SubLine(string note)
        {
            if (string.IsNullOrEmpty(note) || note == "NEW") return string.Empty;
            return note.EndsWith(" · NEW") ? note.Substring(0, note.Length - 6) : note;
        }

        /// <summary>Small pop on a grid tile (x10 reveal): a ring and a few sparks in its colour.</summary>
        public void MiniBurst(RectTransform tile, int tier)
        {
            if (gridParticles == null || tile == null) return;
            var layer = (RectTransform)gridParticles.transform;
            Vector2 local = layer.InverseTransformPoint(tile.TransformPoint(tile.rect.center));
            var col = TierGlow[Mathf.Clamp(tier, 0, 4)];
            Emit(gridParticles, local, spark, 8 + tier * 4, col, Color.white, new Vector2(120, 420), new Vector2(10, 22), new Vector2(0.35f, 0.7f), 200f, 2f, 0f, 360f, 90f, 0.2f, Vector2.zero);
            if (tier >= 3) Emit(gridParticles, local, star, 6 + (tier - 3) * 6, col, Color.white, new Vector2(200, 520), new Vector2(26, 50), new Vector2(0.5f, 0.9f), 260f, 1.8f, 300f, 360f, 90f, 0.3f, Vector2.zero);
        }

        IEnumerator SecondWave()
        {
            for (float t = 0f; t < 0.28f; t += Dt()) yield return null;
            var gold = TierGlow[4];
            Emit(particles, Vector2.zero, star, 40, gold, Color.white, new Vector2(600, 1500), new Vector2(50, 110), new Vector2(0.8f, 1.4f), 500f, 1.2f, 500f, 360f, 90f, 0.3f, Vector2.zero);
            StartCoroutine(Ring(rings[0], 0f, 4.5f));
            StartCoroutine(Flash(0.25f));
        }

        void ConfettiBurst(int n)
        {
            for (int i = 0; i < Confetti.Length; i++)
                Emit(particles, new Vector2(0, -20), confetti, n / Confetti.Length, Confetti[i], Confetti[(i + 2) % Confetti.Length],
                     new Vector2(700, 1500), new Vector2(18, 30), new Vector2(1.4f, 2.4f), 1400f, 1.2f, 720f, 110f, 90f, 1f, Vector2.zero);
        }

        IEnumerator Ring(Image ring, float delay, float scale)
        {
            if (ring == null) yield break;
            for (float t = 0f; t < delay; t += Dt()) yield return null;
            for (float t = 0f; t < 0.5f; t += Dt())
            {
                float k = t / 0.5f;
                ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.2f, scale, 1f - (1f - k) * (1f - k));
                Alpha(ring, 0.9f * (1f - k));
                yield return null;
            }
            Alpha(ring, 0);
        }

        IEnumerator Flash(float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt()) { Alpha(flash, 0.7f * (1f - t / seconds)); yield return null; }
            Alpha(flash, 0);
        }

        IEnumerator Shake(float seconds, float px, Func<bool> skip)
        {
            if (shakeTarget == null) yield break;
            for (float t = 0f; t < seconds && !skip(); t += Dt())
            {
                float k = 1f - t / seconds;
                shakeTarget.anchoredPosition = _shakeRest + new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * px * k;
                yield return null;
            }
            shakeTarget.anchoredPosition = _shakeRest;
        }

        void SetGroupAlpha(float a)
        {
            if (!root.TryGetComponent(out CanvasGroup g)) g = root.AddComponent<CanvasGroup>();
            g.alpha = a;
        }

        static void Emit(UIParticleBurst p, Vector2 at, Sprite s, int count, Color a, Color b, Vector2 speed, Vector2 size, Vector2 life,
                         float gravity, float drag, float spin, float spread, float dir, float endScale, Vector2 area)
        {
            if (p == null || s == null || count <= 0) return;
            p.Emit(at, new UIParticleBurst.Burst
            {
                sprite = s, count = count, colorA = a, colorB = b, speed = speed, size = size, life = life, gravity = gravity,
                drag = drag, spin = spin, spread = spread, direction = dir, endScale = endScale, area = area,
            });
        }

        static void Sfx(string key) { if (Bill.IsReady) Bill.Audio?.Play(key); }
        /// <summary>Editor review only: a fixed step per frame so captures can walk the show.</summary>
        public static float DebugStep;
        static float Dt() => DebugStep > 0f ? DebugStep : Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        static void Alpha(Graphic g, float a) { if (g == null) return; var c = g.color; c.a = a; g.color = c; }
        static void Tint(Graphic g, Color c) { if (g == null) return; c.a = g.color.a; g.color = c; }

        static float OutBounce(float t)
        {
            const float n = 7.5625f, d = 2.75f;
            if (t < 1f / d) return n * t * t;
            if (t < 2f / d) return n * (t -= 1.5f / d) * t + 0.75f;
            if (t < 2.5f / d) return n * (t -= 2.25f / d) * t + 0.9375f;
            return n * (t -= 2.625f / d) * t + 0.984375f;
        }

        static float OutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }
    }
}
