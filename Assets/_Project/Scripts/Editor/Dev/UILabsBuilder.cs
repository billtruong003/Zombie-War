using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZombieWar.Dev;
using ZombieWar.UI;

namespace ZombieWar.EditorTools
{
    /// UI Labs (v2, 2026-10-03): every FTUE radio-call component on real game assets, plus two
    /// complete FTUE screens composed on game captures. Built additively; touches no game UI.
    /// Press Play in the scene to see typing, the tapping hand and the radio lines cycle.
    public static class UILabsBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Dev/UILabs.unity";
        const string Root = "Assets/_Project/UI/Labs";
        const string LL = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/";
        const string OutDir = "Review/UILabs";

        static readonly Color Bg = new Color(0.067f, 0.086f, 0.122f);
        static readonly Color PanelCol = new Color(0.102f, 0.129f, 0.176f);
        static readonly Color Ink = new Color(0.91f, 0.93f, 0.96f);
        static readonly Color Muted = new Color(0.55f, 0.59f, 0.67f);
        static readonly Color Yellow = new Color(1f, 0.82f, 0.24f);
        static readonly Color RadioBg = new Color(0.149f, 0.188f, 0.110f);
        static readonly Color RadioInk = new Color(0.933f, 0.961f, 0.878f);
        static readonly Color Green = new Color(0.66f, 0.88f, 0.35f);
        static readonly Color Dark = new Color(0.063f, 0.082f, 0.122f);

        static TMP_FontAsset _body, _title;
        static Sprite _round;
        static Material _holo, _holoHand, _dissolve, _shine, _ring, _reticle, _spot, _wave, _stripe;

        [MenuItem("HordeCall/Dev/UI Labs/Build Scene")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("[UILabs] exit Play first"); return; }
            // New scene first: the old copy may be the only open scene, which cannot be closed alone.
            var old = SceneManager.GetSceneByPath(ScenePath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            if (old.IsValid()) EditorSceneManager.CloseScene(old, true);
            LoadAssets();

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
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            Label(root, "HordeCall · UI Labs · FTUE radio call", 40, new Vector2(0, 505), new Vector2(1880, 54), Ink, _title);
            Label(root, "Thành phần FTUE trên asset thật · bấm Play trong scene này để xem chữ chạy, tay gõ, bộ đàm đổi câu", 20, new Vector2(0, 468), new Vector2(1880, 30), Muted);

            BuildGallery(root);
            BuildPhoneMove(root, new Vector2(275, -40));
            BuildPhoneArsenal(root, new Vector2(735, -40));

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[UILabs] built " + ScenePath);
        }

        // ───────────────────────────────────────── in-game radio overlay

        const string OverlayPath = "Assets/_Project/UI/Radio/Resources/UI/RadioOverlay.prefab";

