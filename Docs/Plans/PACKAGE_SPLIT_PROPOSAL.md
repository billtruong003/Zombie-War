# Đề xuất tách package — BillGameCore + Outline

**Ngày:** 2026-10-08 · trạng thái: **đã làm phần tách repo** (cùng ngày) · game chưa chuyển sang package.

> Kết quả 08/10: 6 repo public có tag. `BillGameCore` v3.1.0 gồm hai package `com.bill.gamecore` và
> `com.bill.inspector`, kèm site tài liệu https://billtruong003.github.io/BillGameCore/. Bốn repo
> package riêng: `BillFav` v1.0.0, `BillSceneSwitcher` v2.0.0, `Bill-SSOutline` v2.0.0, `BillVAT`
> v1.0.0. `stylized-toon-world-kit` lên v0.7.0, có sample Showcase.
>
> Kiểm tra: cài cả 7 package từ git tag vào một project Unity 6000.3.10f1 + URP 17.3 trống. Kết quả
> 0 lỗi compile, 45/45 test EditMode pass, 38 shader compile đủ mọi pass, 0 lỗi.
>
> Khác với đề xuất ban đầu: chưa chia nhỏ BillGameCore thành 10 package (cần làm B10 trước), và Toon
> Kit được giữ nguyên chứ không tách bản gọn. Việc còn lại là chuyển game sang dùng package, xem
> `FRAMEWORK.md` §1.1.

## 1. Kết quả audit

### BillGameCore (`Assets/ThirdParty/BillGameCore`, v3.0.0)

Lỗi phải sửa trước khi tách:

| # | Vấn đề | Bằng chứng |
|---|---|---|
| B1 | Tween bị tick hai lần trong sequence: chạy nhanh gấp đôi, bước Append chạy ngay, tween bị trả về pool khi sequence còn giữ | `Runtime/Services/Tween/BillTween.cs:55`, `TweenSequence.cs:45-60,136,191`; `Move/LocalMove/ScaleTo` dính lỗi này mọi lần (`BillTween.cs:224-250`) |
| B2 | Handle tween không có generation: giữ `Tween` rồi `Kill()` muộn có thể giết nhầm tween khác đã tái dùng | `Tween.cs` (Pool có `_gen`, Tween thì không) |
| B3 | `Samples/` không có asmdef nên lọt vào Assembly-CSharp và vào build; `Editor/BillTweenDemoEditor.cs` phụ thuộc Samples | `Samples/BillSampleGame.cs:8`, `Editor/BillTweenDemoEditor.cs:6,53` |
| B4 | `Editor/` không có asmdef | — |
| B5 | `package.json` thiếu `dependencies` (ugui, TMP) và `samples` | `package.json` |
| B6 | Define của game nằm trong framework: `ZW_CHEATS` | `Bootstrap/Bill.cs:24,167`, `DevTools/DevTools.cs:1` |
| B7 | Đường dẫn cứng của game hoặc đường dẫn cũ | `BillSceneSwitcherPrefs.cs:45,54`, `BillSetupWizard.cs:60-96`, `BillFavData.cs:141`, `BillSceneSwitcherData.cs:206` |
| B8 | File data của project nằm trong package: `BillFavData.asset`, `BillSceneSwitcherData.asset` | thư mục gốc package |
| B9 | Fusion bật bằng define tay `PHOTON_FUSION`, không dùng `versionDefines` | 6 file trong `Runtime/Network/Fusion` |
| B10 | `Bill.cs` đăng ký cứng mọi service và 6 state nên không tách module được | `Bootstrap/Bill.cs:129-176` |
| B11 | `Interfaces.cs` gom contract của mọi module (ví dụ `ISceneService` dùng `EaseType`) | `Infrastructure/Interfaces.cs` |
| B12 | BillInspector đè inspector của **mọi** MonoBehaviour/ScriptableObject (fallback editor) | `BillInspectorEditor.cs:974,979` |
| B13 | Tên chưa sạch: `rootNamespace` rỗng, `DynamicAnimationEventHub` ở global namespace và tên file lệch, `Bill.Debug` che `UnityEngine.Debug` | `Utils/DynamicEventHub.cs`, `Bill.cs:26` |
| B14 | Quảng cáo "zero-alloc" sai: closure ở mỗi shortcut, `Sequence()` và `TimerHandle` cấp phát mới | `BillTween.cs:59,185+`, `TimerService.cs:36` |
| B15 | Tracing mặc định bật và lấy `StackTrace` ở mỗi lần truy cập `Bill.X` | `BillBootstrapConfig.cs:18`, `ServiceLocator.cs:207` |
| B16 | Không có README/CHANGELOG cho BillGameCore; không có test (BillInspector có 39 test) | — |

Game đang dùng: Events (31 file), Audio (26), Pool (19), Tween (8, chỉ `Float/Scale/Fade/KillTarget`), State, Scene, Save, Timer, Cheat, Joystick.
Không dùng: `Bill.UI`, `Bill.Config`, Network/Fusion, `DynamicAnimationEventHub`, mọi attribute của BillInspector.

### Outline (`Assets/_Project/Art/Rendering/BillSSOutline`)

Đây là bản fork của `github.com/billtruong003/Bill-SSOutline @ 2acf5b72`. Header ghi rõ không gộp vào Toon Kit.

