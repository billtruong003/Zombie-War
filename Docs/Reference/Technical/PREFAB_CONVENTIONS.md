# Prefab Conventions — ZombieWar

Project-wide rules for authoring prefabs (by code or by hand). Keep these in sync with
`Assets/_Project/Scripts/Editor/ZombieVATBaker.cs`.

## 1. Visual mesh always goes on a child, never on the root

**Rule:** the visual (MeshFilter + MeshRenderer, and any renderer-coupled driver such as
`VAT_Animator`) must live on a child GameObject (conventionally named `Visual`), **not** directly
on the prefab root.

```
Root (logic + physics)          <- NavMeshAgent, CapsuleCollider, ZombieBase/Health, axis-aligned
└── Visual (presentation)       <- MeshFilter, MeshRenderer, VAT_Animator — free to rotate/offset
```

**Why**
- The model can be rotated / offset / scaled to face-correct or fix pivot **inside the Visual child**
  without touching the root.
- Colliders + NavMeshAgent stay on the root and remain axis-aligned, so they are **never skewed** by
  the model's facing direction. Hit volumes and pathing stay predictable.
- Swapping the art (different mesh, LOD, VFX) is a localized change to one child — logic untouched.
- Much easier to custom per-instance.

**Implication for `RequireComponent`:** components that must sit next to the renderer (e.g.
`VAT_Animator` → `[RequireComponent(MeshRenderer)]`) must **not** be listed in the root logic
component's `RequireComponent`. Fetch them with `GetComponentInChildren<T>()` instead of
`GetComponent<T>()`. See `ZombieBase.cs`: VAT_Animator was removed from its `[RequireComponent]` and
is now resolved via `GetComponentInChildren<VAT_Animator>()` in `Awake`.

## 2. Root responsibilities
- Physics/logic only: NavMeshAgent, CapsuleCollider, Health, the `ZombieBase`-derived behaviour.
- Serialized refs on the root component point *down* into the child (`bodyRenderer` → child MeshRenderer).
- Root transform is the authoritative position/steering; do not rotate the root to face the model —
  rotate the `Visual` child.
