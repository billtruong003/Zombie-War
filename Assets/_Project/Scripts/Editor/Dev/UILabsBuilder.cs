using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZombieWar.Dev;

namespace ZombieWar.EditorTools
{
    /// UI Labs (2026-10-03): a dev scene for trying UI shaders on real UI assets before they reach a
    /// screen. Built additively so the open scene is left alone; nothing here touches the game UI.
    public static class UILabsBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Dev/UILabs.unity";
        const string Root = "Assets/_Project/UI/Labs";
        const string Sprites = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites/Components/";
        const string OutDir = "Review/UILabs";

        static readonly Color Bg = new Color(0.067f, 0.086f, 0.122f);
        static readonly Color Panel = new Color(0.102f, 0.129f, 0.176f);
        static readonly Color Ink = new Color(0.91f, 0.93f, 0.96f);
        static readonly Color Muted = new Color(0.55f, 0.59f, 0.67f);

        [MenuItem("HordeCall/Dev/UI Labs/Build Scene")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("[UILabs] exit Play first"); return; }
            var old = SceneManager.GetSceneByPath(ScenePath);
            if (old.IsValid() && old.isLoaded) EditorSceneManager.CloseScene(old, true);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var noise = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/Noise/Noise_Perlin_01.png");
            var dissolveNoise = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/T_DissolveNoise.png");
            var holo = Mat("Hologram", "ZombieWar/UI/Hologram", noise);
            var dissolve = Mat("Dissolve", "ZombieWar/UI/Dissolve", dissolveNoise);
            dissolve.SetVector("_NoiseRange", new Vector4(0.52f, 0.84f, 0, 0));   // T_DissolveNoise spans 134..213 of 255
            var shine = Mat("Shine", "ZombieWar/UI/Shine", null);
            var ring = Mat("EnergyRing", "ZombieWar/UI/EnergyRing", noise);
            var ringCyan = Mat("EnergyRing_Cyan", "ZombieWar/UI/EnergyRing", noise);
            ringCyan.SetColor("_Color", new Color(0.35f, 0.9f, 1f, 1f));
            ringCyan.SetFloat("_Dashes", 4); ringCyan.SetFloat("_DashFill", 0.78f); ringCyan.SetFloat("_Spin", -0.05f);