| # | Vấn đề | Bằng chứng |
|---|---|---|
| O1 | Không có asmdef, đang nằm trong Assembly-CSharp | `Mobile_Renderer.asset` |
| O2 | Bốn móc vào code game: `OutlineLayers`, `OutlineLook`, `OutlineCameraWidth`, `GraphicsTier` | `BillOutlineFeature.cs:25,240-251,282,287,363,369` |
| O3 | Include đường dẫn cứng `Assets/_Project/...OutlineAlphaMask.hlsl` | `EnvSolid.shader:131`, `VAT_EnemyToon.shader:236` |
| O4 | `Outline.shader` có khoảng 768 variant và nằm trong Always Included | `Outline.shader:38-45`, `GraphicsSettings.asset:37-39` |
| O5 | Bug tiềm ẩn: tắt occlusion lúc runtime thì composite dùng texture handle cũ | `BillOutlineFeature.cs:82,330,415-416` |
| O6 | Khi outline ẩn vẫn chạy pass và xin input; closure cấp phát mỗi frame; keyword set bằng string | `:175-184,240,260-273` |
| O7 | Code chết: `InvisibleOccluder.shader`, event `OnRenderFoliageMask`, tag `LightweightForward` | — |
| O8 | Chỉ chạy Render Graph (URP 17+), không có fallback | — |

### Stylized Toon World Kit

- Unity đang dùng bản **embedded** trong `Packages/`. Bản này che git URL trong `manifest.json`.
- Bản embedded đã lệch upstream: patch `Core/URPCompat.hlsl` ở commit 79dc2c78f chưa được đẩy ngược lên repo gốc.
- Kit có outline riêng (`ScreenSpaceOutlineFeature`) nhưng không renderer nào dùng.

## 2. Đề xuất repo và package

### Repo 1: `bill-gamecore` (repo mới, monorepo, framework lớn)

Một repo, nhiều package UPM, cài bằng `?path=`:

| Package | Nội dung | Phụ thuộc |
|---|---|---|
| `com.bill.core` | ServiceLocator, `Bill` facade, EventBus, runner, Timer, Save, Trace | — |
| `com.bill.tween` | BillTween, Ease, Sequence (+ shortcut uGUI) | core, ugui |
| `com.bill.pool` | PoolService, IPoolable | core |
| `com.bill.audio` | AudioService, AudioLibrary | core |
| `com.bill.flow` | SceneService, GameStateMachine, BillStartup (loading/transition) | core, tween, TMP |
| `com.bill.devtools` | DevTools/Cheat console, define `BILL_CHEATS` | core, pool, flow |
| `com.bill.inspector` | BillInspector (đã có dạng package, 39 test) | — |
| `com.bill.editortools` | BillFav + BillSceneSwitcher (chỉ Editor) | — |
| `com.bill.net.fusion` | NetworkService + Fusion, bật bằng `versionDefines` | core, Fusion |
| `com.bill.mobile-input` | BillVirtualJoystick | ugui |

Monorepo là lựa chọn hợp lý vì các package chia sẻ `core` và nên lên version cùng lúc.

### Repo 2: `Bill-SSOutline` (repo có sẵn, cập nhật)

- Package: `com.billtruong.ss-outline`.
- Đưa bản fork của game ngược lên repo gốc.
- Thay bốn móc O2 bằng điểm mở rộng: layer mask serialize, `Func<bool>` để bật normals, struct override tĩnh, component width theo camera.
- `OutlineLayers` và `OutlineLook` ở lại game và gán vào các điểm mở rộng đó.

### Repo 3: `stylized-toon-world-kit` (repo có sẵn, đồng bộ)

- Đẩy patch `URPCompat.hlsl` lên upstream.
- Ghi chú hoặc bỏ outline cũ của kit, vì Bill-SSOutline đã thay nó.
- Thêm README, CHANGELOG, LICENSE.

### Để sau khi ship

| Repo | Nội dung | Ghi chú |
|---|---|---|
| `bill-vat` | `Assets/ThirdParty/VAT` | đã có asmdef riêng, dễ tách |
| `bill-render-kit` | PlanarShadows, ToonBloom, ToonPointLights, GraphicsTier | phải gỡ phụ thuộc `GameSettings` trước |
| `bill-mobile-services` | ProtectedSave, ApiClient/RemoteConfig/CloudSave, AdService, IAP | còn dính `PlayerProfile` và `GameAnalytics` |

Những phần không nên tách: Gacha, Radio call, UiShot. Các phần này gắn quá chặt với game.

**Tổng:** làm ngay 3 repo (1 mới, 2 có sẵn), gồm 11 package. Sau khi ship thêm 3 repo.

## 3. Thứ tự làm

1. **Sửa trong game trước, chưa tách.** Làm B1–B9 và O1–O7, giữ nguyên GUID, rồi chạy test EditMode cùng một lần Play từ `Bootstrap.unity`.
2. **Tái cấu trúc nội bộ.** Tách `Interfaces.cs` theo module. `Bill.cs` chuyển sang mô hình module tự đăng ký (B10), nhưng `Bill.X` giữ nguyên để game không phải sửa.
3. **Chuyển sang `Packages/` dạng embedded.** Copy luôn file `.meta`, sửa include `Packages/...`, kiểm tra compile, renderer asset và volume profile.
4. **Đẩy lên git**, đổi `manifest.json` sang git URL, thêm `testables`.
5. **Đóng gói `.tgz`/zip** cho từng package (`npm pack` hoặc `Client.Pack`) để lưu offline.
6. **Viết docs.** Mỗi package có README, CHANGELOG và `Documentation~`. Cập nhật `Docs/FRAMEWORK.md` và skill `.claude/skills/billgamecore` (đổi đường dẫn, bỏ chữ "zero-alloc", sửa mô tả tick).

## 4. Owner cần chốt

1. Monorepo `bill-gamecore`, hay mỗi package một repo?
2. Scope tên: `com.bill.*` (đang dùng) hay `com.billtruong.*` (Toon Kit đang dùng)?
3. Repo public hay private?
4. Giữ package Fusion hay bỏ hẳn?
