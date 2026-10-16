using System;
using System.Collections.Generic;
using Dreamy.Datasave;
using Dreamy.Economy;
using Dreamy.Economy.Rewards;

namespace Dreamy.Progression
{
    public sealed partial class ProgressionModel : IProgressionService, IProgressionRewardService
    {
        private readonly ProgressionCatalogConfig catalog;
        private readonly IDatasaveService datasave;
        private readonly IRewardDeliveryGateway gateway;
        private readonly IProgressionLegacyRewardMigration legacyMigration;
        private readonly string saveKey;
        private readonly string occurrenceId;
        private ProgressionSaveData save;
        private bool busy;
        private bool publishing;
        public bool RequiresReload { get; private set; }
        public bool RequiresMigration { get; private set; }

        // Kept for source compatibility. Grant IDs are now retained without truncation.
        // A bool-only wallet cannot provide durable per-entry delivery guarantees.
        public ProgressionModel(ProgressionCatalogConfig catalog, IDatasaveService datasave,
            IResourceWallet resourceWallet, string saveKey, int processedGrantHistoryLimit = 4096)
            : this(catalog, datasave, GatewayFor(resourceWallet, processedGrantHistoryLimit), saveKey)
        {
        }

        public ProgressionModel(ProgressionCatalogConfig catalog, IDatasaveService datasave,
            IRewardDeliveryGateway gateway, string saveKey,
            IProgressionLegacyRewardMigration legacyMigration = null, string occurrenceId = "permanent")
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.datasave = datasave ?? throw new ArgumentNullException(nameof(datasave));
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            this.saveKey = !string.IsNullOrWhiteSpace(saveKey) ? saveKey : throw new ArgumentException("Save key is required.", nameof(saveKey));
            this.occurrenceId = !string.IsNullOrWhiteSpace(occurrenceId) ? occurrenceId : throw new ArgumentException("Occurrence is required.", nameof(occurrenceId));
            this.legacyMigration = legacyMigration;
            catalog.Initialize("progressionCatalog");
            Load();
        }

        public event Action<ProgressionState> ProgressChanged;
        public event Action RewardsChanged;
        public event Action<Exception> NotificationFailed;
        public ProgressionState GetState() => CreateState();

        public ProgressionAdvanceResult AdvanceNext(string grantId) => Advance(grantId, 0, ProgressionMode.Sequential);
        public ProgressionAdvanceResult GrantExperience(string grantId, long amount) => Advance(grantId, amount, ProgressionMode.Experience);

        private ProgressionAdvanceResult Advance(string grantId, long amount, ProgressionMode mode)
        {
            var state = CreateState();
            if (busy || publishing) return ProgressionAdvanceResult.Rejected(ProgressionAdvanceStatus.Busy, state);
            if (RequiresReload) return ProgressionAdvanceResult.Rejected(ProgressionAdvanceStatus.RecoveryRequired, state);
            if (RequiresMigration) return ProgressionAdvanceResult.Rejected(ProgressionAdvanceStatus.MigrationRequired, state);
            if (catalog.Mode != mode) return ProgressionAdvanceResult.WrongMode(state);
            if (string.IsNullOrWhiteSpace(grantId)) return ProgressionAdvanceResult.InvalidGrant(state);
            if (save.ProcessedGrantIds.Contains(grantId)) return ProgressionAdvanceResult.AlreadyProcessed(state);
            if (mode == ProgressionMode.Experience && (amount <= 0 || save.TotalExperience > long.MaxValue - amount))
                return ProgressionAdvanceResult.InvalidGrant(state);
            if (mode == ProgressionMode.Sequential && state.IsMaxLevel) return ProgressionAdvanceResult.MaxLevel(state);
            busy = true;
            try
            {
                var candidate = save.Clone();
                long xp = mode == ProgressionMode.Experience ? candidate.TotalExperience + amount : 0;
                int target = mode == ProgressionMode.Experience ? ResolveLevelIndex(xp, candidate.Levels) : candidate.CurrentLevelIndex + 1;
                int advanced = target - candidate.CurrentLevelIndex;
                for (int i = candidate.CurrentLevelIndex + 1; i <= target; i++)
                {
                    var level = catalog.Levels[i];
                    if (!level.HasReward) continue;
                    var bundle = level.GetBundle();
                    var record = new ProgressionMilestoneReward
                    {
                        MilestoneId = level.Id, Level = level.Level,
                        BundleSchemaVersion = bundle.SchemaVersion, ResolverVersion = bundle.ResolverVersion,
                        RequiresLegacyReconciliation = candidate.LegacyHistory
                    };
                    foreach (var entry in bundle.Entries)
                        record.Payload.Add(new RewardJournalEntry
                        { EntryId = entry.EntryId, KindId = entry.KindId, TargetId = entry.TargetId, Amount = entry.Amount });
                    string requestId = RequestId(record.MilestoneId);
                    // Stage all coordinator intents into this one progression candidate.
                    var staged = new RewardDeliveryCoordinator(gateway).Begin(requestId,
                        Fingerprint(requestId, bundle), journal => record.Journal = journal.Clone());
                    if (!staged.IsSuccess) throw new InvalidOperationException("Could not stage milestone intent.");
                    candidate.MilestoneRewards.Add(record);
                }
                candidate.TotalExperience = xp;
                candidate.CurrentLevelIndex = target;
                candidate.ProcessedGrantIds.Add(grantId);
                try { Commit(candidate); }
                catch (Exception) { return ProgressionAdvanceResult.Rejected(ProgressionAdvanceStatus.PersistenceFailed, state); }
                var updated = CreateState();
                NotifyProgress(updated);
                NotifyRewards();
                // Delivery failure never changes the durable advancement outcome.
                RetryPendingCore();
                return mode == ProgressionMode.Experience
                    ? ProgressionAdvanceResult.ExperienceGranted(updated, advanced)
                    : ProgressionAdvanceResult.Advanced(updated, advanced);
            }
            finally { busy = false; NotifyRewards(); }
        }

