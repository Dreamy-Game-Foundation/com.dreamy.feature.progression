# Dreamy Progression

Package thuộc Dreamy Game Studio. Hướng dẫn dưới đây mô tả cấu trúc, cách cài vào project và tích hợp ở root/scene.

## Cài package

Dùng Unity 6000.0 trở lên. Sandbox đã tham chiếu package bằng `file:../LocalPackages/com.dreamy.feature.progression`. Project khác dùng Package Manager > + > Install package from disk và chọn package.json, hoặc Git URL của repository nội bộ. Cài cả dependency Dreamy/Git vào manifest của game; version dependency không tự cấu hình registry riêng.

Dependency trực tiếp theo package.json:

- `com.dreamy.core` (1.1.2)
- `com.dreamy.dataconfig` (0.2.0)
- `com.dreamy.datasave` (0.2.0)
- `com.dreamy.feature.economy` (0.5.0)
- `com.unity.nuget.newtonsoft-json` (3.2.1)
- `com.dreamy.ui` (0.2.0)
- `com.cysharp.unitask` (2.5.10)

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

AdvanceNext(grantId) dùng cho Sequential; GrantExperience(grantId, amount) dùng cho Experience. Retry cùng một completion phải giữ grantId. Level bắt đầu từ 1, không có khoảng trống; XP threshold tăng dần, level đầu threshold 0. Catalog/level ID quyết định save/reward; đổi catalogId/threshold/thứ tự level yêu cầu host migration, không tự reset save. Sample có UI/ProgressionHud.prefab: đặt dưới safe area và gọi ProgressionFeatureInstaller.Attach(factory, hud). HUD không phải UIPanel, không tự quyết định thắng màn hoặc cấp XP.

## Import sample

Mở Window > Package Manager, chọn Dreamy Progression > Samples > Import. Unity chép vào Assets/Samples/Dreamy Progression/0.2.0/. Chuyển cả folder nếu tùy biến, giữ .meta và reference prefab; không giữ bản script/asmdef hoặc Resources document trùng.

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


## Multi-reward milestones (0.2.0)

`AdvanceNext` và `GrantExperience` commit XP/level, grant ID và **tất cả** milestone intents
vào cùng Progression document trước khi gọi destination. Advance thành công không đồng nghĩa
reward đã nhận xong. Reward lỗi không rollback XP/level. Grant ID được giữ không giới hạn;
tham số `processedGrantHistoryLimit` ở constructor cũ chỉ còn để source compatibility và
kiểm tra đối số, không cắt history. Game phát cùng completion phải giữ cùng grant ID.

Config legacy `rewardResourceId`/`rewardAmount` tiếp tục đọc được. Config mới dùng:

```json
{
  "id": "rank-2", "level": 2, "requiredExperience": 100,
  "bundle": {
    "schemaVersion": 1, "resolverVersion": "progression-static-v1",
    "entries": [
      { "entryId": "coins", "kindId": "currency", "targetId": "currency.coin", "amount": 100 },
      { "entryId": "card", "kindId": "card", "targetId": "card.knight", "amount": 2 }
    ]
  }
}
```

Không khai báo cả legacy fields và bundle. Level đầu không reward; level khác có thể không
reward. Bundle khai báo phải không rỗng, amount dương, entry IDs duy nhất trong bundle.
`Reward` chỉ dành cho config legacy; dùng `GetBundle()` với bundle.
Kind/target là strings, không phụ thuộc enum hoặc class của game. Coin và Gem dùng cùng
kind `currency`, khác target IDs; Card/Chest/Cosmetic dùng các destination game cung cấp.
Cấp Chest là cấp chest item, không mở hay roll loot. Cosmetic trùng cần policy của owner.

### Wiring và recovery

```csharp
// wallet implements IRewardDestination; other owners are supplied by the game.
var gateway = new RewardDeliveryGateway(new IRewardDestination[]
{
    wallet, cardDestination, chestDestination, cosmeticDestination
});
ProgressionModel model = ProgressionInstaller.Install(
    config, datasave, gateway, saveKey: "profile-42-progression");
// Only after all owners are ready; construction and GetState never deliver.
model.RetryPendingRewards();
IProgressionRewardService rewards = model;
var rows = rewards.GetRewardStates();
rewards.RetryReward("rank-2");
```

Destinations implement Economy `IRewardDestination`. Validate không mutate và kiểm tra tổng
amount; Deliver lưu effect + transaction receipt có fingerprint trong cùng owner document.
Cùng ID/payload trả AlreadyDelivered, ID trùng payload khác trả PayloadMismatch. Không prune
receipt khi progression còn có thể replay. Package không tạo inventory giả cho game.
Constructor IResourceWallet cũ tự tạo currency gateway nếu wallet triển khai IRewardDestination;
wallet bool-only để entitlement pending DestinationUnavailable đến khi có adapter strict.
Installer đăng ký cả IProgressionService và IProgressionRewardService; root unregister cả hai
khi teardown. Không chạy đồng thời hai model writer trên cùng save key.

