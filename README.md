# Dreamy Progression

Package thuộc Dreamy Game Studio. Hướng dẫn dưới đây mô tả cấu trúc, cách cài vào project và tích hợp ở root/scene.

## Cài package

Dùng Unity 6000.0 trở lên. Sandbox đã tham chiếu package bằng `file:../LocalPackages/com.dreamy.feature.progression`. Project khác dùng Package Manager > + > Install package from disk và chọn package.json, hoặc Git URL của repository nội bộ. Cài cả dependency Dreamy/Git vào manifest của game; version dependency không tự cấu hình registry riêng.

Dependency trực tiếp theo package.json:

- `com.dreamy.core` (1.1.2)
- `com.dreamy.dataconfig` (0.2.0)
- `com.dreamy.datasave` (0.2.0)
- `com.dreamy.feature.economy` (0.3.0)
- `com.unity.nuget.newtonsoft-json` (3.2.1)

## Cấu trúc và asmdef

| Assembly | Reference | Phạm vi |
| --- | --- | --- |
| `Dreamy.Progression.Runtime` | Dreamy.Core.Runtime, Dreamy.DataConfig.Runtime, Dreamy.Datasave.Runtime, Dreamy.Economy.Runtime, Unity.Newtonsoft.Json | Runtime |

Trong asmdef của game, thêm assembly chứa API trực tiếp sử dụng. Code bootstrap reference thêm Core/DataConfig/Datasave/Economy theo nhu cầu; code async reference UniTask. Code gọi type sample reference assembly sample. Giữ Editor reference trong asmdef Editor-only.

## Cấu trúc và trách nhiệm

Runtime/Config chứa catalog; Contracts chứa service/view và adapter; Domain chứa quy tắc và state; Installation chứa installer; Persistence xử lý tiến trình lưu. Presentation (nếu có) nối service với view. Samples~ là integration được import vào Assets; game sở hữu UI, gameplay, localization và adapter SDK.

## Cài service ở GameInstaller

Dùng một DataConfig và Datasave dùng chung. Ghép đoạn dưới vào root async; không tạo lại các service trong panel. Cài và đăng ký IResourceWallet dùng chung trước feature; wallet giữ transaction ID ổn định.

```csharp
using Dreamy.Core;
using Dreamy.DataConfig;
using Dreamy.Progression;

// dataConfig: instance root đã tạo, chưa initialize.
ProgressionInstaller.RegisterConfig(dataConfig);
await dataConfig.InitializeAsync(cancellationToken);
ServiceLocator.Register<IDataConfigService>(dataConfig);
// IDatasaveService và wallet (nếu cần) đã đăng ký trước đây.
IProgressionService service = ProgressionInstaller.Install();
```

Với nhiều feature, gọi tất cả RegisterConfig trước một InitializeAsync, rồi mới gọi Install cho từng feature. JSON cần có đúng một Resources/DataConfig/progressionCatalog.json. Root unregister IProgressionService khi teardown; dispose presenter/subscription theo lifecycle UI.

## Sử dụng và sample

AdvanceNext(grantId) dùng cho Sequential; GrantExperience(grantId, amount) dùng cho Experience. Retry cùng một completion phải giữ grantId. Level bắt đầu từ 1, không có khoảng trống; XP threshold tăng dần, level đầu threshold 0. Catalog/level ID quyết định save/reward; đổi catalogId khởi tạo tiến trình mới. Sample có UI/ProgressionHud.prefab: đặt dưới safe area và gọi ProgressionFeatureInstaller.Attach(factory, hud). HUD không phải UIPanel, không tự quyết định thắng màn hoặc cấp XP.

## Import sample

Mở Window > Package Manager, chọn Dreamy Progression > Samples > Import. Unity chép vào Assets/Samples/Dreamy Progression/0.1.1/. Chuyển cả folder nếu tùy biến, giữ .meta và reference prefab; không giữ bản script/asmdef hoặc Resources document trùng.

- **Basic Progression**: nguồn `Samples~/Basic Progression`.
  Assembly `Dreamy.Feature.Progression.Integration.Runtime` reference Dreamy.Progression.Runtime, Unity.TextMeshPro, UnityEngine.UI.

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
