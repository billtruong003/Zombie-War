# Zombie War — Code Conventions

Coding standard for the **Zombie War** top-down shooter. Goal: clean, readable C# with sane allocation discipline that matches the BillGameCore style. These rules are mandatory for all gameplay code under `Assets/_Project/Scripts/Runtime/`.

> Core principles: (1) clear declarations, (2) **public surface up, private implementation down**, (3) **avoid needless per-frame GC allocations** (this is a horde shooter with potentially hundreds of zombies — allocation spikes show up as real frame drops, not just a VR-specific concern), (4) small single-purpose functions.

---

## 1. Naming & declarations

| Element | Style | Example |
|---|---|---|
| Types, methods, public members, events | `PascalCase` | `ZombieAI`, `TryFire()` |
| Constants & `static readonly` | `PascalCase` | `const float MaxRange = 10f;` |
| Locals & parameters | `camelCase` | `float aimRange` |
| **Private/internal fields** | `_camelCase` (underscore) | `Rigidbody _rb;` (matches BillGameCore, already used throughout `Assets/_Project/Scripts/`) |
| Serialized inspector fields | `[SerializeField] private camelCase` | `[SerializeField] private float moveSpeed;` |
| Interfaces | `I` + `PascalCase` | `IDamageable`, `ITargetable` |
| Booleans | `is/has/can/should` prefix | `bool HasTarget`, `IsDead` |
| Enums | `PascalCase` type + members | `enum ZombieTier { Full, Cheap, Inactive }` |

- **One declaration per line.** Declare variables as close to first use as possible.
- **`var` only when the type is obvious** from the right-hand side. Prefer clarity over brevity.
- **No abbreviations** except well-known ones (`id`, `ui`, `sfx`, `rb`, `dt`). No `mgr`, `tmp`.
- **No magic numbers.** Promote to a named `const` or a `[SerializeField]` (designer-tunable). Exceptions: `0`, `1`, `-1`.
- **Namespace = `ZombieWar`** (single namespace for this project's own code, matching `_Project.Runtime.asmdef`'s `rootNamespace` — don't split into per-feature sub-namespaces unless the project grows enough to need it). One top-level type per file; file name = type name.

## 2. Member ordering — public up, private down

Fields grouped at the top; public API first, private helpers last. See `ZombieAI.cs` for the reference example already in this codebase: `[SerializeField]` config → static IDs/statics → private runtime fields → public properties → interface explicit implementations → Unity lifecycle methods → public methods → private helpers.

Within each kind, also order by access: `public` → `internal` → `protected` → `private`.

## 3. Allocation discipline (not VR-critical here, but still matters at horde scale)

Zombie War can have many `ZombieAI` instances active at once (see `ZombieManager.cs`'s 3-tier gating, which exists specifically to bound this cost). In per-frame hot paths:

**Avoid:**
- LINQ (`.Where/.Select/.Any/.ToList`) in `Update`/`FixedUpdate` — allocates iterators/closures. Editor-only tools may use it freely.
- String concatenation/interpolation per frame for anything other than debug logs.
- Lambdas that capture local state inside a per-frame loop — cache the delegate instead.
- `Instantiate`/`Destroy` at runtime for anything spawned repeatedly (zombies, impact VFX, bullets) — use `Bill.Pool` instead (see `billgamecore` SKILL.md).
- `GetComponent`/`Camera.main`/`GameObject.Find` inside `Update` — cache in `Awake`.

**Do:**
- Plain `for` loops in per-frame hot paths (`ZombieManager.cs` already does this over its zombie list).
- `Physics.OverlapSphereNonAlloc`/`RaycastNonAlloc` with a preallocated buffer for anything called per-zombie or per-frame.
- Cache references (`Transform`, `Rigidbody`, components) in fields set during `Awake`.

## 4. Simplicity — break functions down

- **One function = one job.** Aim ≤ ~30 lines; extract a named method rather than growing one.
- **Guard clauses / early return** over nested `if`. `ZombieAI.Update()`'s `if (_tier != ZombieTier.Full || _state == State.Dead) return;` is the pattern to follow.
- **Extract complex booleans** into intention-named methods/properties.
- **One responsibility per class.** If a class both drives AI state and handles rendering setup and pooling lifecycle, consider whether it should split — `ZombieAI`/`ZombieManager`/`Health` are already split this way (state machine, tiering, HP) rather than one god-class.
- No deeply chained ternaries; no clever one-liners that hurt readability.

## 5. Formatting

- **Allman-ish** (this codebase mixes K&R for one-liners with Allman for blocks — stay consistent with whatever surrounds the code you're editing rather than reformatting wholesale).
- **4-space indent**, no tabs. UTF-8.
- Expression-bodied members for true one-liners (`Transform ITargetable.Transform => transform;`).
- `using` directives at top: framework first (`using BillGameCore;`), then `using BillInspector;` when needed, then Unity/System, then this project's own namespace usages last.

## 6. Unity & framework specifics

- **Inspector data** = `[SerializeField] private`. Pure-data ScriptableObjects (`WeaponData`, `ZombieData`) may use public fields directly — that's the existing convention, don't retrofit them to `[SerializeField] private` + properties unless there's a real reason.
- **No singletons / `FindObjectOfType`** for services — resolve via `Bill.*`. The one deliberate exception already in this codebase is `PlayerMovement.Instance` / a future single-player reference pattern — that's fine for "the one player", not a general pattern for services (see `billgamecore` SKILL.md rule #2).
- **Tweens = `BillTween` only. Never DOTween.**
- **Spawning = `Bill.Pool`** once the pooling call sites exist (Phase 3/4 — see `billgamecore` SKILL.md's project-specific recipes for what's wired vs. still missing).
- **Events = `Bill.Events`** for cross-system signals; plain C# `event Action` for per-instance component callbacks (`Health.OnDamaged`/`OnDeath` is the established pattern) — don't force every callback through the global event bus.
- **Fail loud in editor**: validate and `Debug.LogError` on misuse; never swallow exceptions silently.
- `[RequireComponent(typeof(X))]` when a component is mandatory (see `ZombieAI`'s `NavMeshAgent, Health, VAT_Animator` requirement).
- **asmdef boundaries matter here** — see `billgamecore` SKILL.md rule #3 before adding any new script to a `ThirdParty/` folder or moving files between asmdef'd and loose folders.

## 7. Comments & docs

- **Explain WHY, not WHAT.** This codebase's existing comments follow this already (e.g. `ZombieManager.cs`'s comment on why Inactive tier doesn't call `SetActive(false)`) — match that style: justify non-obvious decisions, don't narrate what the next line already says.
- Delete dead/commented-out code. No `TODO` without an owner/context.
- Vietnamese or English comments both fine in this codebase (commit messages and design docs mix both) — be consistent within a single file.
