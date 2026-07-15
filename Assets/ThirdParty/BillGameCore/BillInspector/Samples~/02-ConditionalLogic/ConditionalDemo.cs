using UnityEngine;
using BillInspector;

public class ConditionalDemo : MonoBehaviour
{
    [BillTitle("Conditional Logic Demo")]

    public bool showAdvanced;

    [BillShowIf("showAdvanced")]
    [BillInfoBox("These fields only appear when Show Advanced is checked.", InfoType.Info)]
    [BillSlider(0f, 100f)]
    public float advancedParam1 = 50f;

    [BillShowIf("showAdvanced")]
    public string advancedNote = "Edit me";

    public enum Mode { Simple, Intermediate, Expert }

    [BillEnumToggleButtons]
    public Mode currentMode = Mode.Simple;

    [BillShowIf("currentMode", Mode.Expert)]
    [BillInfoBox("Expert-only settings", InfoType.Warning)]
    public float dangerousParam = 0f;

    [BillReadOnly]
    public string readOnlyField = "Cannot edit this";

    [BillDisableIf("showAdvanced")]
    public string disabledWhenAdvanced = "Disabled when advanced is on";

    [BillRequired("Player name is required!")]
    public string playerName;

    [BillValidateInput("ValidateAge", "Age must be 1-150")]
    public int playerAge = 25;

    bool ValidateAge(int value) => value >= 1 && value <= 150;
}
