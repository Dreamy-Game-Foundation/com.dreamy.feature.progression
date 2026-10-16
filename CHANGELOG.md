# Changelog

## [0.2.1] - 2026-10-16

- Consolidate the current implementation for a local source release; clean UI consumer checks passed; native/mobile gates and the full-project UI test failures remain open.

## 0.2.1

- Update Progression Rewards to Economy 0.5.0 shared inventory owner; all five kinds use one wallet save.
- Import legacy sample inventory counts and original receipts once with durable migration markers.


## 0.2.0 — 2026-10-16

- Add concrete multi-entry milestone bundles using Economy resolved rewards, coordinator and journals.
- Commit XP/level and all reward intents atomically within the Progression document before delivery.
- Persist per-entry acknowledgements, support retry/startup recovery, and isolate delivery failures from advancement.
- Add reward query/retry service, immutable UI states and optional reward presenter.
- Migrate save v1 to v2 without replaying reached milestones; require explicit legacy reconciliation for uncertain prior grants.
- Retain XP grant history without truncation and require reload after ambiguous progression persistence failures.
- Preserve legacy reward config and wallet constructor; bool-only wallets need a durable reward destination adapter.
- Add a persistent five-kind reward sample with failure injection and domain/disk regression tests.
- Require Economy 0.4.0; reject incompatible catalog structure instead of silently resetting progress.

## Earlier unreleased work

- Add an editable feature installer that registers services and presenters with the shared UI factory.
- Keep supplied views passive and delegate presenter lifetime to the shared host; update sample integration documentation.


## 0.1.1

- Added the `ProgressionHud` TMP/UGUI sample prefab.
- Replaced the sample XP slider with an `Image.fillAmount` fill image.

## 0.1.0

- Added sequential and experience-based persistent progression.
- Added DataConfig catalog validation, idempotent grant IDs, and optional
  Economy milestone rewards.
- Added Runtime tests and a skeleton configuration sample.
