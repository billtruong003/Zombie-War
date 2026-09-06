# Framework & tech stack — Zombie War

**Cập nhật:** 2026-07-31 · đọc từ source thật trong `Assets/ThirdParty/` và `Packages/`.

Doc này mô tả **những gì có sẵn** để không ai viết lại thứ framework đã làm.

- **Vì sao** kiến trúc như hiện tại (ràng buộc gốc, vẫn có hiệu lực): `Reference/Technical/CORE_WIRING_DIRECTIVE.md`
- **Reference API đầy đủ** kèm code style: skill `.claude/skills/billgamecore/`
- **Đã làm tới đâu**: `CURRENT_STATE.md`

---

## 1. Tổng quan

| Thành phần | Version | Vị trí | Vai trò |
|---|---|---|---|
| BillGameCore | 3.0.0 | `Assets/ThirdParty/BillGameCore/Runtime` | service layer, pool, event, state, tween, audio |
| BillInspector | — | `Assets/ThirdParty/BillGameCore/BillInspector` | attribute inspector kiểu Odin |
| BillTween | trong BillGameCore | `Runtime/Services/Tween` | tween pooled, zero-alloc |
| BillVirtualJoystick | trong BillGameCore | `Runtime/UI/BillVirtualJoystick.cs` | joystick floating-origin |
| Stylized Toon World Kit | 0.6.0 | `Packages/com.billtruong.stylized-toon-world-kit` | shader toon URP 17, **embedded** |
| VAT | — | `Assets/.../VAT` (asmdef riêng) | vertex-animation texture cho enemy |

**Cấm tuyệt đối:** DOTween. Mọi animation đi qua BillTween / `UITransition`.

---

## 2. BillGameCore

### 2.1 Auto-bootstrap

`Runtime/Bootstrap/Bill.cs` → `BillBootstrap` chạy bằng `[RuntimeInitializeOnLoadMethod]`:

1. Đọc `Assets/Resources/BillBootstrapConfig.asset` (**đã tồn tại** trong project này —
   nó cũng giữ `defaultAudioLibrary` mà audio runtime ghi đè lúc chạy).
2. Tạo root `DontDestroyOnLoad` + một `CoroutineRunner`. `Update`/`LateUpdate` của runner gọi
   `ServiceLocator.TickAll/LateTickAll`.
3. Vào `BootState`.

**Hệ quả quan trọng:** nếu bootstrap không chạy thì `Bill.Tween`, `Bill.Timer` không tick — không
gì animate. Đó là lý do luật "luôn Play từ `Bootstrap.unity`".

Guard cho script có thể chạy trước bootstrap:

```csharp
void Start()
{
    if (!Bill.IsReady) { Bill.Events.Subscribe<GameReadyEvent>(OnReady); return; }
    Init();
}
void OnReady(GameReadyEvent _) { Bill.Events.Unsubscribe<GameReadyEvent>(OnReady); Init(); }
```

### 2.2 Facade `Bill`

`Runtime/Bootstrap/Bill.cs:12-27`

```csharp
Bill.Tween  Bill.Scene  Bill.Pool   Bill.Audio  Bill.Save
Bill.UI     Bill.Timer  Bill.Config Bill.Events Bill.Net
Bill.State                                   // GameStateMachine
Bill.Cheat  Bill.Debug  Bill.Analytics       // gated: UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
Bill.IsReady
Bill.Trace.Print() / .Log() / .HealthCheck() / .Unused()
```

Mọi service resolve qua `ServiceLocator`. **Không** `new` service, không `FindObjectOfType`.

`Bill.Net` là `OfflineAdapter` null-object — project singleplayer, đừng define `PHOTON_FUSION`.

### 2.3 Service API (chữ ký thật, `Runtime/Infrastructure/Interfaces.cs`)

