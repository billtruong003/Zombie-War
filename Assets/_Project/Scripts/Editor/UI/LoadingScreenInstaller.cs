using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar.EditorTools
{
    /// M8 P3: turns the Bootstrap splash into the app's loading screen and gives it the M8 look.
    /// Touches Bootstrap.unity only (SplashCanvas). Idempotent.
    /// - LoadingScreen component wired to the splash's group, bar and status text.
    /// - The "LOGO PLACEHOLDER" caption is hidden; the title is set in the display font until the
    ///   owner's logo art replaces it.
    /// - The bar follows the slice rules (clipped fill, fitted corners).
    public static class LoadingScreenInstaller
    {
        const string BootstrapScene = "Assets/_Project/Scenes/Bootstrap.unity";
        const string DisplayFontPath = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Fonts/Cairo_Line_Black SDF_Light.asset";

        [MenuItem("ZombieWar/UI/M8/Install Loading Screen (Bootstrap splash)")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Loading] Not in Play Mode."); return; }
            var scene = SceneManager.GetSceneByPath(BootstrapScene);
            bool opened = false;
            if (!scene.IsValid() || !scene.isLoaded) { scene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Additive); opened = true; }
            try
            {
                GameObject canvas = null;
                foreach (var go in scene.GetRootGameObjects()) if (go.name == "SplashCanvas") canvas = go;
                if (canvas == null) { Debug.LogError("[Loading] SplashCanvas not found in Bootstrap."); return; }
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
                var root = canvas.transform;

                var bg = root.Find("Bg")?.GetComponent<Image>();
                if (bg != null) bg.color = UITheme.M8Ground;

                var logo = root.Find("Logo");
                if (logo != null)
                {
                    var box = logo.GetComponent<Image>();
                    if (box != null) box.color = new Color(0, 0, 0, 0);
                    var outline = logo.GetComponent<Outline>();
                    if (outline != null) Object.DestroyImmediate(outline);
                    logo.Find("Sub")?.gameObject.SetActive(false);
                    var title = logo.Find("Title")?.GetComponent<TMP_Text>();
                    if (title != null)
                    {
                        title.font = font; title.fontSharedMaterial = font.material;
                        title.fontStyle = FontStyles.Normal; title.fontSize = 150; title.color = UITheme.M8Yellow;
                        title.enableWordWrapping = false;
                        var trt = (RectTransform)title.transform; trt.sizeDelta = new Vector2(1000, 200); trt.anchoredPosition = Vector2.zero;
                    }
                }

                var status = root.Find("Status")?.GetComponent<TMP_Text>();
                if (status != null)
                {
                    status.font = font; status.fontSharedMaterial = font.material; status.fontStyle = FontStyles.Normal;
                    status.fontSize = 36; status.color = UITheme.M8InkDim; status.enableWordWrapping = true;
                    status.alignment = TextAlignmentOptions.Center;
                    var srt = (RectTransform)status.transform; srt.sizeDelta = new Vector2(860, 120); srt.anchoredPosition = new Vector2(0, -300);
                }

                var slider = root.GetComponentInChildren<Slider>(true);
                if (slider != null)
                {
                    var srt = (RectTransform)slider.transform; srt.sizeDelta = new Vector2(640, 22); srt.anchoredPosition = new Vector2(0, -400);
                    var track = slider.transform.Find("Track")?.GetComponent<Image>();
                    if (track != null) track.color = UITheme.M8Deep;
                    if (slider.fillRect != null)
                    {
                        UiKitApply.ConvertBar(canvas, slider.fillRect.gameObject);
                        var g = slider.fillRect.Find("Graphic")?.GetComponent<Image>();
                        if (g != null) g.color = UITheme.M8Yellow;
                    }
                    UiKitApply.AddSliceFit(canvas);
                }

                var ls = canvas.GetComponent<LoadingScreen>() ?? canvas.AddComponent<LoadingScreen>();
                var so = new SerializedObject(ls);
                so.FindProperty("group").objectReferenceValue = canvas.GetComponent<CanvasGroup>();
                so.FindProperty("progress").objectReferenceValue = slider;
                so.FindProperty("status").objectReferenceValue = status;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[Loading] Bootstrap splash is now the loading screen.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
