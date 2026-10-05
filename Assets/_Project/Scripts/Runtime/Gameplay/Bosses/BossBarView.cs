using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.Bosses
{
    /// <summary>
    /// Boss health bar (genre rule 8) — LAB PROTOTYPE, built in code so the HUD prefab (owner-owned)
    /// is untouched until the bar's place in the approved HUD is decided. Name, a red fill that drains
    /// with a trailing white chunk, and ENRAGED when the Titan enrages.
    /// </summary>
    public sealed class BossBarView : MonoBehaviour
    {
        TitanBoss _boss;
        RectTransform _fill, _trail;
        TMP_Text _label;
        float _trailK = 1f;

        public static BossBarView Show(TitanBoss boss, string name)
        {
            var go = new GameObject("BossBar(Lab)", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var v = go.AddComponent<BossBarView>();
            v._boss = boss;
            var root = Rect("Bar", go.transform, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(900f, 46f), new Color(0.08f, 0.06f, 0.1f, 0.85f));
            v._trail = Rect("Trail", root, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(888f, 34f), new Color(1f, 1f, 1f, 0.85f), new Vector2(0f, 0.5f));
            v._fill = Rect("Fill", root, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(888f, 34f), new Color(0.92f, 0.16f, 0.12f), new Vector2(0f, 0.5f));
            var lab = new GameObject("Name", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            lab.transform.SetParent(root, false);
            var lr = (RectTransform)lab.transform; lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 1f); lr.pivot = new Vector2(0.5f, 0f);
            lr.anchoredPosition = new Vector2(0f, 6f); lr.sizeDelta = new Vector2(900f, 56f);
            lab.alignment = TextAlignmentOptions.Bottom; lab.fontSize = 44; lab.fontStyle = FontStyles.Bold; lab.color = Color.white;
            lab.outlineWidth = 0.25f; lab.outlineColor = Color.black;
            v._label = lab; lab.text = name;
            return v;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color c, Vector2? pivot = null)
        {
            var rt = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot ?? new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            rt.GetComponent<Image>().color = c;
            return rt;
        }

        void Update()
        {
            if (_boss == null) { Destroy(gameObject); return; }
            float k = Mathf.Clamp01(_boss.Health / Mathf.Max(1f, _boss.Max));
            _trailK = Mathf.MoveTowards(_trailK, k, Time.unscaledDeltaTime * 0.35f);
            _fill.localScale = new Vector3(k, 1f, 1f);
            _trail.localScale = new Vector3(Mathf.Max(k, _trailK), 1f, 1f);
            if (_boss.Enraged && !_label.text.EndsWith("ENRAGED")) _label.text += "  ·  ENRAGED";
        }
    }
}