```csharp
// Pool — string key; tự load Resources/Pools/<key> nếu chưa register
Bill.Pool.Register(key, prefab, warmCount = 5);
Bill.Pool.Spawn(key, pos, rot);   Bill.Pool.Spawn<T>(key, pos, rot);
Bill.Pool.Return(go);             Bill.Pool.Return(go, delay);
Bill.Pool.WarmUp(key, count);     Bill.Pool.GetStats();

// Events — struct : IEvent, dùng cho tín hiệu toàn game
Bill.Events.Fire(new WaveStartedEvent(...));
Bill.Events.Subscribe<T>(handler);  SubscribeOnce<T>();  Unsubscribe<T>(handler);

// Scene — additive là đường chính của project
Bill.Scene.LoadAdditive(name, onComplete);
Bill.Scene.Unload(name, onComplete);
Bill.Scene.IsAdditiveLoaded(name);

// Audio — key lấy từ AudioLibrary
Bill.Audio.Play(key);  Play(key, worldPos);  Play(key, volume);  PlayPitched(key, pitchMul, vol);
Bill.Audio.PlayMusic(key, fadeDuration);  StopMusic(fade);
Bill.Audio.SetVolume(AudioChannel.Music, v);  GetVolume(ch);  Mute(ch);  Unmute(ch);
// enum AudioChannel { Master, Music, SFX, UI, Voice }

// Save — PlayerProfile của project ngồi trên đây
Bill.Save.Set/GetString/GetInt/GetFloat/GetBool/Get<T>/Has/Delete/SetSlot

// Timer
var h = Bill.Timer.Delay(0.4f, Fire);   h.Cancel();
Bill.Timer.Repeat(interval, cb, count);
```

### 2.4 GameStateMachine

`Runtime/StateMachine/GameStateMachine.cs:92-101` — state có sẵn:
`BootState` · `MenuState` · `LoadingState` · `GameplayState` · `PauseState` · `GameOverState`.

```csharp
Bill.State.GoTo<GameplayState>();
if (Bill.State.GetState<GameOverState>() != null) ...   // guard khi chạy scene lẻ
```

Project dùng nó ở `Flow/GameFlow.cs` và `Gameplay/PlayerController.cs:103-104`.

### 2.5 BillTween

Pooled, zero-alloc, float-based, 31 ease, có loop và sequence. Trả về `Tween` **nullable** —
luôn dùng `?.` vì null cho tới khi bootstrap sẵn sàng.

```csharp
BillTween.MoveY(t, 3f, 1f)?.SetEase(EaseType.OutBack).SetTarget(this);
BillTween.Fade(canvasGroup, 0f, 0.5f)?.SetEase(EaseType.InQuad);
BillTween.FillAmount(image, 0.3f, 0.4f);
BillTween.DelayedCall(0.4f, () => Fire());
```

Single-axis (`MoveX/Y/Z`, `LocalMoveX/Y/Z`, `ScaleX/Y/Z`, `RotateZ`, `Fade`, `FillAmount`,
`ColorR/G/B`) trả `Tween`. Multi-axis (`Move`, `LocalMove`, `ScaleTo`) trả `TweenSequence`.

> ⚠️ **Bẫy multi-axis:** `Move/LocalMove/ScaleTo` dựng 3 tween trục qua `Float()` — vốn đã tự thêm
> vào active list — rồi lại `Append/Join` vào sequence, nên dễ double-tick nếu dùng sai. Muốn
> multi-axis + ease riêng thì tự dựng 3 tween trục rồi `Join` vào `BillTween.Sequence()`.

### 2.6 BillVirtualJoystick

`Runtime/UI/BillVirtualJoystick.cs` — floating origin, pointer-id lock, radius theo rect,
dead-zone remap, handle range. `ZombieWar.VirtualJoystick` là vỏ tương thích mỏng bọc ngoài.

### 2.7 DevTools / CheatConsole

`Runtime/DevTools/DevTools.cs:1` — toàn file gated `#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS`.
Project bật/tắt define bằng menu `CheatBuildToggle` (`Scripts/Editor/CheatBuildToggle.cs`).

> ⚠️ `ZW_CHEATS` **đang bật** cho Android/iOS/Standalone — xem `CURRENT_STATE.md` §3 G9.

---

## 3. BillInspector

Attribute inspector kiểu Odin: `[BillTitle]`, `[BillBoxGroup]`, `[BillRequired]`, `[BillSlider]`,
`[BillTableList]`, `[BillShowIf]`, `[BillButton]`…

Quy tắc của project: `WeaponData`/`ZombieData` hiện là `[SerializeField]` thuần và như vậy là
đúng. Chỉ thêm attribute khi inspector **thật sự** cần group/slider/điều kiện hiển thị — không
rắc attribute theo phản xạ.

---

## 4. asmdef — đọc kỹ, đã gãy 2 lần

```text
BillGameCore.Runtime.asmdef   → references BillInspector.Runtime
_Project.Runtime.asmdef       → references BillGameCore.Runtime + BillInspector.Runtime
_Project.Editor.asmdef        → references _Project.Runtime
VAT.Runtime / VAT.Editor      → asmdef riêng
```

Đây là điểm **khác** với project gốc của BillGameCore (TOSSZONE), nơi framework không có asmdef.
Đừng gỡ asmdef nào.

