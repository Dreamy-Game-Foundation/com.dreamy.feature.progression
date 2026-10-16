# Local source release 0.2.1 — 2026-10-16

Annotated tag `v0.2.1` identifies the reviewed source, not production certification.
Fresh validation: targeted Unity EditMode 133/133 passed; Economy/Shop managed checks 88 passed; Progression managed checks 59 passed
(one Unity-only case is excluded by that harness). Connectivity clean runtime-only Unity consumer
and Android/iOS C# branch compiles passed. A separate clean consumer compiled all four packages
and their unmodified UI samples, validating required shortage bindings and prefab scripts.
Five tests in the unchanged Dreamy UI suite failed in the full-project EditMode run;
full-project green and native/device readiness are not certified. See the sandbox review report
`docs/evidence/resource-recovery-release/REVIEW.md` for Unity suite results and open gates.
The earlier implementation notes below describe historical validation and pre-tag working state.

---

# 0.2.1 — Shared inventory sample

Date: 2026-10-16. Requires Economy 0.5.0; working tree, not tagged/published.
Progression Rewards now routes every resource kind to one DatasaveResourceWallet. Legacy sample
inventory files are imported once, including original delivery receipts. XP/journal persistence
and public Progression runtime contracts are unchanged from 0.2.0.

---

# Ghi chú phiên bản 0.2.0

Ngày: 2026-10-16. Phiên bản trước: 0.1.2. Chưa tag/publish.

Progression hỗ trợ nhiều reward cho mỗi milestone qua Economy bundle/coordinator/journal.
XP/level commit trước delivery; UI query pending/blocked/delivered và retry phần chưa ack.
Save v2 hỗ trợ restart recovery, payload snapshot và migration bảo thủ từ v1.

## Consumer migration

1. Rebuild consumer với Economy 0.4.0 và Progression 0.2.0; giữ stable save key/level IDs.
2. Cung cấp IRewardDestination cho wallet/inventory; mutation + receipt cùng owner save.
   Constructor wallet cũ compile tiếp, nhưng bool-only wallet không tự phát reward strict.
3. Trên bản copy save v1, đối soát legacy IDs qua IProgressionLegacyRewardMigration.
   Không cấp lại reached levels hoặc giả định missing/truncated legacy receipt là chưa nhận.
4. Sau khi các owner sẵn sàng, gọi RetryPendingRewards. Khi RequiresReload, reload trước mutation.
5. UI coi advance thành công là XP đã commit; xem reward state riêng. Reward failure không còn
   trả RewardGrantFailed và rollback XP như behavior v1. Enum cũ giữ numeric values.
6. Unregister cả hai service interface và dispose presenter khi teardown. Không dùng hai writer.

Reward config legacy vẫn đọc được. Thay catalog structure/occurrence cần explicit migration;
không tự reset save. Không prune receipts/history tùy tiện. Chưa chứng nhận backend/cloud,
restore nhiều save khác generation hoặc player/device; xem README để biết contract và giới hạn.

Evidence kiểm chứng của lần triển khai này nằm trong docs/evidence của sandbox.