        public void Reload()
        {
            if (busy || publishing) throw new InvalidOperationException("Progression is busy.");
            busy = true;
            try { RequiresReload = true; Load(); NotifyProgress(CreateState()); NotifyRewards(); }
            finally { busy = false; NotifyRewards(); }
        }

        private void Load()
        {
            var loaded = datasave.Load<ProgressionSaveData>(saveKey);
            loaded.OnAfterLoad();
            var candidate = loaded.Clone();
            bool initialize = candidate.Levels.Count == 0;
            bool fresh = string.IsNullOrEmpty(candidate.CatalogId);
            if (initialize)
            {
                if (!fresh && !candidate.LegacyHistory)
                    throw new InvalidOperationException("Missing v2 level snapshot; host recovery required.");
                if (!fresh && candidate.CatalogId != catalog.CatalogId)
                    throw new InvalidOperationException("Legacy catalog mismatch requires host migration.");
                if (!fresh) { candidate.LegacyHistory = true; candidate.LegacyReachedLevelIndex = candidate.CurrentLevelIndex; }
                candidate.CatalogId = catalog.CatalogId;
                candidate.Mode = catalog.Mode;
                candidate.OccurrenceId = occurrenceId;
                foreach (var level in catalog.Levels)
                    candidate.Levels.Add(new ProgressionLevelSnapshot
                    { Id = level.Id, Level = level.Level, RequiredExperience = level.RequiredExperience });
            }
            ValidateSave(candidate);
            bool compatible = candidate.CatalogId == catalog.CatalogId && candidate.Mode == catalog.Mode &&
                candidate.OccurrenceId == occurrenceId && candidate.Levels.Count == catalog.Levels.Count;
            if (compatible)
                for (int i = 0; i < candidate.Levels.Count; i++)
                    compatible &= candidate.Levels[i].Id == catalog.Levels[i].Id &&
                        candidate.Levels[i].RequiredExperience == catalog.Levels[i].RequiredExperience;
            if (initialize) Commit(candidate);
            else save = candidate;
            RequiresMigration = !compatible;
            RequiresReload = false;
        }

