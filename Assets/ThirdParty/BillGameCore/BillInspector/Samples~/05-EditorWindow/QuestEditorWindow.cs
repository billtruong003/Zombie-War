using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BillInspector;
using BillInspector.Editor;

/// <summary>
/// Example: Full editor window built entirely with attributes.
/// Open via Tools > BillInspector > Quest Editor Sample
/// </summary>
public class QuestEditorWindow : BillEditorWindow
{
    [MenuItem("Tools/BillInspector/Samples/Quest Editor")]
    static void Open() => GetWindow<QuestEditorWindow>("Quest Editor");

    [BillTitle("Quest Editor", "Manage game quests")]

    // ── Configuration ──
    [BillBoxGroup("Config")]
    [BillRequired("Need a quest database path")]
    public string databasePath = "Assets/Quests";

    [BillBoxGroup("Config")]
    public enum ViewMode { List, Grid, Table }

    [BillBoxGroup("Config")]
    [BillEnumToggleButtons]
    public ViewMode viewMode = ViewMode.List;

    // ── Quest data ──
    [BillFoldoutGroup("Current Quest")]
    [BillRequired]
    public string questName = "New Quest";

    [BillFoldoutGroup("Current Quest")]
    [BillSlider(1, 50)]
    [BillLabelText("Difficulty")]
    public int difficulty = 5;

    [BillFoldoutGroup("Current Quest")]
    [BillSlider(0, 10000)]
    public int goldReward = 100;

    [BillFoldoutGroup("Current Quest")]
    public bool isRepeatable;

    [BillFoldoutGroup("Current Quest")]
    [BillShowIf("isRepeatable")]
    [BillSlider(60, 86400)]
    [BillSuffix("seconds")]
    public float cooldown = 3600f;

    // ── Stats (read-only) ──
    [BillBoxGroup("Stats")]
    [BillReadOnly]
    public int totalQuests;

    [BillBoxGroup("Stats")]
    [BillReadOnly]
    public string lastSaved = "Never";

    // ── Actions ──
    [BillButtonGroup("Actions")]
    [BillButton("Save Quest", ButtonSize.Medium)]
    void SaveQuest()
    {
        totalQuests++;
        lastSaved = System.DateTime.Now.ToString("HH:mm:ss");
        Debug.Log($"Saved quest '{questName}' (difficulty {difficulty}, gold {goldReward})");
    }

    [BillButtonGroup("Actions")]
    [BillButton("New Quest", ButtonSize.Medium)]
    void NewQuest()
    {
        questName = "Untitled Quest";
        difficulty = 5;
        goldReward = 100;
        isRepeatable = false;
    }

    [BillButton("Validate All Quests")]
    void ValidateAll()
    {
        Debug.Log($"Validating {totalQuests} quests in '{databasePath}'...");
    }
}