Mỗi milestone có Economy RewardJournal riêng. Coordinator skip entry đã ack; effect đã lưu
nhưng ack chưa lưu được gửi lại cùng transaction ID. Destination receipt chống lặp effect.
All ack nhưng final Completed chưa lưu vẫn là Pending. UI `GetRewardStates()` trả snapshot
immutable: milestone, từng entry/ack, status Pending/Blocked/Delivered, reason, retryability,
CanRetry và IsBusy. Các milestone chưa unlock không xuất hiện trong danh sách; game có thể
kết hợp catalog để vẽ hàng Locked. Không reward không sinh journal rỗng.
`ProgressionRewardPresenter` + `IProgressionRewardView` bổ sung UI; HUD/presenter cũ vẫn dùng.
Event chỉ phát sau commit và listener lỗi không đảo kết quả commit. Dispose presenter để unbind.

Khi Progression Save ném lỗi, `RequiresReload` bật và chặn mọi mutation/delivery tiếp theo:
write có thể đã hoàn tất trên disk. Gọi `Reload()` để reconcile state, rồi retry pending.
Nếu lỗi từ owner inventory/wallet có trạng thái persistence uncertain, reload owner đó theo
contract của game trước khi retry. Không tự retry mỗi frame. UnknownKind/Target, invalid journal
hoặc payload mismatch giữ entitlement Blocked; sửa wiring/migration rồi retry thủ công.
Lỗi một milestone không chặn milestone độc lập khác; lỗi progression persistence dừng batch.

### Save v1 → v2 và giới hạn

V2 giữ XP, level và processed grant IDs v1; lưu level structure, occurrence, payload snapshots
và journals. Các level đã đạt trong v1 được giữ làm legacy baseline, không cấp lại. Catalog ID
legacy khác hoặc XP/level không nhất quán cần migration của game, không tự reset.

V1 cấp reward trước khi lưu level, nên level chưa đạt trong save có thể đã được cấp. Những
entitlement unlock sau migration bắt buộc đối soát qua IProgressionLegacyRewardMigration:

- `AlreadyDelivered`: host xác nhận legacy entitlement đã nhận, hoặc áp dụng explicit trust policy.
- `NotDelivered`: host có evidence/history đầy đủ chứng minh chưa nhận.
- `Unverifiable`/không có adapter: Blocked LegacyReceiptUnverifiable, không tự cấp bằng ID mới.

Legacy ID là `progression:{catalogId}:level:{levelId}`. Receipt legacy không có fingerprint
không chứng minh payload; absence trong bounded history không chứng minh chưa cấp. Migration
adapter là game-owned; không đọc private wallet fields. Quyết định được persist trước delivery
và survive restart. Fresh save không bị legacy gate. Lịch sử grant đã bị v1 prune không thể
khôi phục bằng migration.

Request identity mới gồm save-key scope, feature, catalog, occurrence và milestone; entry ID
ổn định quyết định transaction ID. `occurrenceId` mặc định `permanent`, phải giữ nguyên qua
restart. Đổi occurrence chỉ dùng với progression save mới do game chủ động tạo. Pending reward
luôn dùng snapshot cũ dù reward config đã đổi. Catalog cấu trúc/mode/occurrence không khớp bật
RequiresMigration để chặn XP mới; pending payload hợp lệ vẫn recovery được.

Guarantee cần durable receipt contract và cùng save lineage. Không có atomic transaction
xuyên wallet/inventory/Progression: UI có thể thấy một phần bundle đã nhận. Restore riêng từng
file từ backup/generation khác nhau hoặc cloud merge cần policy riêng. Datasave có backup
fallback chung; host cần ngăn downgrade/future-version restore theo chính sách save của mình.
Fingerprint không chống sửa save. Random resolution, backend authority và chest opening nằm
ngoài phạm vi package này.

### Kiểm chứng

- Managed: `python3 LocalPackages/com.dreamy.feature.progression/Tests~/validate-progression.py`.
  Compile runtime/sample/test source với dependencies Unity đã generate; chạy domain tests.
- Unity: PlayMode assembly `Dreamy.Progression.Runtime.Tests`, bao gồm Datasave disk envelope
  migration fixture; chạy Economy RewardContractTests/RewardJournalTests khi đổi protocol chung.
- Sample **Progression Rewards**: hướng dẫn fault injection, retry và restart trong README đi kèm.


### Shared inventory sample (0.2.1)

Progression Rewards hiện tạo một DatasaveResourceWallet với các ResourceRewardDefinition
Card/Chest/Cosmetic. Currency và ba destination adapter cùng ghi `wallet`, không tạo inventory
file riêng. Save `progression` chỉ giữ XP/level và journals. Các file demo inventory cũ được
đọc một lần bằng ImportInventory, giữ receipts và marker để restart không cộng trùng.
Game có thể dùng chính wallet chung của Shop/Daily/Missions thay vì tạo thêm owner.
