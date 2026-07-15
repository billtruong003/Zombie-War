using UnityEngine;
using BillInspector;

/// <summary>
/// Demo component showing core BillInspector attributes.
/// Attach to any GameObject to see the custom inspector.
/// </summary>
public class BillInspectorDemo : MonoBehaviour
{
    [BillTitle("Character Settings", "Basic demo of BillInspector attributes")]

    [BillRequired("Character must have a name!")]
    public string characterName = "Hero";

    [BillProgressBar(0, 100, ColorType.Red)]
    public float health = 75f;

    [BillProgressBar(0, 50, ColorType.Blue)]
    public float mana = 30f;

    [BillSlider(1f, 20f)]
    [BillLabelText("Move Speed")]
    public float speed = 5f;

    // ── Conditional fields ──
    public enum CharacterClass { Warrior, Mage, Archer }

    [BillEnumToggleButtons]
    public CharacterClass characterClass;

    [BillShowIf("characterClass", CharacterClass.Warrior)]
    [BillBoxGroup("Warrior Skills")]
    [BillSlider(1f, 100f)]
    public float attackPower = 25f;

    [BillShowIf("characterClass", CharacterClass.Warrior)]
    [BillBoxGroup("Warrior Skills")]
    [BillSlider(0f, 1f)]
    public float blockChance = 0.3f;

    [BillShowIf("characterClass", CharacterClass.Mage)]
    [BillBoxGroup("Mage Skills")]
    [BillSlider(1f, 50f)]
    public float spellPower = 15f;

    [BillShowIf("characterClass", CharacterClass.Mage)]
    [BillBoxGroup("Mage Skills")]
    [BillColorPalette]
    public Color spellColor = Color.cyan;

    [BillShowIf("characterClass", CharacterClass.Archer)]
    [BillBoxGroup("Archer Skills")]
    [BillSlider(10f, 200f)]
    public float range = 50f;

    // ── Read-only debug info ──
    [BillFoldoutGroup("Debug Info")]
    [BillReadOnly]
    [BillShowInInspector]
    public Vector3 Position => transform.position;

    [BillFoldoutGroup("Debug Info")]
    [BillReadOnly]
    public int frameCount;

    // ── Buttons ──
    [BillButton("Heal to Full", ButtonSize.Medium)]
    void HealToFull() => health = 100f;

    [BillButton("Take Damage")]
    void TakeDamage() => health = Mathf.Max(0, health - 10f);

    [BillButtonGroup("Quick Actions")]
    [BillButton("Reset Stats", ButtonSize.Small)]
    void ResetStats()
    {
        health = 100f;
        mana = 50f;
        speed = 5f;
    }

    [BillButtonGroup("Quick Actions")]
    [BillButton("Randomize", ButtonSize.Small)]
    void Randomize()
    {
        health = Random.Range(0f, 100f);
        mana = Random.Range(0f, 50f);
        speed = Random.Range(1f, 20f);
    }

    void Update()
    {
        frameCount = Time.frameCount;
    }
}
