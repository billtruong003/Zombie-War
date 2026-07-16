# BillTween — complete reference

Source: `Assets/ThirdParty/BillGameCore/Runtime/Services/Tween/` (`BillTween.cs`, `Tween.cs`, `TweenSequence.cs`, `Ease.cs`, `TweenExtensions.cs`). Namespace `BillGameCore`.

**This is the project's tweener. Do not use DOTween.** Pooled, zero-alloc, float-based, ticked by the framework's `CoroutineRunner` (so it only runs after BillBootstrap is ready). All facade methods return `Tween` (or `TweenSequence`) and may be **null** before bootstrap - always call fluent methods with `?.`.

## `BillTween` static facade

### Core
| Method | Returns | Notes |
|---|---|---|
| `Float(float from, float to, float dur, Action<float> setter)` | `Tween` | The primitive. `setter` gets the eased value each frame. |
| `To(Func<float> getter, Action<float> setter, float to, float dur)` | `Tween` | Starts from `getter()`. |
| `DelayedCall(float delay, Action cb)` | `Tween` | No interpolation; fires `cb` after `delay`. |
| `Sequence()` | `TweenSequence` | New sequence. |
| `ActiveCount` | `int` | Live tween count. |

### Transform (single-axis = single tween, the safe path)
`MoveX/MoveY/MoveZ(Transform t, float to, float dur)`, `LocalMoveX/Y/Z(...)`, `ScaleX/Y/Z(...)`, `Scale(Transform t, float to, float dur)` (uniform), `RotateZ(Transform t, float to, float dur)` -> all return `Tween`.

### Transform (multi-axis = sequence) — ⚠️ see caution below
`Move(Transform t, Vector3 to, float dur)`, `LocalMove(...)`, `ScaleTo(...)` -> return `TweenSequence` (they `Append`+`Join` three axis tweens, each with default `Linear` ease - no way to `SetEase` on the sequence itself).

### UI / renderer
`Fade(CanvasGroup|SpriteRenderer|Image|Text, float to, float dur)`, `FillAmount(Image, float, float)`, `ColorR/ColorG/ColorB(SpriteRenderer, float, float)` -> `Tween`.

### Kill
`Kill(Tween)`, `KillTarget(object target)`, `KillAll()`, `CompleteAll()`. Use `SetTarget(obj)` so `KillTarget(obj)` can find it.

## `Tween` fluent API
All return `this` (chainable):
| Method | Effect |
|---|---|
| `SetEase(EaseType ease)` | Easing (default `Linear`). |
| `SetDelay(float s)` | Wait before starting. |
| `SetLoops(int count, LoopType type = Restart)` | `count`: **0 = once, -1 = infinite, N = repeat N more times**. |
| `SetUnscaled()` | Use `Time.unscaledDeltaTime` (ignores `Time.timeScale` - good for hit-stop-safe UI/pause tweens). |
| `SetTarget(object)` | Owner for `KillTarget`. |
| `OnStart(Action)` | Fires once when it actually starts (after delay). |
| `OnUpdate(Action<float>)` | Each frame; receives **normalized raw t 0..1** (pre-ease, pre-yoyo). |
| `OnComplete(Action)` | Fires when finished (not on `Kill`). |
| `Kill()` | Stop now (marks Complete; no OnComplete). |
| `Complete()` | Jump to end value + fire OnComplete. |

`LoopType`: `Restart`, `Yoyo` (ping-pong), `Incremental` (adds the range each loop). Props: `IsAlive`, `IsComplete`.

## `TweenSequence`
| Method | Effect |
|---|---|
| `Append(Tween)` | Runs after the previous step finishes. |
| `Join(Tween)` | Runs in parallel with the previous `Append`. |
| `AppendInterval(float s)` | Wait. |
| `AppendCallback(Action)` | Fire a callback between steps. |
| `Insert(float atTime, Tween)` | Delay the tween by `atTime`, run parallel. |
| `SetLoops(int count)` | 0 once, -1 infinite. |
| `SetUnscaled()` / `OnComplete(Action)` / `OnStepComplete(Action<int>)` / `Kill()` | — |

## EaseType (31 values)
`Linear`, and In/Out/InOut variants of: `Sine, Quad, Cubic, Quart, Quint, Expo, Circ, Back, Elastic, Bounce`. (e.g. `OutBack`, `InOutQuad`, `OutBounce`.)

## Extension methods (`TweenExtensions`)
`transform.TweenMoveX/Y/Z`, `TweenLocalMoveX/Y/Z`, `TweenMove(Vector3)`, `TweenLocalMove(Vector3)`, `TweenScaleX/Y/Z`, `TweenScale(float)`, `TweenScaleTo(Vector3)`, `TweenRotateZ`; `canvasGroup.TweenFade`; `spriteRenderer.TweenFade/TweenColorR/G/B`; `image.TweenFade/TweenFillAmount`; `text.TweenFade`; `gameObject.TweenScale/TweenMoveY`.

## ⚠️ Caution: multi-axis helpers & passing pooled tweens into a sequence — verified against this exact copy of the source
`BillTween.Move/LocalMove/ScaleTo` build their axis tweens with `Float()` (which **adds them to the service's active list**) and then `Append/Join` them into a sequence. Such a tween ends up driven by **both** the active-list tick and the sequence tick -> it advances at ~2x speed and may be returned to the pool mid-sequence. The same applies if you pass any `BillTween.X(...)`-created tween into `Append/Join`.

**Therefore:**
- For **path / arc / multi-axis** motion, or whenever you need per-axis easing, build the axis tweens yourself and join them into a **fresh** `BillTween.Sequence()` - this is exactly what `Weapon.cs`'s recoil already does (`ApplyRecoil()`): three `BillTween.LocalMoveX/Y/Z` calls, each `.SetEase(...)`, joined via `BillTween.Sequence().Append(...).Join(...).Join(...)`. Copy that pattern, don't call `BillTween.LocalMove(...)` directly if you need custom ease.
- Use `TweenSequence` primarily for **timeline sequencing** with `AppendInterval` / `AppendCallback` (see `Weapon.cs`'s `AppendCallback` chaining the kick -> return phases).
- Single-axis tweens, `Fade`, `Scale`, `DelayedCall` are single-tick and safe to call directly.

## Idiomatic examples (from this codebase)
```csharp
// Weapon.cs recoil kick, per-axis ease, joined into a fresh sequence (the correct multi-axis pattern)
var kickX = BillTween.LocalMoveX(weaponTransform, kickPosition.x, data.recoilKickDuration).SetEase(EaseType.OutQuad);
var kickY = BillTween.LocalMoveY(weaponTransform, kickPosition.y, data.recoilKickDuration).SetEase(EaseType.OutQuad);
var kickZ = BillTween.LocalMoveZ(weaponTransform, kickPosition.z, data.recoilKickDuration).SetEase(EaseType.OutQuad);
BillTween.Sequence().Append(kickX).Join(kickY).Join(kickZ)
    .AppendCallback(() => { /* return-to-rest phase, same per-axis pattern */ });
```
