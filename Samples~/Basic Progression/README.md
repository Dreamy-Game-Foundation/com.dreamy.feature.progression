# Basic Progression

Sample của Dreamy Progression. Import từ Window > Package Manager > Dreamy Progression > Samples > Import. Unity chép nội dung vào Assets/Samples/Dreamy Progression/0.1.1/Basic Progression/.

## Cấu trúc và tích hợp

Giữ nguyên folder, .meta, asmdef và reference prefab khi chuyển vào project. Chỉ giữ một bản script/asmdef và một JSON cho mỗi key Resources/DataConfig. Bootstrap config/save/wallet/audio tại GameInstaller trước khi bật UI, theo [README package](../../README.md). Link tương đối này dùng trong source package; sau import, mở README package từ Package Manager.

## Sử dụng và sample

AdvanceNext(grantId) dùng cho Sequential; GrantExperience(grantId, amount) dùng cho Experience. Retry cùng một completion phải giữ grantId. Level bắt đầu từ 1, không có khoảng trống; XP threshold tăng dần, level đầu threshold 0. Catalog/level ID quyết định save/reward; đổi catalogId khởi tạo tiến trình mới. Sample có UI/ProgressionHud.prefab: đặt dưới safe area và gọi ProgressionFeatureInstaller.Attach(factory, hud). HUD không phải UIPanel, không tự quyết định thắng màn hoặc cấp XP.


Assembly Dreamy.Feature.Progression.Integration.Runtime reference Dreamy.Progression.Runtime, Unity.TextMeshPro, UnityEngine.UI.

## Addressables cho HUD/overlay

ProgressionHud không phải UIPanel. Có thể đặt trực tiếp dưới Canvas và bind/initialize như hướng dẫn trên. Nếu cần tải theo yêu cầu:

1. Đưa prefab của game vào Addressables Group, ví dụ UI Widgets.
2. Đặt Address là UI/ProgressionHud.prefab và khai báo constant tương ứng trong class UIAddress.
3. Dùng AssetLoader.LoadAsync<GameObject>(address), instantiate dưới Canvas rồi gọi ProgressionFeatureInstaller.Attach(factory, hud).
4. Destroy instance khi kết thúc, chỉ unload cache khi không còn consumer. Build content trước khi thử player.

Luồng này cần Dreamy Assets và UniTask ở game. Không truyền type HUD/overlay vào PanelManager.Show<T>(), vì API đó yêu cầu UIPanel subclass.

## Production integration and presenter lifecycle

The editable integration entry point is `ProgressionFeatureInstaller` in Samples~. Runtime `ProgressionInstaller` remains available for custom UI; games using the supplied views call only the feature installer. All presenters implement the engine-independent `IPanelPresenter` lifecycle in `Dreamy.UI.Presentation`.

```csharp
ProgressionFeatureInstaller.RegisterConfig(dataConfig); // Before dataConfig.InitializeAsync.
// After config/save/wallet readiness, using the same factory as other features:
ProgressionFeatureInstaller.Install(factory, config, save, wallet);
// Or reuse a host-owned service: ProgressionFeatureInstaller.Install(factory, service);
```

Dependencies in this example belong to the composition root. No installer creates an in-memory wallet/save fallback. Model/service own rewards and checkpoints; views only render state and emit intent. Add direct asmdef references to the integration assembly and Dreamy.UI.Presentation wherever their APIs are used.

ProgressionHud is a passive IProgressionView, not a UIPanel. Call `ProgressionFeatureInstaller.Attach(factory, hud)` after creating the HUD. It adds the shared PresenterViewHost to handle show/disable/destroy without a feature-specific controller. Presenter subscriptions belong to ProgressionPresenter, never to the view. The sandbox GameInstaller attaches scene HUDs and releases their hosts at teardown.

Sandbox validation: `python3 LocalPackages/com.dreamy.feature.settings/Tests~/validate-settings.py --shop --features`. This compiles runtime/integration/sample assemblies against their declared references and runs pure managed model/presenter regressions. Unity scene/coroutine/raycast lifecycle still requires Editor/PlayMode validation.