        private static void ValidateSave(ProgressionSaveData candidate)
        {
            if (!Enum.IsDefined(typeof(ProgressionMode), candidate.Mode) || string.IsNullOrWhiteSpace(candidate.OccurrenceId) ||
                candidate.CurrentLevelIndex < 0 || candidate.CurrentLevelIndex >= candidate.Levels.Count || candidate.TotalExperience < 0)
                throw new InvalidOperationException("Invalid Progression save; host recovery required.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            long previous = -1;
            for (int i = 0; i < candidate.Levels.Count; i++)
            {
                var level = candidate.Levels[i];
                if (level == null || string.IsNullOrWhiteSpace(level.Id) || !ids.Add(level.Id) || level.Level != i + 1 ||
                    level.RequiredExperience < 0 || (candidate.Mode == ProgressionMode.Experience && level.RequiredExperience <= previous) ||
                    (candidate.Mode == ProgressionMode.Sequential && level.RequiredExperience != 0))
                    throw new InvalidOperationException("Invalid saved level structure.");
                previous = level.RequiredExperience;
            }
            if (candidate.Mode == ProgressionMode.Experience && (candidate.Levels[0].RequiredExperience != 0 ||
                ResolveLevelIndex(candidate.TotalExperience, candidate.Levels) != candidate.CurrentLevelIndex))
                throw new InvalidOperationException("Saved XP and level disagree; explicit migration required.");
            ids.Clear();
            foreach (var record in candidate.MilestoneRewards)
                if (record == null || string.IsNullOrWhiteSpace(record.MilestoneId) || !ids.Add(record.MilestoneId))
                    throw new InvalidOperationException("Invalid milestone records.");
        }

        private void Commit(ProgressionSaveData candidate)
        {
            try { datasave.Save(candidate, saveKey); }
            catch (Exception) { RequiresReload = true; throw; }
            save = candidate;
        }

        private string RequestId(string milestoneId) => RewardIdentity.Request(saveKey, "progression", save.CatalogId, save.OccurrenceId, milestoneId);
        private static string Fingerprint(string requestId, ResolvedRewardBundle bundle) =>
            RewardIdentity.Fingerprint("progression-milestone", requestId, bundle.Fingerprint);

        private static int ResolveLevelIndex(long experience, IReadOnlyList<ProgressionLevelSnapshot> levels)
        {
            int index = 0;
            while (index + 1 < levels.Count && levels[index + 1].RequiredExperience <= experience) index++;
            return index;
        }

        private ProgressionState CreateState()
        {
            var current = save.Levels[save.CurrentLevelIndex];
            bool experience = save.Mode == ProgressionMode.Experience;
            bool hasNext = save.CurrentLevelIndex + 1 < save.Levels.Count;
            return new ProgressionState(save.Mode, current.Id, current.Level, save.Levels.Count,
                experience ? save.TotalExperience : 0,
                experience && hasNext ? save.TotalExperience - current.RequiredExperience : 0,
                experience && hasNext ? save.Levels[save.CurrentLevelIndex + 1].RequiredExperience - current.RequiredExperience : 0);
        }

        private void NotifyProgress(ProgressionState state)
        {
            if (ProgressChanged == null) return;
            foreach (Action<ProgressionState> listener in ProgressChanged.GetInvocationList())
                try { listener(state); } catch (Exception exception) { Report(exception); }
        }
        private void NotifyRewards()
        {
            if (RewardsChanged == null) return;
            bool previous = publishing;
            publishing = true;
            try
            {
                foreach (Action listener in RewardsChanged.GetInvocationList())
                    try { listener(); } catch (Exception exception) { Report(exception); }
            }
            finally { publishing = previous; }
        }
        private void Report(Exception exception)
        { try { NotificationFailed?.Invoke(exception); } catch (Exception) { } }

        private static IRewardDeliveryGateway GatewayFor(IResourceWallet wallet, int processedGrantHistoryLimit)
        {
            if (processedGrantHistoryLimit <= 0) throw new ArgumentOutOfRangeException(nameof(processedGrantHistoryLimit));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            return wallet is IRewardDestination destination
                ? new RewardDeliveryGateway(new[] { destination }) : new UnavailableGateway();
        }
        private sealed class UnavailableGateway : IRewardDeliveryGateway
        {
            public RewardDeliveryResult Validate(string id, string fingerprint, ResolvedRewardBundle bundle) =>
                RewardDeliveryResult.Pending(RewardDeliveryReason.DestinationUnavailable);
            public RewardDeliveryResult Deliver(RewardDeliveryRequest request) =>
                RewardDeliveryResult.Pending(RewardDeliveryReason.DestinationUnavailable);
        }
    }
}
