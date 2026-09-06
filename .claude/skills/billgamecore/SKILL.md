---
name: billgamecore
description: How to write gameplay C# in the Zombie War Unity project using the BillGameCore framework (Bill.* services, BillTween tweening — NEVER DOTween, object pooling, EventBus, GameStateMachine) and BillInspector (Odin-style) attributes. Use whenever creating or editing C# scripts, tweens/animations, ScriptableObject data, scene flow, pooling, timers, or audio in this project.
---

# BillGameCore — coding guide for Zombie War

`Assets/ThirdParty/BillGameCore` is a **code-first, zero-config Unity 6 framework** (`com.bill.gamecore` v3.0.0), brought over from another project (TOSSZONE). It auto-boots and exposes everything through a single static facade `Bill`. Write game code against `Bill.*`; never re-implement what a service already does.

> This is the canonical "how we code" reference for this project. Deep tables live in `reference/` — read them when you need exact signatures or the code style:
> [reference/conventions.md](reference/conventions.md) · [reference/tween.md](reference/tween.md) · [reference/services.md](reference/services.md) · [reference/billinspector.md](reference/billinspector.md)

## 🔑 Golden rules (read first)

1. **Tween = `BillTween`. NEVER use DOTween** (do not import it, do not add the package). Every animation goes through `BillTween` / `Tween` / `TweenSequence`. See [reference/tween.md](reference/tween.md).
2. **Access services via the `Bill` facade** (`Bill.Tween`, `Bill.Pool`, `Bill.Events`, …). Don't `new` services or `FindObjectOfType`. Resolution is `ServiceLocator` under the hood.
3. **asmdef setup is the OPPOSITE of BillGameCore's origin project — read carefully:** in the source project (TOSSZONE), `BillGameCore/Runtime` had no asmdef and everything compiled into the default `Assembly-CSharp`, so gameplay code there also had to avoid an asmdef. **We fixed that here.** `Assets/ThirdParty/BillGameCore/Runtime/BillGameCore.Runtime.asmdef` gives it a proper asmdef (referencing `BillInspector.Runtime`), and our own `_Project.Runtime.asmdef` references `BillGameCore.Runtime` + `BillInspector.Runtime` directly. **Do not remove either asmdef** — that's what makes `using BillGameCore;` work correctly from `Assets/_Project/Scripts/Runtime/`. If you ever see `CS0246: type or namespace 'X' could not be found` for a BillGameCore/VAT/other ThirdParty type, the cause is almost always a loose (no-asmdef) file that got left in the default assembly while something asmdef'd tries to reference it — give that folder its own `.asmdef` and reference it, don't strip asmdefs to "fix" it. This has already happened twice in this project (BillGameCore's `DevTools.cs`, and the `VAT/` package) — same fix both times.
4. **Pool anything spawned frequently** (zombies, bullets/impact VFX, particles) with `Bill.Pool`. Never `Instantiate`/`Destroy` in hot paths — see `ZombieAI.cs`'s `Bill.Pool?.Return(gameObject)` on death for the existing pattern.
5. **Events are `struct … : IEvent`.** Fire/subscribe via `Bill.Events`. Always `Unsubscribe` (EventBus channels are static and persist). Prefer plain C# events (`event Action`) for per-instance component signals (see `Health.cs`'s `OnDamaged`/`OnDeath`) — reserve `Bill.Events` for game-wide/cross-system events (e.g. level start/end, wave changes), not every component-local callback.
6. **Author data as ScriptableObject + BillInspector attributes** when the data is designer-tunable (`WeaponData`, `ZombieData` already do this as plain `[SerializeField]` — add BillInspector attributes on top only when the inspector actually needs grouping/validation, don't add them reflexively). See [reference/billinspector.md](reference/billinspector.md).
7. **Namespaces:** `using BillGameCore;` (framework) and `using BillInspector;` (attributes) when needed.
8. **No networking in this project.** BillGameCore's `Network/Fusion/*` module exists (whole-file gated by `#if PHOTON_FUSION`) but Zombie War is singleplayer — do not define `PHOTON_FUSION`, do not add the Fusion package. `Bill.Net` will just be the `OfflineAdapter` (null-object), harmless if untouched.

## Architecture in 60 seconds

- **Auto-bootstrap** (`Bootstrap/Bill.cs` → `BillBootstrap`): runs via `[RuntimeInitializeOnLoadMethod]`. Reads `BillBootstrapConfig` from a `Resources/` folder — **`Assets/Resources/BillBootstrapConfig.asset` already exists here**; it also holds `defaultAudioLibrary`, which `AddressableAudioRuntime` overwrites at runtime with the loaded cue set. Creates a `DontDestroyOnLoad` root + a `CoroutineRunner` whose `Update`/`LateUpdate` drive `ServiceLocator.TickAll/LateTickAll` — **this tick is what drives `Bill.Tween`, `Bill.Timer`, etc. Nothing animates if bootstrap didn't run.**
- **ServiceLocator** (`Infrastructure/ServiceLocator.cs`): `Register`, `Get<T>`, `TryGet<T>`, `Has<T>`. Auto-calls `Initialize()`/`Cleanup()`, auto-adds `ITickable`/`ILateTickable`.
- **`Bill` facade**: `Tween, Scene, Pool, Audio, Save, UI, Timer, Config, Events, Net, State` + `IsReady`. Dev-only: `Cheat, Debug, Analytics` (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`).

### Early-access guard (use in any script that might run before bootstrap)
```csharp
void Start()
{
    if (!Bill.IsReady) { Bill.Events.Subscribe<GameReadyEvent>(OnReady); return; }
    Init();
}
void OnReady(GameReadyEvent _) { Bill.Events.Unsubscribe<GameReadyEvent>(OnReady); Init(); }
```

## Tween — the essentials (full API in reference/tween.md)

`BillTween` is a **pooled, zero-alloc, float-based** tweener with 31 eases, loops, and sequences. Returns a `Tween` (nullable — use `?.` because it's null until bootstrap is ready).

```csharp
BillTween.MoveY(t, 3f, 1f)?.SetEase(EaseType.OutBack).SetTarget(this);
BillTween.Fade(canvasGroup, 0f, 0.5f)?.SetEase(EaseType.InQuad);
BillTween.DelayedCall(0.4f, () => Fire());
```

**⚠️ `BillTween.Move/LocalMove/ScaleTo` (multi-axis) build 3 axis tweens via `Float()` — which already adds them to the active list — then `Append/Join` them into a sequence too, so they'd double-tick if misused.** For a multi-axis tween with per-axis easing, build the 3 axis tweens yourself via `BillTween.LocalMoveX/Y/Z` (each with its own `.SetEase`) and `Join` THOSE into a fresh `BillTween.Sequence()` — don't call `LocalMove(...)` and then try to add an ease.

**Where BillTween is actually used today:** UI only — `UI/Core/UIFx.cs`, `UIFxPress`, `UIFxPulse`, `UIFxBreathe`, `UIToggleVisual`. Weapon recoil is deliberately NOT a tween: it is a hand-rolled `SmoothDamp` spring on `RecoilPivot` (`Gameplay/Weapon.cs:175-184, 524-543`) because it must be re-impulsed every shot and settle continuously, which a fire-and-forget tween can't do. Don't "convert it back" to BillTween.

## Services cheat-sheet (full signatures in reference/services.md)

```csharp
Bill.Pool.Spawn("pickup_coin", pos, rot);    // string-key pool; auto-loads Resources/Pools/<key> if unregistered
Bill.Pool.Return(go);                        // or go.ReturnToPool(delay) extension
Bill.Timer.Delay(0.4f, Fire);                // -> TimerHandle (.Cancel()); Repeat(interval,cb,count)
Bill.Audio.Play("sfx.weapon.ak47.fire");     // dotted cue keys from AddressableAudioCatalog (330 of them)
Bill.Audio.Play(key, worldPos);              // PlayPitched/PlayMusic/StopMusic/SetVolume(AudioChannel,..)
Bill.Events.Fire(new WaveStartedEvent{ WaveIndex = i });   // struct : IEvent, game-wide signals only
Bill.State.GoTo<GameplayState>();            // Boot/Menu/Loading/Gameplay/Pause/GameOver built in
```

## Authoring data — BillInspector (full catalog in reference/billinspector.md)

`WeaponData`/`ZombieData` are plain `[SerializeField]` ScriptableObjects right now — that's correct for their current simplicity (Simple is Key: don't add BillInspector attributes until the inspector genuinely needs grouping, sliders, or conditional visibility). When they grow (e.g. a boss's per-phase attack table), reach for `[BillBoxGroup]`/`[BillTableList]`/`[BillSlider]` etc. instead of hand-rolling a custom editor.

```csharp
using BillInspector;
[CreateAssetMenu(menuName = "ZombieWar/Zombie Data")]
public class ZombieData : ScriptableObject
{
    [BillTitle("Zombie")] [BillRequired] public string zombieName;
    [BillBoxGroup("Combat")] [BillSlider(0, 500)] public float maxHealth;
    [BillBoxGroup("Combat")] public float damage;
}
```

## Project-specific recipes

- **Zombie pooling — DONE, don't rebuild it.** `ZombieSpawner.cs` registers a pool per `ZombieData` (`EnsureRegistered`) and spawns through `Bill.Pool`; `ZombieBase` returns itself on death with a pool-safe reset. `WaveDirector` pre-warms every pool of every wave before the run starts. The old `ZombieAI.cs`/`WaveSpawner.cs` names in earlier docs no longer exist — current files are `Gameplay/Zombies/ZombieBase.cs` (+ 6 subclasses), `Gameplay/Waves/ZombieSpawner.cs`, `Gameplay/Waves/WaveDirector.cs`.
- **Other pools already in place:** `FxPool` (impact/muzzle), `TracerPool` (`MeshTracer`), `DamageNumberSpawner`, `PickupManager` (`Resources/Pools/pickup_*`).
- **Weapon recoil / camera shake — intentionally NOT tweens.** `Weapon.cs` runs a `SmoothDamp` spring on `RecoilPivot` re-impulsed per shot; `CameraFollow.Shake` samples an assigned noise texture (`NoiseTextureSampler.cs`). Both need continuous re-impulse, which a fire-and-forget tween can't express. Leave them alone.
- **SFX:** the library **exists** — `Assets/Resources/Audio/ZombieWarRuntimeAudioLibrary.asset` is filled at runtime by `AddressableAudioRuntime` from `AddressableAudioCatalog` (330 dotted cue keys / 970 clips). Route everything through `Bill.Audio.Play(key)`; never `AudioSource.PlayOneShot` in gameplay code. Only 3 cues are wired today — wiring the rest is the current top priority (`Docs/REMAINING_FEATURES.md` P0).
- **Bootstrap is done:** `Assets/Resources/BillBootstrapConfig.asset` exists and `Bootstrap.unity` is the entry scene (`BootstrapEntry` → `GameFlow.EnterMenu`). Always Play from `Bootstrap.unity`; playing `Menu`/`Map_*` directly gives `SERVICE NOT FOUND` spam, which is expected, not a bug.

## Gotchas

- A `[Bill] SERVICE NOT FOUND` error → bootstrap didn't run: you Played a scene other than `Bootstrap.unity`, or accessed a service before `GameReadyEvent`.
- Tween/Timer "not animating" → same root cause (no `CoroutineRunner` tick).
- Don't add DOTween (rule #1). Don't strip an asmdef to "fix" a missing-type error (rule #3) — add one to the orphaned folder instead.
- `Bill.Cheat/Debug/Analytics` are gated `UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS`. ⚠️ `ZW_CHEATS` is currently ENABLED for Android/iOS/Standalone (`ProjectSettings.asset:833-835`), so release builds currently ship the cheat panel — toggle it off via the `CheatBuildToggle` menu before any store build.
- `DynamicAnimationEventHub` (`Runtime/Utils/DevTools.cs`... actually `Runtime/DevTools/DevTools.cs`, moved there to fix an asmdef issue — see rule #3) is a **global-namespace** component (string→UnityEvent map, `Trigger(id)`) — handy for animation-event wiring, e.g. a zombie attack animation firing a hit event.

## Where the project stands (2026-07-31)

Read `Docs/CURRENT_STATE.md` before assuming a system is missing. Short version of what is
already wired: combat core, auto-aim, pooling, VAT enemies, spawn safety, save/economy/gacha.
What is built but NOT connected: audio (970 clips, only 3 cues playing), campaign level select,
level-up perks (picking one has no effect), run result screen, haptics, bomb pickup.
Full evidence with `file:line` is in that doc; the prioritized fix order is in
`Docs/REMAINING_FEATURES.md`.
