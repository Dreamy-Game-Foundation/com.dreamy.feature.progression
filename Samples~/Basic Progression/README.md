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

## Tích hợp vào game và vòng đời presenter

Điểm tích hợp có thể chỉnh sửa là `ProgressionFeatureInstaller` trong Samples~. Runtime `ProgressionInstaller` vẫn dành cho UI tự viết; game dùng view đi kèm chỉ gọi feature installer. Các presenter triển khai vòng đời `IPanelPresenter` độc lập engine trong `Dreamy.UI.Presentation`.

```csharp
ProgressionFeatureInstaller.RegisterConfig(dataConfig); // Trước dataConfig.InitializeAsync.
// Sau khi config/save/wallet sẵn sàng, dùng factory chung của các tính năng:
ProgressionFeatureInstaller.Install(factory, config, save, wallet);
// Hoặc dùng lại service do game sở hữu: ProgressionFeatureInstaller.Install(factory, service);
```

Các dependency trong ví dụ do composition root sở hữu. Installer không tự tạo wallet/save trong bộ nhớ để dự phòng. Model/service quản lý reward và checkpoint; view chỉ render trạng thái và gửi yêu cầu. Thêm reference trực tiếp tới assembly tích hợp và Dreamy.UI.Presentation tại nơi dùng API tương ứng.

ProgressionHud là IProgressionView thụ động, không phải UIPanel. Gọi `ProgressionFeatureInstaller.Attach(factory, hud)` sau khi tạo HUD để gắn PresenterViewHost chung quản lý show/disable/destroy. ProgressionPresenter sở hữu subscription, view không tự bind. GameInstaller của sandbox gắn HUD trong scene và giải phóng host khi teardown.

Lệnh kiểm chứng sandbox: `python3 LocalPackages/com.dreamy.feature.settings/Tests~/validate-settings.py --shop --features`. Lệnh compile các assembly runtime/tích hợp/sample theo reference đã khai báo và chạy regression model/presenter bằng managed code. Scene, coroutine, raycast và vòng đời Unity vẫn cần kiểm chứng Editor/PlayMode.
