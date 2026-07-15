using UnityEngine;
using BillInspector;
using System.Collections.Generic;

/// <summary>
/// Demo: Validation attributes and custom validation methods.
/// </summary>
public class ValidationDemo : MonoBehaviour
{
    [BillTitle("Validation Demo", "All validation features")]

    // ── Required field ──
    [BillRequired("Character name is mandatory!")]
    public string characterName;

    // ── Range validation ──
    [BillRangeValidation(1, 100, Message = "Health must be 1-100")]
    [BillSlider(0, 150)]
    public float health = 80f;

    // ── Custom validation via method ──
    [BillValidateInput("ValidateSpeed", "Speed must be positive and < 100")]
    [BillSlider(0, 200)]
    public float speed = 10f;

    bool ValidateSpeed(float value) => value > 0 && value < 100;

    // ── Expression validation ──
    [BillValidateInput("@minDamage <= maxDamage", "Min must be ≤ Max")]
    [BillSlider(0, 50)]
    public float minDamage = 5f;

    [BillSlider(0, 100)]
    public float maxDamage = 20f;

    // ── Not empty collection ──
    [BillNotEmpty(Message = "Must have at least one weapon")]
    public List<string> weapons = new() { "Sword" };

    // ── File exists ──
    [BillFileExists(Extension = ".json", Message = "Config file not found")]
    [BillFilePath(ParentFolder = "Assets")]
    public string configPath;

    // ── Required object reference ──
    [BillRequired("Must assign a spawn point!")]
    [BillSceneObjectsOnly]
    public Transform spawnPoint;

    // ── Self-validation method ──
    [BillValidate]
    void CustomValidation(ValidationResultList results)
    {
        if (health < 10 && speed > 50)
            results.AddWarning("Low health + high speed is dangerous!", "health");

        if (string.IsNullOrEmpty(characterName) && weapons.Count == 0)
            results.AddError("Must have a name or at least one weapon!");
    }

    // ── Buttons ──
    [BillButton("Run Validation")]
    void RunValidation()
    {
        var results = BillInspector.Editor.BillValidator.Validate(this);
        BillInspector.Editor.BillValidator.LogResults(results);
    }
}