Nếu gặp `CS0246: type or namespace 'X' could not be found` cho một type ThirdParty: nguyên nhân
gần như luôn là một thư mục không có asmdef bị kẹt trong `Assembly-CSharp` trong khi thứ khác đã
asmdef hoá đang tham chiếu nó. **Cách sửa là cho thư mục đó một asmdef riêng rồi reference**, không
phải gỡ asmdef để "cho nó chạy". Chuyện này đã xảy ra với `DevTools.cs` và với package `VAT/`.

---

## 5. Lớp riêng của project ngồi trên framework

| Lớp | File | Ghi chú |
|---|---|---|
| `PlayerProfile` | `Systems/PlayerProfile.cs` | save authority, ngồi trên `Bill.Save`, versioned |
| `RunState` | `Systems/RunState.cs` | sổ cái trong-trận, `Payout()` idempotent |
| `GameFlow` | `Flow/GameFlow.cs` | điều hướng scene additive, wrap `Bill.Scene` + `Bill.State` |
| `FxPool` / `TracerPool` | `Systems/FxPool.cs`, `Gameplay/FX/TracerPool.cs` | pool FX one-shot |
| `TargetRegistry` | `Systems/TargetRegistry.cs` | nguồn tìm target cho auto-aim, thay `FindObjectsOfType` |
| `UITransition` | `UI/Core/UITransition.cs` | `Show`/`Hide` coroutine cho `UIScreen` |
| `UIFx` | `UI/Core/UIFx.cs` | `Punch`, `Shake`, có cờ `ReducedMotion` |
| `AddressableAudioRuntime` | `Audio/AddressableAudioRuntime.cs` | nạp clip Addressables vào `AudioLibrary` lúc runtime |

### Audio runtime của project

`AddressableAudioRuntime` tự bootstrap bằng `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`,
đọc `Resources/Audio/AddressableAudioCatalog`, load clip theo label rồi `ReplaceEntries` vào
`BillBootstrapConfig.defaultAudioLibrary`. Sau đó `Bill.Audio.Play("<cueKey>")` dùng được ngay.

Cue key theo dạng phân cấp bằng dấu chấm: `sfx.weapon.ak47.fire`, `sfx.impact.flesh.heavy`,
`amb.stage3.bone_yard.base_loop`, `ui.gacha.reveal.epic`, `stinger.wave.start`.

> Hiện chỉ 3 key được phát trong gameplay — xem `CURRENT_STATE.md` §3 G1.

---

## 6. Stylized Toon World Kit + VAT

- Kit là package **embedded** trong `Packages/` (owner sở hữu repo upstream). Có một patch trong
  `Core/URPCompat.hlsl`: `STW_GetMainLight` ưu tiên `_ToonLightDirection`/`_ToonLightColor` do
  `ZombieWar.ToonLightRig` push lên làm global. Nhờ vậy directional light có thể tắt hẳn mà
  không có gì render đen.
- Enemy dùng VAT: `MeshRenderer + VAT_Animator`, **cấm** Animator/SkinnedMeshRenderer cho enemy
  production (có test enforce). Bake bằng `Tools/ZombieWar/Bake Enemies (VAT)`.
- Shader `ZombieWar/VAT/EnemyToon`: unlit albedo + specular stepped, `_HitFlash` và `_Dissolve`
  per-instance qua MaterialPropertyBlock. Baker xuất **cả** position map lẫn normal map — thiếu
  normal map thì cel terminator đứng yên khi thân quái động.

---

## 7. Quy ước code (rút gọn)

1. Tween = BillTween. Không DOTween.
2. Truy cập service qua facade `Bill.*`.
3. Pool mọi thứ spawn thường xuyên. Không `Instantiate`/`Destroy` trong hot path.
4. `struct : IEvent` + `Bill.Events` cho tín hiệu **toàn game**. Dùng `event Action` C# thuần cho
   tín hiệu nội bộ component (xem `Health.OnDamaged`/`OnDeath`). Luôn `Unsubscribe` — channel của
   EventBus là static và sống dai.
5. Data designer chỉnh được → ScriptableObject.
6. `using BillGameCore;` và `using BillInspector;` khi cần.
7. Prefab: visual nằm ở child `Visual`, root chỉ giữ logic + physics — xem `Reference/Technical/PREFAB_CONVENTIONS.md`.

Reference API đầy đủ: `.claude/skills/billgamecore/reference/`
(`../.agents/skills/billgamecore/reference/conventions.md` ·
`../.agents/skills/billgamecore/reference/tween.md` ·
`../.agents/skills/billgamecore/reference/services.md` ·
`../.agents/skills/billgamecore/reference/billinspector.md`).
