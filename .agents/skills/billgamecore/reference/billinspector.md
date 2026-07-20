# BillInspector — attribute reference (data authoring)

Source: `Assets/ThirdParty/BillGameCore/BillInspector/` (own asmdef `BillInspector.Runtime`, auto-referenced -> usable from `_Project.Runtime.asmdef`, which references it directly - see `billgamecore` SKILL.md rule #3). Namespace `BillInspector`. An Odin-style inspector, from-scratch (no Sirenix/paid Odin dependency).

## How it hooks in
`BillInspectorEditor` replaces Unity's default inspector **only for types that have ≥1 `Bill…` attribute (or a `[BillButton]` method)**; types without any get the stock inspector. So:
- Put attributes on **any** `MonoBehaviour` or `ScriptableObject` - no base class required.
- Inherit **`BillSerializedMonoBehaviour`** / **`BillSerializedScriptableObject`** *only* when you need Unity to serialize `Dictionary`, `HashSet`, `Tuple`, polymorphic refs, etc.

## Verified signatures (most-used)
```csharp
[BillSlider(float min, float max)]                 // numeric field slider
[BillMinMaxSlider(float minLimit, float maxLimit)] // Vector2: X=min, Y=max (dual handle)
[BillInfoBox(string message, InfoType type = Info)] // box above field/class; .VisibleIf
[BillButton(string label = null, ButtonSize size = Medium)]  // method -> button; .Icon, .EnableIf; params -> fields
[BillTableList]                                     // List/Array as a table (each serializable field = column)
[BillListDrawerSettings]                            // props incl. ShowItemCount, DraggableItems
[BillShowIf(string condition)] / [BillShowIf(condition, object compareValue)]  // .Operator (And/Or), AllowMultiple
```
`InfoType`: `None, Info, Warning, Error` · `ButtonSize`: `Small, Medium, Large` · `ConditionOperator`: `And, Or`.

### Condition expressions (BillShowIf/HideIf/EnableIf/DisableIf)
```csharp
[BillShowIf("isActive")]
[BillShowIf("weaponType", WeaponType.Melee)]
[BillShowIf("@level >= 5 && isReady")]
[BillShowIf("CanShowMethod")]
```

## Full catalog (grouped — see each `*.cs` in `Runtime/Attributes/` for exact params)

**Display**: `BillTitle`, `BillLabelText`, `BillHideLabel`, `BillSuffix`, `BillIndent`, `BillPropertyOrder`, `BillGUIColor`, `BillShowInInspector`, `BillHideInPlayMode`.
**Groups**: `BillBoxGroup`, `BillFoldoutGroup`, `BillTabGroup`, `BillToggleGroup`, `BillHorizontalGroup`, `BillVerticalGroup`.
**Drawers**: `BillSlider`, `BillMinMaxSlider`, `BillProgressBar`, `BillDropdown`, `BillEnumToggleButtons`, `BillColorPalette`, `BillAssetSelector`, `BillFilePath`, `BillInlineEditor`, `BillPreviewField`, `BillResizableTextArea`, `BillSearchable`, `BillListDrawerSettings`, `BillTableList`, `BillTableColumnWidth`, `BillDictionaryDrawer`.
**Meta/validation**: `BillShowIf`, `BillHideIf`, `BillEnableIf`, `BillDisableIf`, `BillReadOnly`, `BillRequired`, `BillInfoBox`, `BillOnValueChanged("Method")`, `BillValidateInput("Method")`, `BillAssetsOnly`, `BillSceneObjectsOnly`.
**Buttons**: `BillButton`, `BillButtonGroup`, `BillShowResultAs`.
**Serialization**: `BillSerializedMonoBehaviour`, `BillSerializedScriptableObject`, `[BillSerialize]`.

## Should you add these to `WeaponData`/`ZombieData` right now?

Not yet, by default. Both are currently plain `[SerializeField]`/public-field ScriptableObjects (see `Weapon.cs`/`ZombieAI.cs`'s existing `WeaponData.cs`/`ZombieData.cs`) - that's the correct amount of ceremony for their current size. Reach for BillInspector attributes when the inspector genuinely needs it: grouping once a data asset grows past ~10 fields (`BillBoxGroup`), a slider range that's easy to fat-finger wrong (`BillSlider`), or a boss's per-phase table (`BillTableList`, once `BossZombieData`/similar exists in Phase 6). Adding attributes reflexively to every field is the over-engineering this project's `expert-developer` skill warns against.

## Example — if/when Zombie War data grows past plain fields

```csharp
using BillInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "ZombieWar/Zombie Data")]
public class ZombieData : ScriptableObject
{
    [BillTitle("Zombie")]
    [BillRequired] public string zombieName;

    [BillBoxGroup("Combat")] [BillSlider(0, 500)] public float maxHealth;
    [BillBoxGroup("Combat")] public float damage;
    [BillBoxGroup("Combat")] [BillSuffix("m")] public float attackRange;

    [BillBoxGroup("Boss")] [BillShowIf("isBoss")] public bool isBoss;
}
```