        /// The FTUE v3 radio-call overlay the game uses (FtueRadio): the UI Labs components on their
        /// own overlay canvas (1080×1920, above every screen, no input). Cards are the Lab's radio card
        /// at phone width 1014 (normal, small, with item icon) plus a small subtitle card; markers are
        /// the Lab's dim, spotlight hole, energy ring, reticle corners, chip and hologram tapping hand.
        [MenuItem("HordeCall/UI/Radio/Build Overlay Prefab")]
        public static void BuildRadioOverlay()
        {
            if (Application.isPlaying) { Debug.LogWarning("[RadioOverlay] exit Play first"); return; }
            LoadAssets();
            var root = new GameObject("RadioOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            // Full-screen layers first (under the safe area, so they cover the notch too).
            var dim = Img(root.transform, null, Vector2.zero, Vector2.zero);
            dim.name = "Dim";
            Stretch(dim.rectTransform, 0, 0, 0, 0);
            var spot = Img(root.transform, null, Vector2.zero, Vector2.zero);
            spot.name = "Spotlight";
            Stretch(spot.rectTransform, 0, 0, 0, 0);
            spot.gameObject.AddComponent<UIRectUV>();
            var hole = spot.gameObject.AddComponent<UISpotlightHole>();

            var safe = new GameObject("Safe", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(root.transform, false);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.offsetMin = safe.offsetMax = Vector2.zero;

            var spotTarget = new GameObject("SpotTarget", typeof(RectTransform)).GetComponent<RectTransform>();
            spotTarget.SetParent(safe, false);
            var ring = Img(safe, null, Vector2.zero, new Vector2(300, 300));
            ring.name = "Ring";
            ring.material = _ring;
            var reticle = Reticle(safe, Vector2.zero, new Vector2(300, 300));
            var chip = Chip(safe, "DRAG ANYWHERE", Vector2.zero, 30);
            chip.name = "Chip";
            var chipText = chip.GetComponentInChildren<TextMeshProUGUI>();
            chip.sizeDelta = new Vector2(420, 52);

            RadioCallView Card(string name, bool small, Sprite item)
            {
                var v = Radio(safe, Vector2.zero, 1014, small, item, 1f);
                v.name = name;
                var rt = (RectTransform)v.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                var g = v.gameObject.AddComponent<CanvasGroup>();
                g.blocksRaycasts = false; g.interactable = false; g.alpha = 0f;
                return v;
            }
            var normal = Card("CardNormal", false, null);
            var small = Card("CardSmall", true, null);
            var itemCard = Card("CardItem", false, Spr($"{Root}/Textures/Screens/T_Lab_Icon_Magnet.jpg"));
            var sub = Card("CardSubtitle", true, null);

            var handRoot = new GameObject("HandRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            handRoot.SetParent(safe, false);
            var hand = Hand(handRoot, Vector2.zero, 180);
            // The fingertip is the sprite's top-left; the hand hangs below-right of the tap point.
            ((RectTransform)hand.transform).pivot = new Vector2(0.18f, 0.86f);

            foreach (var gr in root.GetComponentsInChildren<Graphic>(true)) gr.raycastTarget = false;

            var overlay = root.AddComponent<FtueRadio>();
            var ids = new[] { "riley", "lukas", "chen", "kaito", "jiho", "mai" };
            var so = new SerializedObject(overlay);
            so.FindProperty("safe").objectReferenceValue = safe;
            so.FindProperty("cardNormal").objectReferenceValue = normal;
            so.FindProperty("cardSmall").objectReferenceValue = small;
            so.FindProperty("cardItem").objectReferenceValue = itemCard;
            so.FindProperty("cardSub").objectReferenceValue = sub;
            so.FindProperty("itemIcon").objectReferenceValue = itemCard.transform.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i.name == "ItemIcon");
            so.FindProperty("dim").objectReferenceValue = dim;
            so.FindProperty("spot").objectReferenceValue = spot;
            so.FindProperty("ring").objectReferenceValue = ring;
            so.FindProperty("reticle").objectReferenceValue = reticle;
            so.FindProperty("spotHole").objectReferenceValue = hole;
            so.FindProperty("spotTarget").objectReferenceValue = spotTarget;
            so.FindProperty("chip").objectReferenceValue = chip;
            so.FindProperty("chipText").objectReferenceValue = chipText;
            so.FindProperty("handRoot").objectReferenceValue = handRoot;
            var a = so.FindProperty("agentIds"); var f = so.FindProperty("faces");
            a.arraySize = f.arraySize = ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                a.GetArrayElementAtIndex(i).stringValue = ids[i];
                f.GetArrayElementAtIndex(i).objectReferenceValue = Face(ids[i]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var hs = new SerializedObject(hole);
            hs.FindProperty("target").objectReferenceValue = spotTarget;
            hs.FindProperty("baseMaterial").objectReferenceValue = _spot;
            hs.FindProperty("padding").floatValue = 0f;
            hs.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(OverlayPath));
            PrefabUtility.SaveAsPrefabAsset(root, OverlayPath);
            Object.DestroyImmediate(root);
            Debug.Log("[RadioOverlay] built " + OverlayPath);
        }

        // ───────────────────────────────────────── assets

        static void LoadAssets()
        {
            _body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LL + "Fonts/Cairo SDF.asset");
            _title = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LL + "Fonts/Cairo_Line_Black SDF_Light.asset");
            _round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var noise = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/Noise/Noise_Perlin_01.png");
            var dnoise = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/T_DissolveNoise.png");
            _holo = Mat("Hologram", "ZombieWar/UI/Hologram", noise);
            _holo.SetFloat("_Fade", 0.12f);
            _holoHand = Mat("Hologram_Hand", "ZombieWar/UI/Hologram", noise);
            _holoHand.SetFloat("_HoloMix", 0.45f); _holoHand.SetFloat("_Fade", 0f); _holoHand.SetFloat("_Lines", 60); _holoHand.SetFloat("_Glitch", 0.006f);
            _dissolve = Mat("Dissolve", "ZombieWar/UI/Dissolve", dnoise);
            _dissolve.SetVector("_NoiseRange", new Vector4(0.52f, 0.84f, 0, 0));
            _shine = Mat("Shine", "ZombieWar/UI/Shine", null);
            _ring = Mat("EnergyRing", "ZombieWar/UI/EnergyRing", noise);
            _reticle = Mat("Reticle", "ZombieWar/UI/Reticle", null);
            _spot = Mat("Spotlight", "ZombieWar/UI/Spotlight", null);
            // UI blends in linear space: 0.88 here reads like the mockup's ~0.75 dim.
            _spot.SetColor("_Color", new Color(0.03f, 0.05f, 0.09f, 0.88f));
            _wave = Mat("Waveform", "ZombieWar/UI/Waveform", noise);
            _stripe = Mat("HazardStripe", "ZombieWar/UI/HazardStripe", null);
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
            else mat.shader = Shader.Find(shaderName);
            if (noise != null) mat.SetTexture("_NoiseTex", noise);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Sprite Spr(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite Face(string id) => Spr($"{Root}/Textures/Portraits/T_Agent_{id}.png");

        // ───────────────────────────────────────── gallery (left half)

        static void BuildGallery(Transform root)
        {
            // Radio call: the three card variants
            var p1 = Panel(root, "Khung bộ đàm", "Thường · nhỏ · có icon vật phẩm. Sọc vàng đen, chân dung hologram, sóng âm khi đang nói, chữ chạy.", new Vector2(-480, 255), new Vector2(930, 380));
            var c1 = Radio(p1, new Vector2(-232, 45), 1000, false, null, 0.43f);
            Cycle(c1, ("● HQ · NIGHTFIN", "DRAG TO MOVE", "Drag anywhere to move. Your gun fires at the nearest monster by itself.", "riley"),
                      ("● HQ · NIGHTFIN", "THREAT RISING", "They know you're here. Keep moving.", "riley"));
            var c2 = Radio(p1, new Vector2(232, 45), 1000, false, Spr($"{Root}/Textures/Screens/T_Lab_Icon_Magnet.jpg"), 0.43f);
            Cycle(c2, ("● HQ · TIGER · NEW ITEM", "MAGNET", "Every coin on the map is coming your way.", "mai"));
            var c3 = Radio(p1, new Vector2(-232, -105), 1000, true, null, 0.43f);
            Cycle(c3, ("● HQ · SHARK", "ALPHA SIGHTED", "Big one. Keep your distance!", "kaito"),
                      ("● HQ · SHARK", "500 DOWN", "You're catching up to me.", "kaito"));
            var c4 = Radio(p1, new Vector2(232, -105), 1000, true, null, 0.43f);
            Cycle(c4, ("● HQ · SMOG · NEW STATION", "SIGNAL RELAY", "Hold the ring for 12 s. I'll do the rest.", "jiho"));

            // Agents: hologram portraits
            var p2 = Panel(root, "Chân dung đặc vụ · hologram", "Trong game là RenderTexture trực tiếp; ở đây là ảnh render nền trong suốt.", new Vector2(-480, -47), new Vector2(930, 200));
            string[] ids = { "riley", "lukas", "chen", "kaito", "jiho", "mai" };
            string[] names = { "Riley", "Lukas", "Chen", "Kaito", "Ji-ho", "Mai" };
            for (int i = 0; i < ids.Length; i++)
            {
                var x = -375 + i * 150;
                var frame = Rounded(p2, new Vector2(x, -24), new Vector2(96, 96), new Color(0.24f, 0.35f, 0.16f));
                frame.gameObject.AddComponent<RectMask2D>();
                var face = Img(frame, Face(ids[i]), new Vector2(0, -20), new Vector2(145, 145));
                face.material = _holo;
                Label(p2, names[i], 17, new Vector2(x, -82), new Vector2(140, 24), Ink);
            }

            // Buttons + markers
            var p3 = Panel(root, "Nút có vệt sáng · dấu chỉ dẫn", "Nút 9-slice đúng tỉ lệ (vệt sáng tính theo pixel trong khung) · góc ngắm · vòng năng lượng · tay gõ · nhãn · dissolve.", new Vector2(-480, -339), new Vector2(930, 360));
            Button(p3, "Button01_l_Green", "CLAIM", new Vector2(-330, 60), new Vector2(240, 80));
            Button(p3, "Button01_l_Sky", "FREE PULL", new Vector2(-330, -30), new Vector2(240, 80));
            Button(p3, "Button01_l_Purple", "BUY MAKAROV · 400", new Vector2(-260, -120), new Vector2(380, 80));
            var tgt = Rounded(p3, new Vector2(60, -30), new Vector2(120, 120), new Color(1, 1, 1, 0.08f));
            Reticle(tgt.parent, new Vector2(60, -30), new Vector2(148, 148));
            Chip(p3, "SIGNAL RELAY · 12 s", new Vector2(60, -126));
            var ring = Img(p3, null, new Vector2(230, -30), new Vector2(150, 150));
            ring.material = _ring;
            Hand(p3, new Vector2(250, -50), 96);
            var star = Img(p3, Spr(LL + "Sprites/Components/Icon_ItemIcons/256/ItemIcon_Star_Gold.Png"), new Vector2(385, -30), new Vector2(120, 120));
            star.material = _dissolve;
            var demo = star.gameObject.AddComponent<UILabsDemo>();
            var so = new SerializedObject(demo);
            so.FindProperty("target").objectReferenceValue = star;
            so.ApplyModifiedPropertiesWithoutUndo();
            Label(p3, "dissolve hiện / ẩn", 15, new Vector2(385, -112), new Vector2(180, 22), Muted);
        }

        // ───────────────────────────────────────── phones (right half)

        static RectTransform Phone(Transform root, string title, Vector2 pos, string bg)
        {
            Label(root, title, 20, pos + new Vector2(0, 410), new Vector2(440, 28), Ink, _title);
            var frame = Rounded(root, pos, new Vector2(446, 782), Dark);
            var holder = new GameObject("Screen", typeof(RectTransform));
            var rt = (RectTransform)holder.transform;
            rt.SetParent(frame, false);
            rt.sizeDelta = new Vector2(1080, 1920);
            rt.localScale = Vector3.one * 0.4f;
            Img(rt, Spr($"{Root}/Textures/Screens/{bg}"), Vector2.zero, new Vector2(1080, 1920));
            return rt;
        }

        /// Places a child of a 1080x1920 phone by its top-left corner, like the mockup coordinates.
        static void At(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x + w / 2, -(y + h / 2));
        }

        static void BuildPhoneMove(Transform root, Vector2 pos)
        {
            var s = Phone(root, "Màn ghép: Kéo để đi", pos, "T_Lab_Screen_Move.jpg");
            var dim = Img(s, null, Vector2.zero, new Vector2(1080, 1920));
            dim.color = new Color(0.03f, 0.05f, 0.09f, 0.35f);
            var card = Radio(s, Vector2.zero, 1014, false, null, 1f);
            At((RectTransform)card.transform, 33, 216, 1014, 276);
            Cycle(card, ("● HQ · NIGHTFIN", "DRAG TO MOVE", "Drag anywhere to move. Your gun fires at the nearest monster by itself.", "riley"),
                        ("● HQ · NIGHTFIN", "STAY ALIVE", "Keep moving. Standing still gets you swarmed.", "riley"));
            var ring = Img(s, null, Vector2.zero, Vector2.zero);
            ring.material = _ring;
            At(ring.rectTransform, 91, 1528, 300, 300);
            var ret = Reticle(s, Vector2.zero, Vector2.zero);
            At(ret.rectTransform, 83, 1517, 316, 316);
            var chip = Chip(s, "DRAG ANYWHERE", Vector2.zero, 30);
            At(chip, 83, 1846, 330, 52);
            var hand = Hand(s, Vector2.zero, 180);
            At((RectTransform)hand.transform, 240, 1676, 180, 180);
        }

        static void BuildPhoneArsenal(Transform root, Vector2 pos)
        {
            var s = Phone(root, "Màn ghép: Súng đầu tiên", pos, "T_Lab_Screen_Arsenal.jpg");
            // The gun cell the gift paid for, as the spotlight's target.
            var cell = new GameObject("TargetCell", typeof(RectTransform));
            var cellRt = (RectTransform)cell.transform;
            cellRt.SetParent(s, false);
            At(cellRt, 385, 1100, 310, 272);
            var spot = Img(s, null, Vector2.zero, new Vector2(1080, 1920));
            spot.gameObject.AddComponent<UIRectUV>();
            var hole = spot.gameObject.AddComponent<UISpotlightHole>();
            var so = new SerializedObject(hole);
            so.FindProperty("target").objectReferenceValue = cellRt;
            so.FindProperty("baseMaterial").objectReferenceValue = _spot;
            so.ApplyModifiedPropertiesWithoutUndo();
            var ret = Reticle(s, Vector2.zero, Vector2.zero);
            At(ret.rectTransform, 371, 1086, 338, 300);
            var buy = Button(s, "Button01_l_Green", "BUY MAKAROV · 400", Vector2.zero, Vector2.zero, 44);
            At(buy.rectTransform, 240, 1420, 600, 130);
            var card = Radio(s, Vector2.zero, 1014, false, null, 1f);
            At((RectTransform)card.transform, 33, 700, 1014, 276);
            Cycle(card, ("● HQ · RAPTOR · ARMORY", "YOUR FIRST NEW GUN", "Makarov. Hits harder than your pistol. You have the coins. Buy it.", "lukas"));
            var hand = Hand(s, Vector2.zero, 180);
            At((RectTransform)hand.transform, 700, 1500, 180, 180);
        }

        // ───────────────────────────────────────── components

        /// The radio card, laid out in phone pixels (width w); scale shrinks it for the gallery.
        static RadioCallView Radio(Transform parent, Vector2 pos, float w, bool small, Sprite item, float scale)
        {
            float h = small ? 214 : 276, port = small ? 136 : 180, pad = 22, top = 22;
            var go = new GameObject(small ? "RadioCall_Small" : "RadioCall", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one * scale;

            var shadow = Rounded(rt, new Vector2(0, -10), new Vector2(w, h), new Color(0, 0, 0, 0.45f));
            var border = Rounded(rt, Vector2.zero, new Vector2(w, h), Dark);
            var card = Rounded(border, Vector2.zero, new Vector2(w - 12, h - 12), RadioBg);
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var stripe = Img(card, null, Vector2.zero, Vector2.zero);
            stripe.material = _stripe;
            stripe.gameObject.AddComponent<UIRectUV>();
            Stretch(stripe.rectTransform, 0, 0, 0, top, top: true);

            var frame = Rounded(card, Vector2.zero, new Vector2(port, port), new Color(0.24f, 0.35f, 0.16f));
            Anchor(frame, new Vector2(0, 1), new Vector2(pad + port / 2, -(top + pad + port / 2 - 4)));
            frame.gameObject.AddComponent<RectMask2D>();
            // Zoomed to head and shoulders.
            var face = Img(frame, null, new Vector2(0, -port * 0.2f), new Vector2(port * 1.45f, port * 1.45f));
            face.material = _holo;

            float tx = pad + port + 24;
            float tw = w - 12 - tx - pad - (item != null ? 150 : 0);
            var channel = Text(card, "● HQ", small ? 24 : 27, new Vector2(tx, -(top + 18)), new Vector2(tw, 34), Green);
            var wave = Img(card, null, Vector2.zero, new Vector2(70, 26));
            wave.material = _wave;
            Anchor(wave.rectTransform, new Vector2(1, 1), new Vector2(-(pad + 35 + (item != null ? 150 : 0)), -(top + 34)));
            var title = Text(card, "TITLE", small ? 40 : 46, new Vector2(tx, -(top + 50)), new Vector2(tw, 56), Yellow, _title);
            var body = Text(card, "Body", small ? 26 : 29, new Vector2(tx, -(top + (small ? 96 : 104))), new Vector2(tw, small ? 80 : 120), RadioInk);
            body.enableWordWrapping = true;
            body.alignment = TextAlignmentOptions.TopLeft;

            if (item != null)
            {
                var ic = Rounded(card, Vector2.zero, new Vector2(124, 124), Dark);
                Anchor(ic, new Vector2(1, 1), new Vector2(-(pad + 62), -(top + 40 + 62)));
                Img(ic, item, Vector2.zero, new Vector2(116, 116)).name = "ItemIcon";
            }

            var view = go.AddComponent<RadioCallView>();
            var so = new SerializedObject(view);
            so.FindProperty("portrait").objectReferenceValue = face;
            so.FindProperty("channel").objectReferenceValue = channel;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("waveform").objectReferenceValue = wave;
            so.FindProperty("waveformMaterial").objectReferenceValue = _wave;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        static void Cycle(RadioCallView view, params (string ch, string title, string body, string face)[] lines)
        {
            var cyc = view.gameObject.AddComponent<UILabsRadioCycle>();
            var so = new SerializedObject(cyc);
            so.FindProperty("view").objectReferenceValue = view;
            var arr = so.FindProperty("lines");
            arr.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("channel").stringValue = lines[i].ch;
                e.FindPropertyRelative("title").stringValue = lines[i].title;
                e.FindPropertyRelative("body").stringValue = lines[i].body;
                e.FindPropertyRelative("face").objectReferenceValue = Face(lines[i].face);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            view.Say(lines[0].ch, lines[0].title, lines[0].body, Face(lines[0].face));
        }

        static Image Button(Transform parent, string sprite, string label, Vector2 pos, Vector2 size, float fontSize = 30)
        {
            var b = Img(parent, Spr(LL + $"Sprites/Components/Button/{sprite}.png"), pos, size);
            b.type = Image.Type.Sliced;
            b.material = _shine;
            b.gameObject.AddComponent<UIRectUV>();
            var t = Text(b.transform, label, fontSize, Vector2.zero, size, Color.white, _title);
            t.alignment = TextAlignmentOptions.Center;
            Stretch(t.rectTransform, 0, 0, 0, 0);
            t.rectTransform.anchoredPosition = new Vector2(0, 3);
            return b;
        }

        static Image Reticle(Transform parent, Vector2 pos, Vector2 size)
        {
            var r = Img(parent, null, pos, size);
            r.material = _reticle;
            r.gameObject.AddComponent<UIRectUV>();
            r.name = "Reticle";
            return r;
        }

        static RectTransform Chip(Transform parent, string text, Vector2 pos, float fontSize = 16)
        {
            var bg = Img(parent, _round, pos, new Vector2(Mathf.Max(120, text.Length * fontSize * 0.62f + 24), fontSize * 1.7f));
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.063f, 0.082f, 0.122f, 0.88f);
            var t = Text(bg.transform, text, fontSize, Vector2.zero, bg.rectTransform.sizeDelta, Yellow);
            t.alignment = TextAlignmentOptions.Center;
            Stretch(t.rectTransform, 0, 0, 0, 0);
            return bg.rectTransform;
        }

        static TapHand Hand(Transform parent, Vector2 pos, float size)
        {
            var h = Img(parent, Spr(LL + "Sprites/Components/Icon_TutorialHand/256/tutorial_hand_3.png"), pos, new Vector2(size, size));
            h.material = _holoHand;
            h.name = "TapHand";
            return h.gameObject.AddComponent<TapHand>();
        }

        // ───────────────────────────────────────── primitives

        static RectTransform Panel(Transform parent, string title, string note, Vector2 pos, Vector2 size)
        {
            var p = Rounded(parent, pos, size, PanelCol);
            var t = Text(p, title, 24, new Vector2(20, -12), new Vector2(size.x - 40, 32), Yellow, _title);
            var n = Text(p, note, 15, new Vector2(20, -44), new Vector2(size.x - 40, 40), Muted);
            n.enableWordWrapping = true;
            return p;
        }

        static RectTransform Rounded(Transform parent, Vector2 pos, Vector2 size, Color colour)
        {
            var img = Img(parent, _round, pos, size);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.35f;   // bigger corner radius from the built-in 9-slice
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

        /// Text placed by its top-left corner inside the parent's top-left.
        static TextMeshProUGUI Text(Transform parent, string text, float size, Vector2 topLeft, Vector2 box, Color colour, TMP_FontAsset font = null)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = box;
            var t = go.GetComponent<TextMeshProUGUI>();
            if ((font ?? _body) != null) t.font = font ?? _body;
            t.text = text;
            t.fontSize = size;
            t.color = colour;
            t.enableWordWrapping = false;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.raycastTarget = false;
            return t;
        }

        static void Label(Transform parent, string text, float size, Vector2 pos, Vector2 box, Color colour, TMP_FontAsset font = null)
        {
            var t = Text(parent, text, size, Vector2.zero, box, colour, font);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            t.alignment = TextAlignmentOptions.Center;
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
        }

        static void Stretch(RectTransform rt, float l, float r, float b, float t, bool top = false)
        {
            if (top)
            {
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(0, t);
                rt.anchoredPosition = Vector2.zero;
                return;
            }
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        // ───────────────────────────────────────── capture

        [MenuItem("HordeCall/Dev/UI Labs/Capture")]
        public static void Capture()
        {
            var cam = GameObject.Find("LabCamera")?.GetComponent<Camera>();
            if (cam == null) { Debug.LogWarning("[UILabs] open " + ScenePath + " first"); return; }
            Directory.CreateDirectory(OutDir);
            foreach (var d in Object.FindObjectsByType<UILabsDemo>(FindObjectsSortMode.None))
                if (d.TryGetComponent(out Graphic g) && g.material != null) g.material.SetFloat("_Progress", 0.6f);
            foreach (var h in Object.FindObjectsByType<UISpotlightHole>(FindObjectsSortMode.None)) h.SendMessage("LateUpdate");
            const int w = 2560, hgt = 1440;
            var rt = new RenderTexture(w, hgt, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, hgt, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, hgt), 0, 0);
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
    }
}
