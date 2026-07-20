# BillGameCore services — complete reference

Source: `Assets/ThirdParty/BillGameCore/Runtime/`. Namespace `BillGameCore`. Access every service via the `Bill` facade. Interfaces in `Infrastructure/Interfaces.cs`.

## Bill facade map
`Bill.Tween` `Bill.Scene` `Bill.Pool` `Bill.Audio` `Bill.Save` `Bill.UI` `Bill.Timer` `Bill.Config` `Bill.Events` `Bill.Net` `Bill.State` · `Bill.IsReady` · (dev only) `Bill.Cheat` `Bill.Debug` `Bill.Analytics`.

## Pool — `Bill.Pool` (`IPoolService`)
String-keyed GameObject pool. Auto-loads `Resources/Pools/<key>` if the key isn't registered.
```csharp
GameObject Spawn(key) | Spawn(key,pos,rot) | Spawn(key,parent) | Spawn(key,pos,rot,parent)
T Spawn<T>(key) | Spawn<T>(key,pos,rot)            // where T : Component
void Return(go) | Return(go, delay)
void ReturnAll(key) | ReturnAll()
void WarmUp(key, count)
void Register(key, prefab, warmCount = 5)
int GetPooledCount(key) | GetActiveCount(key); string GetStats()
```
- `ZombieAI.cs` already calls `Bill.Pool?.Return(gameObject)` on death (`DissolveAndReturn()`). Nothing yet calls `Bill.Pool.Register`/`Spawn` for zombies - that belongs in the Phase 4 `WaveSpawner.cs` (not yet written) which needs to `Register()` each `ZombieData.prefab` at level start and `Spawn()` instead of `Instantiate()`.
- Extension: `go.ReturnToPool()`, `go.ReturnToPool(delay)`, `component.ReturnToPool()` (from `BillExtensions`, if present in this copy).
- Register defaults in `BillBootstrapConfig.defaultPools` once that asset exists (see `billgamecore` SKILL.md - not created yet in this project).

## Timer — `Bill.Timer` (`ITimerService`)
```csharp
TimerHandle Delay(seconds, cb) | Delay(seconds, cb, unscaled)
TimerHandle Repeat(interval, cb) | Repeat(interval, cb, count)   // count = -1 infinite
void Cancel(handle) | CancelAll(); int ActiveCount
```
`TimerHandle`: `.Cancel()`, `.IsActive`, `.IsCancelled`.

## Scene — `Bill.Scene` (`ISceneService`)
```csharp
void Load(name) | Load(name, TransitionType, dur = 0.5f) | Load(name, TransitionType, dur, EaseType) | Load(buildIndex)
void LoadAdditive(name, onComplete=null) | Unload(name, onComplete=null) | UnloadAllAdditive() ; bool IsAdditiveLoaded(name)
void LoadAsync(name, onProgress=null, onComplete=null)
void LoadWithTransition(name, TransitionType, dur, EaseType, onProgress=null, onComplete=null)
void Reload() | LoadNext() | LoadPrevious()
```
`TransitionType`: `None`, `Fade`, `CrossFade`. Fires `SceneLoadStartEvent` / `SceneLoadCompleteEvent`. Relevant for Level 1 -> Level 2 transition (Phase 6) and win/lose flow (Phase 5).

## Audio — `Bill.Audio` (`IAudioService`)
```csharp
void Play(key) | Play(key, pos) | Play(key, volume) | Play(key, pos, volume)
void PlayMusic(key) | PlayMusic(key, fadeDuration) | StopMusic(fadeDuration = 0)
void SetVolume(AudioChannel, v) | float GetVolume(AudioChannel) | Mute(AudioChannel) | Unmute(AudioChannel)
```
`AudioChannel`: `Master, Music, SFX, UI, Voice`. Keys resolve from an **`AudioLibrary`** ScriptableObject assigned in `BillBootstrapConfig.defaultAudioLibrary` - not created yet in this project. `Weapon.cs`/`Bomb.cs` already call `Bill.Audio?.Play("gun_fire")`/`Play("bomb_explode", position)` - those keys need real `AudioLibrary` entries before they'll produce sound.

## Save — `Bill.Save` (`ISaveService`)
Slot-prefixed PlayerPrefs (`s{slot}_{key}`). Likely unused in this project (no persistent progression in the brief) unless a high-score/best-time feature gets added in Phase 5/7.
```csharp
Set(key, string|int|float|bool) ; Set<T>(key, T value)   // T serialized as JSON
GetString/GetInt/GetFloat/GetBool(key, fallback) ; T Get<T>(key)
bool Has(key) ; Delete(key) ; SetSlot(int) ; Flush()
```

