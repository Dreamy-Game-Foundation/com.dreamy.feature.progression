# Dreamy Progression

Dreamy Progression provides persistent, idempotent player progression for two
catalog modes:

- `Sequential`: the host calls `AdvanceNext(grantId)` after a game-defined
  completion, such as winning a stage.
- `Experience`: the host calls `GrantExperience(grantId, amount)` and the
  package unlocks every reached level.

The feature owns catalog validation, progression state, idempotent grant IDs,
and optional economy milestone delivery. The host owns gameplay completion,
UI, localization, assets, and SDK integrations.

## Installation

Register the catalog before DataConfig initialization, then install only after
Datasave and the persistent `IResourceWallet` are ready:

```csharp
ProgressionInstaller.RegisterConfig(dataConfigService);
await dataConfigService.InitializeAsync();

IProgressionService progression = ProgressionInstaller.Install();
```

`progressionCatalog.json` must contain a stable `catalogId` and levels numbered
from 1 without gaps. Experience catalogs start at threshold 0 and use strictly
increasing `requiredExperience`. Sequential catalogs use no experience
thresholds.

Milestone reward transaction IDs are derived from catalog and level IDs. Keep
those IDs stable after release. A changed `catalogId` deliberately starts a new
progression save for the configured save key.

## Sample

Import **Basic Progression** from Package Manager. It is a skeleton, not a
copy-ready UI prefab. Replace its sample JSON and use the game-owned persistent
wallet in production.
