# Changelog

## [0.4.0] — Phase 4 — Expression Engine, EditorWindow, Validation

### Expression Engine (full rewrite)
- ExpressionCompiler: compiles @expressions into AST node tree, caches per type+expression
- Supports: field access, chained member access (a.b.c), method calls, comparisons
  (>=, <=, ==, !=, >, <), boolean operators (&&, ||), negation (!), ternary (a ? b : c)
- String interpolation: $"text {field:F2}" with format specifiers
- Static type access: Color.red, Vector3.zero, Application.isPlaying, etc.
- ValueResolver: unified resolution for labels, colors, dropdowns, titles

### BillEditorWindow
- Base class for attribute-driven editor windows (no OnGUI needed)
- Auto-draws fields: string, int, float, bool, Color, Vector2/3, enum, Object
- All group attributes work: BoxGroup, FoldoutGroup, HorizontalGroup, VerticalGroup
- All meta attributes work: ShowIf, ReadOnly, Required, InfoBox, Title
- Buttons with groups and sizing
- State persistence across domain reload

### Validation System
- BillValidator: validates single objects, batches, scene, project assets
- BillValidationWindow: editor window with search, severity filters, object ping
- New validation attributes: [BillValidate] (self-validation method),
  [BillFileExists], [BillRangeValidation], [BillNotEmpty]
- Menu items: Tools > BillInspector > Validate Scene / Validate Assets
- Cross-field validation via @expressions

### Utilities
- BillReflectionCache: centralized field/property/method cache, auto-clears on domain reload
- BillEditorUtility: helper methods for dirty marking, type checking
- BillMenuItems: Tools menu with validation, cache clear, about

### Samples
- 05-EditorWindow: QuestEditorWindow with full attribute-driven UI
- 06-Validation: ValidationDemo with all validation attribute types

## [0.3.0] — Phase 2 + 3

### Phase 2 — Serialization + Advanced Drawers
- BillSerializer: binary serializer for Dictionary, HashSet, Tuple, nested generics
- BillSerializedMonoBehaviour / BillSerializedScriptableObject base classes
- [BillSerialize] attribute for manual field marking
- Dropdown, MinMaxSlider, ColorPalette, FilePath, AssetSelector drawers
- InlineEditor, TableList, DictionaryDrawer

### Phase 3 — Shader Editor Framework
- BillShaderGUI: auto-generated URP-quality material inspector
- Naming conventions: _H_ headers, [Toggle] keywords, texture+color combos
- Material property drawers: BillHeader, BillToggle, BillEnum, BillGradient
- Sample toon shader (URP HLSL)

## [0.1.0] — Phase 1

### Added
- Core attribute system (30+ attributes)
- Drawer pipeline with UI Toolkit and IMGUI
- Condition evaluator, group drawers, property tree
- Button support, basic validation
