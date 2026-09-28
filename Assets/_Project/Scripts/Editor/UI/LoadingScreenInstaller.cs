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
        const string ArtDir = "Assets/_Project/UI/Sprites/Loading/";
        static readonly string[] Art = { "loading_bg_01", "loading_bg_02", "loading_bg_03" };
        const string DisplayFontPath = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Fonts/Cairo_Line_Black SDF_Light.asset";

        /// A loading sprite, imported as a UI sprite (no mipmaps, capped size).
        static Sprite Sprite(string name, int maxSize)
        {
            string path = ArtDir + name + ".png";
            if (AssetImporter.GetAtPath(path) is TextureImporter imp &&
                (imp.textureType != TextureImporterType.Sprite || imp.maxTextureSize != maxSize || imp.mipmapEnabled))
            {
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.maxTextureSize = maxSize;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

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

                // Key art (owner 2026-09-29): covers the screen at any aspect without stretching.
                var arts = new Sprite[Art.Length];
                for (int i = 0; i < Art.Length; i++) arts[i] = Sprite(Art[i], 2048);
                var bg = root.Find("Bg")?.GetComponent<Image>();
                if (bg != null)
                {
                    bg.color = arts[0] != null ? Color.white : UITheme.M8Ground;
                    bg.sprite = arts[0];
                    var fit = bg.GetComponent<AspectRatioFitter>() ?? bg.gameObject.AddComponent<AspectRatioFitter>();
                    fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                    fit.aspectRatio = arts[0] != null ? arts[0].rect.width / arts[0].rect.height : 1080f / 2400f;
                }

                var logo = root.Find("Logo");
                if (logo != null)
                {
                    var box = logo.GetComponent<Image>();
                    if (box != null) box.color = new Color(0, 0, 0, 0);
                    var outline = logo.GetComponent<Outline>();
                    if (outline != null) Object.DestroyImmediate(outline);
                    logo.Find("Sub")?.gameObject.SetActive(false);
                    var logoArt = Sprite("logo_hordecall", 1024);
                    if (logoArt != null && box != null)
                    {
                        box.sprite = logoArt; box.color = Color.white; box.preserveAspect = true; box.raycastTarget = false;
                        ((RectTransform)logo).sizeDelta = new Vector2(760, 380);
                        logo.Find("Title")?.gameObject.SetActive(false);
                    }
                    var title = logo.Find("Title")?.GetComponent<TMP_Text>();
                    if (title != null && logoArt == null)
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
                    status.fontSize = 36; status.color = Color.white; status.enableWordWrapping = true;
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

                // A soft dark band behind the tip and the bar, so they read on any key art.
                var scrim = root.Find("Scrim") ?? new GameObject("Scrim", typeof(RectTransform), typeof(Image)).transform;
                scrim.SetParent(root, false);
                scrim.SetSiblingIndex(bg != null ? bg.transform.GetSiblingIndex() + 1 : 0);
                var srt2 = (RectTransform)scrim; srt2.anchorMin = srt2.anchorMax = new Vector2(0.5f, 0.5f);
                srt2.sizeDelta = new Vector2(1400, 330); srt2.anchoredPosition = new Vector2(0, -345);
                var si = scrim.GetComponent<Image>(); si.sprite = Sprite("scrim", 256); si.raycastTarget = false; si.color = Color.white;

                var ls = canvas.GetComponent<LoadingScreen>() ?? canvas.AddComponent<LoadingScreen>();
                var so = new SerializedObject(ls);
                so.FindProperty("group").objectReferenceValue = canvas.GetComponent<CanvasGroup>();
                so.FindProperty("progress").objectReferenceValue = slider;
                so.FindProperty("status").objectReferenceValue = status;
                so.FindProperty("background").objectReferenceValue = bg;
                var list = so.FindProperty("backgrounds"); list.arraySize = arts.Length;
                for (int i = 0; i < arts.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = arts[i];
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