## Config — `Bill.Config` (`IConfigService`)
Loads `GameConfigAsset`(s) from `Resources/Configs`. Probably not needed - this project's tunables live directly on `WeaponData`/`ZombieData` ScriptableObjects instead.

## Events — `Bill.Events` (`IEventBus`)
Events are `struct … : IEvent`. Channels are **static per type** -> always unsubscribe.
```csharp
Subscribe<T>(Action<T>) ; SubscribeOnce<T>(Action<T>) ; Unsubscribe<T>(Action<T>)
Fire<T>(T data) ; Fire<T>()   // parameterless for struct events
```
Built-in: `GameReadyEvent`, `AppPauseEvent{IsPaused}`, `SceneLoadStartEvent{SceneName}`, `SceneLoadCompleteEvent{SceneName}`, `StateChangedEvent{From,To}`, `NetworkPhaseChangedEvent{Phase}`, `ConfigRefreshedEvent`. Good candidates for new project events: `WaveStartedEvent`, `LevelCompleteEvent`, `PlayerDiedEvent` (Phase 4/5) - reserve these for cross-system signals (UI reacting to game state), not per-instance callbacks (use plain C# `event Action` for those, see `Health.cs`).

## State machine — `Bill.State` (`GameStateMachine`)
```csharp
AddState<T>() | AddState<T>(instance) ; GoTo<T>() | GoTo(Type) | GoBack()
bool IsInState<T>() ; T GetState<T>() ; GameState Current/Previous ; string CurrentName ; History
OnEnter<T>(Action) ; OnExit<T>(Action) ; OnTransition(Action<GameState,GameState>) ; string GetHistoryLog()
```
`GameState` base: `Enter()/Tick(dt)/Exit()/Name`. Built-in states: `BootState, MenuState, LoadingState, GameplayState, PauseState` (sets `Time.timeScale=0` on enter/restores on exit - useful for a pause menu), `GameOverState`. Likely relevant for Phase 5's win/lose flow and Phase 7 polish.

## Network — `Bill.Net` (`INetworkService`)
**Not used in this project - Zombie War is singleplayer.** `OfflineAdapter` (null-object) is the default and will remain so; do not define `PHOTON_FUSION` or add the Fusion package. The whole `Network/Fusion/*` module is safely dormant (whole-file `#if PHOTON_FUSION` guards, verified during import - see Docs/GAMEPLAY_DESIGN.md B3 notes).

## UI — `Bill.UI` (`IUIService`) — screen-space UI Toolkit
```csharp
T Open<T>() | Open<T>(Action<T> setup) ; Close<T>() ; CloseAll() ; Toggle<T>() ; bool IsOpen<T>() ; AnyOpen()   // T : BasePanel, new()
```
`BasePanel` (UI Toolkit): override `Build(VisualElement root)`, `OnOpened()/OnClosed()`. Auto-creates a screen-space `UIDocument`. This project's HUD/menu plan (Phase 5) currently assumes regular uGUI + the imported GUI Pro-SuperCasual kit (`Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/`), not UI Toolkit panels - decide per-screen whether `Bill.UI` or plain Canvas prefabs fit better; don't feel obligated to route everything through `Bill.UI` just because it exists.

## Bootstrap config — `BillBootstrapConfig` (ScriptableObject in `Resources/`)
Create via **BillGameCore ▸ Bootstrap Config** menu (not created yet in this project - required before any `Bill.*` call works). Fields: `enforceBootstrapScene`, `defaultGameScene`, `returnToEditSceneInEditor`; dev `includeDebugOverlay/includeCheatConsole/showOverlayOnStartup/enableTracing`; `defaultNetworkMode` (leave `Offline`); `defaultPools[]`; `defaultAudioLibrary` + volume fields; `targetFrameRate`, `vSyncCount`.

## Utilities — `BillExtensions` (`Runtime/Utils/Extensions.cs`)
- Transform: `DestroyAllChildren()`, `ResetLocal()`, `SetX/SetY/SetZ(float)`.
- GameObject: `GetOrAdd<T>()`, `Has<T>()`, `ReturnToPool([delay])`.
- Collections: `list.Random()`, `list.Shuffle()`, `list.SafeGet(i, fb)`, `collection.IsNullOrEmpty()`.
- Vector3: `Flat()` (zero Y), `WithY(y)`, `a.FlatDistance(b)` (XZ distance) - useful for `ZombieAI`/`PlayerMovement`'s existing manual `v.y = 0f` flattening, consider switching to `.Flat()` for consistency if touching that code again.
