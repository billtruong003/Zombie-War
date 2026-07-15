using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BillInspector;

namespace BillInspector.Editor
{
    /// <summary>
    /// Interactive showcase window: live code + rendered inspector side by side.
    /// Opens from Tools > BillInspector > Showcase.
    /// </summary>
    public class BillInspectorShowcase : EditorWindow
    {
        private int _selectedCategory;
        private Vector2 _scrollPos;
        private readonly Dictionary<Type, (ScriptableObject instance, UnityEditor.Editor editor)> _demos = new();

        private static readonly string[] Categories =
        {
            "Display", "Drawers", "Groups", "Conditions", "Buttons", "Validation"
        };

        [MenuItem("Tools/BillInspector/Showcase %#b")]
        public static void Open()
        {
            var w = GetWindow<BillInspectorShowcase>("BillInspector Showcase");
            w.minSize = new Vector2(720, 480);
        }

        private void OnEnable()
        {
            // Pre-create demo instances
            EnsureDemo<DisplayDemo>();
            EnsureDemo<DrawerDemo>();
            EnsureDemo<GroupDemo>();
            EnsureDemo<ConditionDemo>();
            EnsureDemo<ButtonDemo>();
            EnsureDemo<ValidationDemo>();
        }

        private void OnDisable()
        {
            foreach (var kvp in _demos)
            {
                if (kvp.Value.editor != null)
                    DestroyImmediate(kvp.Value.editor);
                if (kvp.Value.instance != null)
                    DestroyImmediate(kvp.Value.instance);
            }
            _demos.Clear();
        }

        private void EnsureDemo<T>() where T : ScriptableObject
        {
            if (_demos.ContainsKey(typeof(T))) return;
            var instance = CreateInstance<T>();
            instance.hideFlags = HideFlags.HideAndDontSave;
            var editor = UnityEditor.Editor.CreateEditor(instance);
            _demos[typeof(T)] = (instance, editor);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // ── Sidebar ──
            EditorGUILayout.BeginVertical(GUILayout.Width(140));
            DrawSidebar();
            EditorGUILayout.EndVertical();

            // Separator
            var sep = EditorGUILayout.GetControlRect(false, GUILayout.Width(1));
            EditorGUI.DrawRect(sep, new Color(0.2f, 0.2f, 0.2f, 0.6f));

            // ── Main content ──
            EditorGUILayout.BeginVertical();
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_selectedCategory)
            {
                case 0: DrawCategory<DisplayDemo>(DisplayDemo.Code); break;
                case 1: DrawCategory<DrawerDemo>(DrawerDemo.Code); break;
                case 2: DrawCategory<GroupDemo>(GroupDemo.Code); break;
                case 3: DrawCategory<ConditionDemo>(ConditionDemo.Code); break;
                case 4: DrawCategory<ButtonDemo>(ButtonDemo.Code); break;
                case 5: DrawCategory<ValidationDemo>(ValidationDemo.Code); break;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSidebar()
        {
            GUILayout.Space(8);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("BillInspector", titleStyle);
            EditorGUILayout.LabelField("Showcase", new GUIStyle(EditorStyles.centeredGreyMiniLabel));
            GUILayout.Space(8);

            var lineRect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(lineRect, new Color(0.3f, 0.3f, 0.3f, 0.4f));
            GUILayout.Space(4);

            for (int i = 0; i < Categories.Length; i++)
            {
                var style = new GUIStyle(i == _selectedCategory ? "SelectionRect" : EditorStyles.label)
                {
                    padding = new RectOffset(12, 4, 6, 6),
                    fontStyle = i == _selectedCategory ? FontStyle.Bold : FontStyle.Normal,
                    fontSize = 12
                };

                if (GUILayout.Button(Categories[i], style))
                    _selectedCategory = i;
            }

            GUILayout.FlexibleSpace();

            var versionStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField("v0.4.0", versionStyle);
            GUILayout.Space(4);
        }

        private void DrawCategory<T>(string code) where T : ScriptableObject
        {
            // Section: Code
            DrawCodeBlock(code);

            GUILayout.Space(8);

            // Section: Live Preview
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField("Live Preview", headerStyle);
            var previewLine = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(previewLine, new Color(0.24f, 0.49f, 0.91f, 0.6f));
            GUILayout.Space(4);

            if (_demos.TryGetValue(typeof(T), out var demo) && demo.editor != null)
            {
                demo.editor.OnInspectorGUI();
            }

            GUILayout.Space(16);
        }

        private void DrawCodeBlock(string code)
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField("Code", headerStyle);
            var codeLine = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(codeLine, new Color(0.24f, 0.49f, 0.91f, 0.6f));
            GUILayout.Space(4);

            var bgRect = EditorGUILayout.GetControlRect(false, 0);

            var codeStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = false,
                font = GetMonoFont(),
                fontSize = 11,
                padding = new RectOffset(12, 12, 8, 8),
                normal = { textColor = new Color(0.78f, 0.82f, 0.87f) }
            };

