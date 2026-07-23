using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using K = ZombieWar.Editor.UI.UIKit;
using A = ZombieWar.Editor.UI.UIKit.Anch;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// Additive visual skin backed by GUI Pro-SuperCasual assets.
    /// Keeps vendor assets read-only and avoids changing UIKit primitives globally.
    /// </summary>
    public static class SuperCasualSkin
    {
        const string Root = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites";
        static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite Sprite(string relativePath)
        {
            if (Cache.TryGetValue(relativePath, out var cached) && cached != null) return cached;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/{relativePath}");
            if (sprite == null) Debug.LogWarning($"[SuperCasualSkin] Missing sprite: {Root}/{relativePath}");
            Cache[relativePath] = sprite;
            return sprite;
        }

        public static Image Image(RectTransform parent, string name, string relativePath, Color color, bool raycast = true)
            => K.Image(parent, name, Sprite(relativePath), color, raycast);

        public static Button Button(RectTransform parent, string name, string label, Vector2 size, A anchor,
            Vector2 position, string colorName, Color textColor, float fontSize)
        {
            var spriteName = size.x >= 420f ? $"Button01_l_{colorName}.png" : $"Button01_s_{colorName}.png";
            var image = Image(parent, name, $"Components/Button/{spriteName}", Color.white);
            K.Place(image.rectTransform, anchor, position, size);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.45f, 0.47f, 0.52f, 0.8f);
            button.colors = colors;
            image.gameObject.AddComponent<UIFxPress>();

            var text = K.Text(image.rectTransform, "Label", label, fontSize, textColor,
                FontStyles.Bold, TextAlignmentOptions.Center);
            text.outlineWidth = 0.14f;
            text.outlineColor = new Color32(10, 16, 29, 230);
            K.Full(text.rectTransform, 24, 8, 24, 14);
            return button;
        }

        public static TMP_Text ResourcePill(RectTransform parent, string name, A anchor, Vector2 position,
            float width, string iconName)
        {
            var pill = Image(parent, name, "Components/UI_Etc/ResourceBar_Demo_Bg.png", Color.white);
            K.Place(pill.rectTransform, anchor, position, new Vector2(width, 72));

            var icon = Image(pill.rectTransform, "Icon",
                $"Components/UI_Etc/ResourceBar_Demo_Icon_{iconName}.png", Color.white, false);
            K.Place(icon.rectTransform, A.ML, new Vector2(-2, 0), new Vector2(58, 58));
            icon.preserveAspect = true;

            var value = K.Text(pill.rectTransform, "Value", "0", 31, Color.white,
                FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            value.outlineWidth = 0.14f;
            value.outlineColor = new Color32(10, 16, 29, 235);
            K.Full(value.rectTransform, 56, 4, 18, 8);
            return value;
        }

        /// <summary>
        /// Compact portrait header shared by secondary menu screens.
        /// The product direction intentionally has no settings shortcut here; currency remains visible.
        /// </summary>
        public static Button HeaderWithCurrency(RectTransform parent, string title)
        {
            var back = K.HeaderBack(parent, title, out var titleLabel);
            K.Place(titleLabel.rectTransform, A.TL, new Vector2(144, -38), new Vector2(360, 76));
            titleLabel.fontSize = 42;
            titleLabel.alignment = TextAlignmentOptions.MidlineLeft;

            var coinLabel = ResourcePill(parent, "HeaderCoin", A.TR, new Vector2(-220, -40), 172, "Coin");
            var gemLabel = ResourcePill(parent, "HeaderGem", A.TR, new Vector2(-32, -40), 172, "Gem");
            coinLabel.transform.parent.GetComponent<Image>().color = UITheme.Surface;
            gemLabel.transform.parent.GetComponent<Image>().color = UITheme.Surface;
            var cluster = parent.gameObject.AddComponent<CurrencyClusterWidget>();
            var serialized = new SerializedObject(cluster);
            K.Wire(serialized, "coinLabel", coinLabel);
            K.Wire(serialized, "gemLabel", gemLabel);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return back;
        }

        public static Image ItemFrame(RectTransform parent, string name, Color color, bool raycast = false)
            => Image(parent, name, "Components/Frame/ItemFrame01_Demo_Navy.png", color, raycast);

        public static Image Dock(RectTransform parent, string name)
            => Image(parent, name, "Components/Button/Menu_BottomBtn_Bg.png", Color.white);

        /// <summary>
        /// Conservative post-process for generated menu prefabs. Only known structural roles are skinned;
        /// item thumbnails, runtime references and hierarchy are left untouched.
        /// </summary>
        public static void ApplyMenuScreen(RectTransform root)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                var objectName = image.gameObject.name;
                var parentName = image.transform.parent != null ? image.transform.parent.name : string.Empty;

                if (objectName == "BackBtn")
                {
                    Use(image, "Components/Button/Button_Round02_Blue.png", Color.white);
                    continue;
                }

                if (objectName.StartsWith("Tab", StringComparison.Ordinal) && parentName == "Tabs")
                {
                    Use(image, "Components/Button/Button01_s_White_Bg.png", image.color);
                    var tabLabel = image.GetComponentInChildren<TMP_Text>(true);
                    Outline(tabLabel, 0.11f);
                    continue;
                }

                if (objectName == "Bg" && IsCardContainer(parentName))
                {
                    Use(image, "Components/Frame/ItemFrame01_Demo_Navy.png", Color.white);
                    continue;
                }

                if (objectName is "PriceChip" or "CountChip" or "MaxChip")
                {
                    Use(image, "Components/UI_Etc/ResourceBar_Demo_Bg.png", UITheme.Surface);
                    continue;
                }

                if (objectName == "Owned")
                {
                    Use(image, "Components/Button/Button01_s_Green.png", Color.white);
                    Outline(image.GetComponentInChildren<TMP_Text>(true), 0.1f);
                }
            }

            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var objectName = button.gameObject.name;
                if (objectName == "BackBtn" || objectName.StartsWith("Tab", StringComparison.Ordinal)) continue;

                string colorName = null;
                if (ContainsAny(objectName, "Claim", "Confirm", "UpBtn", "Quay", "Random", "Buy", "Equip"))
                    colorName = "Green";
                else if (ContainsAny(objectName, "Cancel", "Prev", "Next"))
                    colorName = "DarkGray";
                else if (ContainsAny(objectName, "ItemMode", "SetMode"))
                    colorName = "Blue";

                if (colorName == null) continue;
                var target = button.targetGraphic as Image ?? button.GetComponent<Image>();
                if (target == null) continue;
                Use(target, $"Components/Button/Button01_s_{colorName}.png", Color.white);
                var rootImage = button.GetComponent<Image>();
                if (rootImage != null && rootImage != target) rootImage.color = Color.clear;
                Outline(button.GetComponentInChildren<TMP_Text>(true), 0.12f);
            }

            foreach (var title in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (title.gameObject.name == "Title" || title.fontSize >= UITheme.FontHeader)
                    Outline(title, 0.1f);
            }
        }

        static bool IsCardContainer(string name)
            => name.IndexOf("Card", StringComparison.OrdinalIgnoreCase) >= 0
               || name.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0
               || name.EndsWith("Row", StringComparison.Ordinal)
               || name.StartsWith("Up", StringComparison.Ordinal);

        static bool ContainsAny(string value, params string[] tokens)
        {
            foreach (var token in tokens)
                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static void Use(Image image, string relativePath, Color color)
        {
            var sprite = Sprite(relativePath);
            if (sprite == null) return;
            image.sprite = sprite;
            image.color = color;
            image.type = sprite.border.sqrMagnitude > 0f ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        }

        static void Outline(TMP_Text text, float width)
        {
            if (text == null) return;
            text.outlineWidth = width;
            text.outlineColor = new Color32(8, 13, 25, 225);
        }
    }
}
