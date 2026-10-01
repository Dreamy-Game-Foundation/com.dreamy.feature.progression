# Basic Progression

This is a skeleton integration sample. It contains static configuration only;
the consuming game owns its gameplay win condition and any UI.

Before `IDataConfigService.InitializeAsync`, call:

```csharp
ProgressionInstaller.RegisterConfig(dataConfigService);
```

After config initialization, Datasave setup, and `IResourceWallet` registration,
install the feature from the composition root:

```csharp
IProgressionService progression = ProgressionInstaller.Install();
ProgressionAdvanceResult result = progression.AdvanceNext("stage-1-win");
```

`grantId` must be stable for a single gameplay completion so retries do not
advance twice. In `Experience` mode, call `GrantExperience(grantId, amount)`
instead. The package validates progression order; the host is responsible for
deciding when the player has won a stage or earned experience.

## UI HUD

`UI/ProgressionHud.prefab` is a TMP/UGUI sample that receives the service through
`Bind(IProgressionService)`. It renders `Level current/max`; its XP bar uses an
`Image.fillAmount` and is only visible for `Experience` catalogs. Place the
prefab under the game-owned Safe Area container, then bind it from the host
composition root. The HUD never decides progression or writes save data, and
unregisters its event on destroy.