            var content = new GUIContent(code);
            float height = codeStyle.CalcHeight(content, EditorGUIUtility.currentViewWidth - 170);
            height = Mathf.Max(height, 40);

            var rect = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.14f));

            // Border
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), new Color(0.2f, 0.2f, 0.25f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), new Color(0.2f, 0.2f, 0.25f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), new Color(0.2f, 0.2f, 0.25f));
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), new Color(0.2f, 0.2f, 0.25f));

            EditorGUI.LabelField(rect, code, codeStyle);
        }

        private static Font s_monoFont;
        private static Font GetMonoFont()
        {
            if (s_monoFont == null)
                s_monoFont = Font.CreateDynamicFontFromOSFont("Consolas", 11);
            return s_monoFont;
        }

        // ═══════════════════════════════════════════════════════════
        // Demo ScriptableObjects — each contains Bill attributes
        // ═══════════════════════════════════════════════════════════

        // ── Display ──
        private class DisplayDemo : ScriptableObject
        {
            [BillTitle("Player Settings", "Configure the player character")]
            [BillInfoBox("These settings affect gameplay balance.", InfoType.Info)]
            public string playerName = "Hero";

            [BillLabelText("Display Name")]
            public string nickname = "The Brave";

            [BillSuffix("km/h")]
            public float moveSpeed = 5.5f;

            [BillSuffix("%")]
            [BillSlider(0, 100)]
            public int critChance = 15;

            [BillReadOnly]
            public string buildVersion = "0.4.0";

            [BillIndent(2)]
            public string indentedField = "Indented by 2 levels";

            public const string Code =
@"[BillTitle(""Player Settings"", ""Configure the player character"")]
[BillInfoBox(""These settings affect gameplay balance."", InfoType.Info)]
public string playerName = ""Hero"";

[BillLabelText(""Display Name"")]
public string nickname = ""The Brave"";

[BillSuffix(""km/h"")]
public float moveSpeed = 5.5f;

[BillSuffix(""%"")]
[BillSlider(0, 100)]
public int critChance = 15;

[BillReadOnly]
public string buildVersion = ""0.4.0"";

[BillIndent(2)]
public string indentedField = ""Indented by 2 levels"";";
        }

        // ── Drawers ──
        private class DrawerDemo : ScriptableObject
        {
            [BillTitle("Drawer Attributes")]
            [BillSlider(0, 100)]
            public float health = 75f;

            [BillProgressBar(0, 100, ColorType.Green)]
            public float xp = 42f;

            [BillMinMaxSlider(0, 100)]
            public Vector2 damageRange = new(10, 50);

            [BillEnumToggleButtons]
            public WeaponType weapon = WeaponType.Sword;

            [BillColorPalette]
            public Color teamColor = Color.red;

            [BillDropdown("GetWeaponNames")]
            public string selectedWeapon = "Sword";

            [BillResizableTextArea]
            public string description = "A mighty warrior.";

            private List<string> GetWeaponNames()
                => new() { "Sword", "Axe", "Bow", "Staff", "Dagger" };

            public enum WeaponType { Sword, Axe, Bow, Staff, Dagger }

            public const string Code =
@"[BillSlider(0, 100)]
public float health = 75f;

[BillProgressBar(0, 100, ColorType.Green)]
public float xp = 42f;

[BillMinMaxSlider(0, 100)]
public Vector2 damageRange = new(10, 50);

[BillEnumToggleButtons]
public WeaponType weapon = WeaponType.Sword;

[BillColorPalette]
public Color teamColor = Color.red;

[BillDropdown(""GetWeaponNames"")]
public string selectedWeapon = ""Sword"";

[BillResizableTextArea]
public string description = ""A mighty warrior."";

List<string> GetWeaponNames()
    => new() { ""Sword"", ""Axe"", ""Bow"", ""Staff"", ""Dagger"" };";
        }

        // ── Groups ──
        private class GroupDemo : ScriptableObject
        {
            [BillTitle("Group Attributes")]

            [BillBoxGroup("Stats")]
            public int strength = 10;
            [BillBoxGroup("Stats")]
            public int agility = 8;
            [BillBoxGroup("Stats")]
            public int intelligence = 12;

            [BillFoldoutGroup("Advanced")]
            public float critMultiplier = 1.5f;
            [BillFoldoutGroup("Advanced")]
            public float dodgeChance = 0.1f;

            [BillHorizontalGroup("Position")]
            public float posX = 0f;
            [BillHorizontalGroup("Position")]
            public float posY = 0f;
            [BillHorizontalGroup("Position")]
            public float posZ = 0f;

            [BillTabGroup("tabs", "Combat")]
            public int attackPower = 25;
            [BillTabGroup("tabs", "Combat")]
            public int defense = 15;
            [BillTabGroup("tabs", "Magic")]
            public int mana = 100;
            [BillTabGroup("tabs", "Magic")]
            public int spellPower = 30;

            public const string Code =
@"[BillBoxGroup(""Stats"")]
public int strength = 10;
[BillBoxGroup(""Stats"")]
public int agility = 8;
[BillBoxGroup(""Stats"")]
public int intelligence = 12;

[BillFoldoutGroup(""Advanced"")]
public float critMultiplier = 1.5f;
[BillFoldoutGroup(""Advanced"")]
public float dodgeChance = 0.1f;

[BillHorizontalGroup(""Position"")]
public float posX, posY, posZ;

[BillTabGroup(""tabs"", ""Combat"")]
public int attackPower = 25;
[BillTabGroup(""tabs"", ""Combat"")]
public int defense = 15;
[BillTabGroup(""tabs"", ""Magic"")]
public int mana = 100;
[BillTabGroup(""tabs"", ""Magic"")]
public int spellPower = 30;";
        }

        // ── Conditions ──
        private class ConditionDemo : ScriptableObject
        {
            [BillTitle("Conditional Attributes")]
            [BillInfoBox("Toggle 'Enable Magic' to see conditional fields appear/disappear.")]
            public bool enableMagic;

            [BillShowIf("enableMagic")]
            [BillSlider(0, 200)]
            public int manaPool = 100;

            [BillShowIf("enableMagic")]
            [BillInfoBox("Magic is enabled! Configure your spells below.", InfoType.Info)]
            public string spellName = "Fireball";

            [BillShowIf("enableMagic")]
            [BillSlider(1, 10)]
            public int spellLevel = 1;

            [Space]
            public bool isInCombat;

            [BillEnableIf("isInCombat")]
            [BillInfoBox("Only editable during combat.", InfoType.Warning)]
            public int combatBonus = 5;

            [BillDisableIf("isInCombat")]
            public string peacefulAction = "Rest";

            public const string Code =
@"public bool enableMagic;

[BillShowIf(""enableMagic"")]
[BillSlider(0, 200)]
public int manaPool = 100;

[BillShowIf(""enableMagic"")]
[BillInfoBox(""Magic is enabled!"", InfoType.Info)]
public string spellName = ""Fireball"";

[Space]
public bool isInCombat;

[BillEnableIf(""isInCombat"")]
[BillInfoBox(""Only editable during combat."", InfoType.Warning)]
public int combatBonus = 5;

[BillDisableIf(""isInCombat"")]
public string peacefulAction = ""Rest"";";
        }

        // ── Buttons ──
        private class ButtonDemo : ScriptableObject
        {
            [BillTitle("Button Attributes")]
            [BillInfoBox("Methods become clickable buttons in the inspector.")]
            public string lastAction = "(none)";

            [BillSlider(1, 100)]
            public int healAmount = 25;

            [BillButton("Heal Player")]
            private void HealPlayer()
            {
                lastAction = $"Healed for {healAmount} HP";
                Debug.Log(lastAction);
            }

            [BillButton("Full Restore", ButtonSize.Large)]
            private void FullRestore()
            {
                healAmount = 100;
                lastAction = "Full restore!";
                Debug.Log(lastAction);
            }

            [BillButtonGroup("Actions")]
            [BillButton("Attack")]
            private void Attack()
            {
                lastAction = "Attacked!";
                Debug.Log(lastAction);
            }

            [BillButtonGroup("Actions")]
            [BillButton("Defend")]
            private void Defend()
            {
                lastAction = "Defending!";
                Debug.Log(lastAction);
            }

            [BillButtonGroup("Actions")]
            [BillButton("Flee")]
            private void Flee()
            {
                lastAction = "Fled!";
                Debug.Log(lastAction);
            }

            public const string Code =
@"public string lastAction = ""(none)"";

[BillSlider(1, 100)]
public int healAmount = 25;

[BillButton(""Heal Player"")]
void HealPlayer()
{
    lastAction = $""Healed for {healAmount} HP"";
}

[BillButton(""Full Restore"", ButtonSize.Large)]
void FullRestore()
{
    healAmount = 100;
    lastAction = ""Full restore!"";
}

[BillButtonGroup(""Actions"")]
[BillButton(""Attack"")]
void Attack() => lastAction = ""Attacked!"";

[BillButtonGroup(""Actions"")]
[BillButton(""Defend"")]
void Defend() => lastAction = ""Defending!"";

[BillButtonGroup(""Actions"")]
[BillButton(""Flee"")]
void Flee() => lastAction = ""Fled!"";";
        }

        // ── Validation ──
        private class ValidationDemo : ScriptableObject
        {
            [BillTitle("Validation Attributes")]
            [BillInfoBox("Leave fields empty or invalid to see validation in action.", InfoType.Warning)]

            [BillRequired("Player name is required!")]
            public string playerName;

            [BillRequired("Weapon must be assigned!")]
            public GameObject weapon;

            [BillSlider(0, 200)]
            [BillValidateInput("IsValidHP", "HP must be between 1 and 100")]
            public int hp = 50;

            [BillSlider(0, 100)]
            [BillValidateInput("IsValidLevel", "Level must be at least 1")]
            public int level = 0;

            private bool IsValidHP(int value) => value >= 1 && value <= 100;
            private bool IsValidLevel(int value) => value >= 1;

            public const string Code =
@"[BillRequired(""Player name is required!"")]
public string playerName;

[BillRequired(""Weapon must be assigned!"")]
public GameObject weapon;

[BillSlider(0, 200)]
[BillValidateInput(""IsValidHP"", ""HP must be between 1 and 100"")]
public int hp = 50;

[BillSlider(0, 100)]
[BillValidateInput(""IsValidLevel"", ""Level must be at least 1"")]
public int level = 0;

bool IsValidHP(int value) => value >= 1 && value <= 100;
bool IsValidLevel(int value) => value >= 1;";
        }
    }
}
