# Progression Rewards

Import cả sample từ Package Manager. Trong một scene trống, tạo GameObject và Add Component
`ProgressionRewardsDemo`, rồi Play. UI diagnostic dùng IMGUI, giới hạn trong Screen.safeArea;
không cần prefab hoặc dependencies lên UI game. Sample dùng riêng directory
`DreamyProgressionRewardsDemo` trong Application.persistentDataPath.

Config `Resources/progressionRewardsCatalog.json` có 8 level (threshold cách nhau 100), level 4
không reward; các milestone khác cấp Coin/Gem/Card/Chest/Cosmetic. Không import Resources trùng
tên. Sample có namespace/assembly riêng, không tham chiếu code game hoặc Basic Progression.

## Thử recovery

1. Bấm **Fail next Card delivery**, rồi **Grant 300 XP**. XP lên 300/level 4; rank-2 có
   Coin/Gem acknowledged, Card pending. Milestone rank-3 độc lập vẫn có thể hoàn tất.
2. Bấm **Retry rank-2** hoặc **Retry pending**. Coin/Gem không cộng lại.
3. Trước một grant tiếp theo, arm **Fail journal ack**. Currency destination đã lưu effect + receipt,
   Progression ack save lỗi. Model yêu cầu reload; bấm **Reload owners and recover**, hoặc restart
   app/Play Mode. Retry dùng receipt cũ, balance không tăng lần nữa.
4. Arm **Fail final reward completion save**, grant qua milestone mới. Các entry đã ack nhưng row
   còn Pending. Reload owners sẽ hoàn tất Completed mà không cấp lại.
5. Stop/Play hoặc restart app giữ XP, journals, balances và inventory. Reload owners gọi recovery
   sau wiring; constructor không tự delivery. Sample không có reset tự động để giữ evidence.

Khi đã max level, thử trên save directory demo mới bằng cách thay DirectoryName trong sample
copy của bạn. Không xóa save thật của game để thử sample.

## Thay adapter cho game

Economy `CreateRewardDestination` dùng cùng wallet cho currency/card/chest/cosmetic.
Card/chest cộng quantity; cosmetic đã có giữ owned=1 và vẫn lưu receipt cho entitlement mới. Chest không
mở loot. Target IDs được whitelist tại composition root. Owner gặp save error trở thành uncertain
cho đến khi reload owners; không tiếp tục dùng in-memory state cũ như bằng chứng chưa giao.

Game đăng ký ResourceRewardDefinition và dùng cùng wallet đã cài cho Shop/Daily/Missions.
Có thể thay bằng adapters tới inventory riêng khi game cần. Protocol nằm trong Economy;
Progression không biết class Card/Chest/Cosmetic. Event/render UI thật có thể dùng
IProgressionRewardService + ProgressionRewardPresenter; sample query snapshot mỗi lần render.

Đối với v1 save của game, cần explicit IProgressionLegacyRewardMigration; sample chỉ tạo save
mới, không tự tin tưởng legacy receipts. Chi tiết migration và guarantee trong README package.


Từ 0.2.1, tất cả quantity/ownership + delivery receipts được ghi vào một save `wallet`.
`DemoInventorySave` chỉ còn là DTO migration đọc các file `progression-rewards-card/chest/cosmetic`
cũ. ImportInventory giữ marker trong wallet để không cộng lại khi reload; không xóa file nguồn.
