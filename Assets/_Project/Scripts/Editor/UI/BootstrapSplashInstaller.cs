using BillGameCore;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// Dựng splash trên Bootstrap scene: SplashCanvas (phủ trên) + BillStartup.
    /// Logo = PLACEHOLDER (t thay sau). Idempotent: đập "SplashCanvas" cũ, dựng lại.
    /// Flow: BootstrapEntry drive EnterMenu; splash chỉ là visual → fade out lộ menu.
    /// </summary>
    public static class BootstrapSplashInstaller
    {
        const string CanvasName = "SplashCanvas";

        [MenuItem("ZombieWar/UI/Build Bootstrap Splash")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Bootstrap"))
            {
                Debug.LogError("[Splash] Mở Bootstrap scene trước đã.");
                return;
            }

            // đập cũ
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == CanvasName) Object.DestroyImmediate(go);

            // ---- Canvas phủ trên ----
            var canvasGo = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UITheme.RefWidth, UITheme.RefHeight);
            scaler.matchWidthOrHeight = 0.5f;
            var group = canvasGo.GetComponent<CanvasGroup>();
            var root = (RectTransform)canvasGo.transform;

            // ---- BG ----
            var bg = UIKit.Image(root, "Bg", null, UITheme.Bg);
            UIKit.Full(bg.rectTransform);

            // ---- Logo PLACEHOLDER (khối bo góc + chữ) ----
            var logoBox = UIKit.Image(root, "Logo", UIKit.Rounded, UITheme.Surface);
            UIKit.Place(logoBox.rectTransform, UIKit.Anch.C, new Vector2(0, 120), new Vector2(620, 360));
            var logoEdge = logoBox.gameObject.AddComponent<Outline>();
            logoEdge.effectColor = UITheme.Gold; logoEdge.effectDistance = new Vector2(3, -3);

            var title = UIKit.Text(logoBox.rectTransform, "Title", "ZOMBIE WAR", 84,
                UITheme.Gold, FontStyles.Bold | FontStyles.UpperCase);
            UIKit.Place(title.rectTransform, UIKit.Anch.C, new Vector2(0, 30), new Vector2(560, 120));
            var sub = UIKit.Text(logoBox.rectTransform, "Sub", "LOGO PLACEHOLDER", UITheme.FontLabel,
                UITheme.TextDim, FontStyles.Bold);
            UIKit.Place(sub.rectTransform, UIKit.Anch.C, new Vector2(0, -70), new Vector2(560, 44));

            // ---- Status text ----
            var status = UIKit.Text(root, "Status", "Đang tải...", UITheme.FontBody,
                UITheme.TextDim, FontStyles.Normal);
            UIKit.Place(status.rectTransform, UIKit.Anch.C, new Vector2(0, -260), new Vector2(700, 50));

            // ---- Progress slider (skeleton bar) ----
            var slider = BuildSlider(root);

            // ---- BillStartup wiring ----
            var startup = canvasGo.AddComponent<BillStartup>();
            var so = new SerializedObject(startup);
            UIKit.Wire(so, "logo", logoBox);
            UIKit.Wire(so, "progressSlider", slider);
            UIKit.Wire(so, "statusText", status);
            UIKit.Wire(so, "rootCanvasGroup", group);
            var nx = so.FindProperty("nextScene"); if (nx != null) nx.stringValue = ""; // BootstrapEntry lo menu
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Splash] Bootstrap splash dựng xong + lưu scene.");
        }

        static Slider BuildSlider(RectTransform parent)
        {
            var sRt = UIKit.Rect("Progress", parent);
            UIKit.Place(sRt, UIKit.Anch.C, new Vector2(0, -340), new Vector2(560, 14));
            var slider = sRt.gameObject.AddComponent<Slider>();

            var track = UIKit.Image(sRt, "Track", UIKit.Rounded, UITheme.Surface2, false);
            UIKit.Full(track.rectTransform);

            var fillArea = UIKit.Rect("FillArea", sRt);
            UIKit.Full(fillArea);
            var fill = UIKit.Image(fillArea, "Fill", UIKit.Rounded, UITheme.Gold, false);
            fill.rectTransform.anchorMin = new Vector2(0, 0);
            fill.rectTransform.anchorMax = new Vector2(1, 1);
            fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;

            slider.fillRect = fill.rectTransform;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 1; slider.value = 0;
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            return slider;
        }
    }
}