            var camGo = new GameObject("LabCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Bg;
            camGo.transform.position = new Vector3(0, 0, -10);

            var canvasGo = new GameObject("LabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            Text(root, "HordeCall · UI Labs", 44, new Vector2(0, 470), new Vector2(1800, 60), Ink);
            Text(root, "Each shader: 1-2 texture samples, no runtime noise maths; effects keep the UI stencil and mask rules.", 22, new Vector2(0, 420), new Vector2(1800, 40), Muted);

            // 1. Hologram on the agent portrait
            var p1 = PanelAt(root, "Hologram", "Radio portrait. 2 samples: noise row (glitch + flicker) + portrait. Lines are frac/step.", -705);
            var frame = Box(p1, new Vector2(0, 40), new Vector2(360, 360), new Color(0.15f, 0.19f, 0.11f));
            var portrait = Img(frame, Load<Sprite>(Root + "/Textures/T_Lab_AgentPortrait.png"), Vector2.zero, new Vector2(340, 340));
            portrait.material = holo;
            Text(p1, "_Glitch / _Flicker / _Lines tune it; low tier: plain UI material", 18, new Vector2(0, -280), new Vector2(400, 60), Muted);

            // 2. Dissolve on a feature icon
            var p2 = PanelAt(root, "Dissolve", "Feature unlock, gacha reveal. 2 samples: sprite + dissolve noise. _Progress from code.", -235);
            var icon = Img(p2, Load<Sprite>(Sprites + "Icon_ItemIcons/256/ItemIcon_Star_Gold.Png"), new Vector2(0, 40), new Vector2(300, 300));
            icon.material = dissolve;
            var demo = icon.gameObject.AddComponent<UILabsDemo>();
            var so = new SerializedObject(demo);
            so.FindProperty("target").objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();
            Text(p2, "Loops 0 → 1 → 0 here; BillTween drives it in a real screen", 18, new Vector2(0, -280), new Vector2(400, 60), Muted);

            // 3. Shine on buttons
            var p3 = PanelAt(root, "Shine", "Buttons, cards, rarity. 1 sample (the sprite); the band is a distance in uv.", 235);
            string[] colours = { "Blue", "Green", "Purple" };
            for (int i = 0; i < 3; i++)
            {
                var b = Img(p3, Load<Sprite>(Sprites + $"Button/Button01_l_{colours[i]}.png"), new Vector2(0, 170 - i * 130), new Vector2(340, 110));
                b.type = Image.Type.Simple;
                b.material = shine;
                Text(b.transform, i == 0 ? "PLAY" : i == 1 ? "CLAIM" : "FREE PULL", 34, Vector2.zero, new Vector2(340, 110), Color.white);
            }
            Text(p3, "Simple sprites; 9-slice needs rect uv in uv1", 18, new Vector2(0, -280), new Vector2(400, 60), Muted);

            // 4. Energy rings
            var p4 = PanelAt(root, "Energy Ring", "FTUE highlight, joystick hint, reticle. 1 noise sample, one atan2. Plain Image.", 705);
            Img(p4, null, new Vector2(0, 40), new Vector2(320, 320)).material = ring;
            var inner = Img(p4, null, new Vector2(0, 40), new Vector2(190, 190));
            inner.material = ringCyan;
            Text(p4, "Additive; keep the quad small, cost scales with its pixels", 18, new Vector2(0, -280), new Vector2(400, 60), Muted);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[UILabs] built " + ScenePath);
        }

        [MenuItem("HordeCall/Dev/UI Labs/Capture")]
        public static void Capture()
        {
            var cam = GameObject.Find("LabCamera")?.GetComponent<Camera>();
            if (cam == null) { Debug.LogWarning("[UILabs] open " + ScenePath + " first"); return; }
            Directory.CreateDirectory(OutDir);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            // Freeze the dissolve mid-burn so the still shows its edge.
            foreach (var d in Object.FindObjectsByType<UILabsDemo>(FindObjectsSortMode.None))
                if (d.TryGetComponent(out Graphic g) && g.material != null) g.material.SetFloat("_Progress", 0.6f);
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            cam.targetTexture = prev;
            var path = Path.Combine(OutDir, $"uilabs_{System.DateTime.Now:HHmmss}.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Debug.Log("[UILabs] captured " + path);
        }

        static Material Mat(string name, string shaderName, Texture noise)
        {
            Directory.CreateDirectory(Root + "/Materials");
            string path = $"{Root}/Materials/M_UILab_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shaderName));
                AssetDatabase.CreateAsset(mat, path);
            }
            if (noise != null) mat.SetTexture("_NoiseTex", noise);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static T Load<T>(string path) where T : Object
        {
            if (typeof(T) == typeof(Sprite) && AssetImporter.GetAtPath(path) is TextureImporter ti
                && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        static RectTransform PanelAt(Transform parent, string title, string note, float x)
        {
            var p = Box(parent, new Vector2(x, -60), new Vector2(440, 820), Panel);
            Text(p, title, 34, new Vector2(0, 360), new Vector2(400, 50), new Color(1f, 0.82f, 0.24f));
            Text(p, note, 18, new Vector2(0, 300), new Vector2(400, 70), Ink);
            return p;
        }

        static RectTransform Box(Transform parent, Vector2 pos, Vector2 size, Color colour)
        {
            var img = Img(parent, null, pos, size);
            img.color = colour;
            return img.rectTransform;
        }

        static Image Img(Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(sprite != null ? sprite.name : "Image", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        static void Text(Transform parent, string text, float size, Vector2 pos, Vector2 box, Color colour)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = colour;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
        }
    }
}
