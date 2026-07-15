# BillInspector

Complete attribute-based inspector framework for Unity 6. Four pillars:

1. **Inspector Attributes** — 50+ attributes for fields, groups, buttons, validation
2. **Serialization** — Dictionary, HashSet, Tuple support via BillSerializer
3. **Shader Editor** — `CustomEditor "BillShaderGUI"` for URP-quality material inspector
4. **Editor Windows** — Build windows entirely with attributes via BillEditorWindow

## Quick Start

```csharp
using UnityEngine;
using BillInspector;

public class MyComponent : MonoBehaviour
{
    [BillTitle("My Component")]
    [BillRequired]
    public string componentName;

    [BillSlider(0, 100)]
    [BillProgressBar(0, 100, ColorType.Red)]
    public float health = 100f;

    [BillShowIf("@health < 30")]
    [BillInfoBox("Health is critically low!", InfoType.Error)]
    public bool showWarning;

    [BillButton("Heal")]
    void Heal() => health = 100f;
}
```

## Installation

Add to `Packages/manifest.json`:
```json
"com.bill.inspector": "file:../path/to/com.bill.inspector"
```

Or copy `com.bill.inspector/` folder into your project's `Packages/` directory.

## Features

| Category | Count | Highlights |
|----------|-------|-----------|
| Drawer Attributes | 15 | Slider, ProgressBar, Dropdown, EnumToggleButtons, TableList, InlineEditor |
| Group Attributes | 6 | BoxGroup, FoldoutGroup, TabGroup, HorizontalGroup, VerticalGroup, ToggleGroup |
| Meta Attributes | 12 | ShowIf, HideIf, EnableIf, DisableIf, ReadOnly, Required, ValidateInput, InfoBox |
| Display Attributes | 8 | Title, LabelText, Suffix, PropertyOrder, ShowInInspector, GUIColor, Indent |
| Button Attributes | 3 | Button (with params), ButtonGroup, ShowResultAs |
| Validation | 5 | Required, ValidateInput, FileExists, RangeValidation, NotEmpty, BillValidate |
| Serialization | 3 | BillSerialize, BillSerializedMonoBehaviour, BillSerializedScriptableObject |
| Shader Editor | 1 | BillShaderGUI with naming conventions + full override API |
| Editor Windows | 1 | BillEditorWindow base class |

## Expression System

Prefix `@` for C# expressions, `$` for field interpolation:

```csharp
[BillShowIf("@level >= 5 && !isDead")]
[BillLabelText("@$\"HP ({health:F0}/{maxHealth:F0})\"")]
[BillGUIColor("@health < 30 ? Color.red : Color.white")]
```

## Shader Editor

```hlsl
CustomEditor "BillShaderGUI"  // One line in your shader
```

Convention: `_H_SectionName` = header, `[Toggle] _UseProp` = toggle + keyword, texture + color = single line.

## Validation

```csharp
[MenuItem("Tools/BillInspector/Validate Scene")]
static void Run() => BillValidator.ValidateScene();
```

Or use `Tools > BillInspector > Validation Window` for interactive validation.
