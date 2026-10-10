# Basic Progression

Sample của Dreamy Progression. Import từ Window > Package Manager > Dreamy Progression > Samples > Import. Unity chép nội dung vào Assets/Samples/Dreamy Progression/0.1.1/Basic Progression/.

## Cấu trúc và tích hợp

Giữ nguyên folder, .meta, asmdef và reference prefab khi chuyển vào project. Chỉ giữ một bản script/asmdef và một JSON cho mỗi key Resources/DataConfig. Bootstrap config/save/wallet/audio tại GameInstaller trước khi bật UI, theo [README package](../../README.md). Link tương đối này dùng trong source package; sau import, mở README package từ Package Manager.

## Sử dụng và sample

AdvanceNext(grantId) dùng cho Sequential; GrantExperience(grantId, amount) dùng cho Experience. Retry cùng một completion phải giữ grantId. Level bắt đầu từ 1, không có khoảng trống; XP threshold tăng dần, level đầu threshold 0. Catalog/level ID quyết định save/reward; đổi catalogId khởi tạo tiến trình mới. Sample có UI/ProgressionHud.prefab: đặt dưới safe area và Bind(IProgressionService). HUD không phải UIPanel, không tự quyết định thắng màn hoặc cấp XP.


Assembly Dreamy.Feature.Progression.Integration.Runtime reference Dreamy.Progression.Runtime, Unity.TextMeshPro, UnityEngine.UI.

## Addressables cho HUD/overlay

ProgressionHud không phải UIPanel. Có thể đặt trực tiếp dưới Canvas và bind/initialize như hướng dẫn trên. Nếu cần tải theo yêu cầu:

1. Đưa prefab của game vào Addressables Group, ví dụ UI Widgets.
2. Đặt Address là UI/ProgressionHud.prefab và khai báo constant tương ứng trong class UIAddress.
3. Dùng AssetLoader.LoadAsync<GameObject>(address), instantiate dưới Canvas rồi Bind hoặc Initialize với service/registry cần thiết.
4. Destroy instance khi kết thúc, chỉ unload cache khi không còn consumer. Build content trước khi thử player.

Luồng này cần Dreamy Assets và UniTask ở game. Không truyền type HUD/overlay vào PanelManager.Show<T>(), vì API đó yêu cầu UIPanel subclass.

## Interactive demo panel

`UI/ProgressionDemoPanel.prefab` contains the existing ProgressionHud plus Advance and Back controls. Instantiate under PanelManager, await Init/PostInit, call Configure(hostProgressionService), then await Show. The sample chooses Complete Stage for Sequential or Grant 100 XP for Experience, uses a fresh grant ID per deliberate click, and disables advancement at max level. Services, config and rewards remain host-owned. Requires Dreamy UI and UniTask alongside the existing sample references.
