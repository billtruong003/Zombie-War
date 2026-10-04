using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar
{
    /// <summary>
    /// G12.8 (owner-approved 04/10): the level-up and chest overlays used to look their widgets up by
    /// path every time they opened ("Perk0/Name", "Card/Recipe/A/Art"…), so renaming a node in the HUD
    /// prefab silently blanked a label. The widgets are now serialized references, wired once from
    /// those same paths (<see cref="EditorWireViews"/>, menu HordeCall/UI/Wire Run Overlay Views), and
    /// a test fails when any of them is missing.
    /// </summary>
    public partial class RunOverlays
    {
        [Serializable]
        public sealed class SkillTileView
        {
            [Tooltip("The tile's own image (tinted by layer).")] public Image frame;
            public Image art;
            public TMP_Text badge;
            [Tooltip("Build strip only.")] public TMP_Text rank;
        }

        [Serializable]
        public sealed class OfferCardView
        {
            public RectTransform root;
            public TMP_Text title, desc;
            public Image border, bg;
            public SkillTileView icon = new();
            public GameObject pipsRoot;
            public Image[] pips = new Image[0];
        }

        [Serializable]
        public sealed class LevelUpView
        {
            public TMP_Text sub, hint;
            public Graphic dim;
            public Transform title;
            public OfferCardView[] cards = new OfferCardView[0];
            public GameObject build;
            public TMP_Text buildCaption;
            public SkillTileView[] buildSlots = new SkillTileView[0];
            [Tooltip("v2 coach widgets kept hidden (the radio card marks the suggested card).")]
            public GameObject[] legacyWidgets = new GameObject[0];
        }

        [Serializable]
        public sealed class ChestView
        {
            public GameObject root;
            public Button claim;
            public Graphic dim;
            public Transform chest, card;
            public Image tagBg, frame;
            public TMP_Text tag, title, desc, req, hint;
            public SkillTileView icon = new();
            public GameObject recipe;
            public SkillTileView recipeA = new(), recipeB = new(), recipeEvo = new();
            [Tooltip("v2 widget kept hidden.")] public GameObject ftueEvo;
        }

        [Header("Wired views (G12.8)")]
        [SerializeField] private LevelUpView levelUp = new();
        [SerializeField] private ChestView chest = new();
        [Tooltip("The HUD skill bar, hidden while the level-up sheet shows the build.")]
        [SerializeField] private GameObject skillBar;

#if UNITY_EDITOR
        static readonly string[] LegacyCardWidgetNames = { "FtueRing", "FtueTag", "FtueCoach" };

        /// Fills the views from the prefab's node paths (the paths the overlays used at run time).
        /// Returns the paths that were not found, empty when everything is wired.
        public System.Collections.Generic.List<string> EditorWireViews()
        {
            var missing = new System.Collections.Generic.List<string>();
            T Get<T>(Transform root, string path) where T : Component
            {
                var t = root != null ? (string.IsNullOrEmpty(path) ? root : root.Find(path)) : null;
                var c = t != null ? t.GetComponent<T>() : null;
                if (c == null) missing.Add((root != null ? root.name : "?") + "/" + path + " (" + typeof(T).Name + ")");
                return c;
            }
            Transform Node(Transform root, string path)
            {
                var t = root != null ? root.Find(path) : null;
                if (t == null) missing.Add((root != null ? root.name : "?") + "/" + path);
                return t;
            }
            SkillTileView Tile(Transform t, bool withRank) => new()
            {
                frame = Get<Image>(t, ""), art = Get<Image>(t, "Art"), badge = Get<TMP_Text>(t, "Badge"),
                rank = withRank ? Get<TMP_Text>(t, "Rank/Label") : null,
            };

            var lu = levelUpRoot != null ? levelUpRoot.transform : null;
            levelUp = new LevelUpView
            {
                sub = Get<TMP_Text>(lu, "Sub/Label"),
                hint = Get<TMP_Text>(lu, "Hint"),
                dim = Get<Graphic>(lu, "Dim"),
                title = Node(lu, "Title"),
                build = Node(lu, "Build")?.gameObject,
                buildCaption = Get<TMP_Text>(lu, "Build/Caption/Label"),
            };
            var cards = new System.Collections.Generic.List<OfferCardView>();
            for (int i = 0; lu != null && lu.Find($"Perk{i}") != null; i++)
            {
                var c = (RectTransform)lu.Find($"Perk{i}");
                var pipsRoot = Node(c, "Pips");
                var pips = new System.Collections.Generic.List<Image>();
                if (pipsRoot != null) for (int k = 0; k < pipsRoot.childCount; k++) pips.Add(Get<Image>(pipsRoot.GetChild(k), ""));
                cards.Add(new OfferCardView
                {
                    root = c, title = Get<TMP_Text>(c, "Name"), desc = Get<TMP_Text>(c, "Desc"),
                    border = Get<Image>(c, "Border"), bg = Get<Image>(c, "Bg"),
                    icon = Tile(Node(c, "Icon"), false), pipsRoot = pipsRoot?.gameObject, pips = pips.ToArray(),
                });
            }
            if (cards.Count == 0) missing.Add("LevelUp/Perk0");
            levelUp.cards = cards.ToArray();
            var items = lu != null ? lu.Find("Build/Items") : null;
            int slotCount = Skills.SkillCatalogDefs.MaxSkillSlots + Skills.SkillCatalogDefs.MaxStatSlots;
            levelUp.buildSlots = new SkillTileView[slotCount];
            for (int k = 0; k < slotCount; k++) levelUp.buildSlots[k] = Tile(Node(items, "Item" + k), true);
            var legacy = new System.Collections.Generic.List<GameObject>();
            foreach (var n in LegacyCardWidgetNames) { var t = lu != null ? lu.Find(n) : null; if (t != null) legacy.Add(t.gameObject); }
            levelUp.legacyWidgets = legacy.ToArray();

            var ct = lu != null && lu.parent != null ? lu.parent.Find("ChestOverlay") : null;
            if (ct == null) missing.Add("ChestOverlay");
            var recipe = ct != null ? ct.Find("Card/Recipe") : null;
            chest = new ChestView
            {
                root = ct != null ? ct.gameObject : null,
                claim = Get<Button>(ct, "Claim"),
                dim = Get<Graphic>(ct, "Dim"),
                chest = Node(ct, "Chest"), card = Node(ct, "Card"),
                tagBg = Get<Image>(ct, "Card/Tag"), frame = Get<Image>(ct, "Card/Frame"),
                tag = Get<TMP_Text>(ct, "Card/Tag/Label"), title = Get<TMP_Text>(ct, "Card/Name"),
                desc = Get<TMP_Text>(ct, "Card/Desc"), req = Get<TMP_Text>(ct, "Card/Req"), hint = Get<TMP_Text>(ct, "Hint"),
                icon = Tile(Node(ct, "Card/Icon"), false),
                recipe = recipe != null ? recipe.gameObject : null,
                recipeA = Tile(Node(recipe, "A"), false), recipeB = Tile(Node(recipe, "B"), false), recipeEvo = Tile(Node(recipe, "Evo"), false),
                ftueEvo = ct != null && ct.Find("Card/FtueEvo") != null ? ct.Find("Card/FtueEvo").gameObject : null,
            };
            skillBar = Node(transform, "Safe/SkillBar")?.gameObject;
            return missing;
        }

        public System.Collections.Generic.List<string> EditorWire() => EditorWireViews();

        /// The required view references that are null (empty when the prefab is fully wired).
        public System.Collections.Generic.List<string> EditorUnwired()
        {
            var n = new System.Collections.Generic.List<string>();
            void Need(string name, UnityEngine.Object v) { if (v == null) n.Add(name); }
            void Tile(string name, SkillTileView t, bool rank)
            {
                if (t == null) { n.Add(name); return; }
                Need(name + ".frame", t.frame); Need(name + ".art", t.art); Need(name + ".badge", t.badge);
                if (rank) Need(name + ".rank", t.rank);
            }
            Need("levelUp.sub", levelUp.sub); Need("levelUp.hint", levelUp.hint); Need("levelUp.dim", levelUp.dim);
            Need("levelUp.title", levelUp.title); Need("levelUp.build", levelUp.build); Need("levelUp.buildCaption", levelUp.buildCaption);
            if (levelUp.cards == null || levelUp.cards.Length < 3) n.Add("levelUp.cards (3)");
            else for (int i = 0; i < levelUp.cards.Length; i++)
            {
                var c = levelUp.cards[i];
                Need($"cards[{i}].root", c.root); Need($"cards[{i}].title", c.title); Need($"cards[{i}].desc", c.desc);
                Need($"cards[{i}].border", c.border); Need($"cards[{i}].bg", c.bg); Need($"cards[{i}].pipsRoot", c.pipsRoot);
                Tile($"cards[{i}].icon", c.icon, false);
            }
            int slots = Skills.SkillCatalogDefs.MaxSkillSlots + Skills.SkillCatalogDefs.MaxStatSlots;
            if (levelUp.buildSlots == null || levelUp.buildSlots.Length != slots) n.Add($"levelUp.buildSlots ({slots})");
            else for (int k = 0; k < slots; k++) Tile($"buildSlots[{k}]", levelUp.buildSlots[k], true);
            Need("chest.root", chest.root); Need("chest.claim", chest.claim); Need("chest.dim", chest.dim);
            Need("chest.chest", chest.chest); Need("chest.card", chest.card); Need("chest.tagBg", chest.tagBg);
            Need("chest.frame", chest.frame); Need("chest.tag", chest.tag); Need("chest.title", chest.title);
            Need("chest.desc", chest.desc); Need("chest.req", chest.req); Need("chest.hint", chest.hint);
            Need("chest.recipe", chest.recipe);
            Tile("chest.icon", chest.icon, false); Tile("chest.recipeA", chest.recipeA, false);
            Tile("chest.recipeB", chest.recipeB, false); Tile("chest.recipeEvo", chest.recipeEvo, false);
            Need("skillBar", skillBar);
            return n;
        }
#endif
    }
}
