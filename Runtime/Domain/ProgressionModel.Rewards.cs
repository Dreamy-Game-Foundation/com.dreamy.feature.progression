using System;
using System.Collections.Generic;
using Dreamy.Economy.Rewards;

namespace Dreamy.Progression
{
    public sealed partial class ProgressionModel
    {
        public IReadOnlyList<ProgressionRewardState> GetRewardStates()
        {
            var states = new List<ProgressionRewardState>();
            foreach (var record in save.MilestoneRewards)
                states.Add(new ProgressionRewardState(record, busy, RequiresReload, ValidateRecord(record)));
            return states.AsReadOnly();
        }

        public RewardDeliveryResult RetryReward(string milestoneId)
        {
            if (busy || publishing) return RewardDeliveryResult.Pending(RewardDeliveryReason.Busy);
            if (RequiresReload) return RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed);
            int index = save.MilestoneRewards.FindIndex(r => r.MilestoneId == milestoneId);
            if (index < 0) return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest);
            busy = true;
            try { return Deliver(index); }
            finally { busy = false; NotifyRewards(); }
        }

        public IReadOnlyList<RewardDeliveryResult> RetryPendingRewards()
        {
            if (busy || publishing) return new[] { RewardDeliveryResult.Pending(RewardDeliveryReason.Busy) };
            if (RequiresReload) return new[] { RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed) };
            busy = true;
            try { return RetryPendingCore(); }
            finally { busy = false; NotifyRewards(); }
        }

        private IReadOnlyList<RewardDeliveryResult> RetryPendingCore()
        {
            var results = new List<RewardDeliveryResult>();
            for (int i = 0; i < save.MilestoneRewards.Count; i++)
            {
                var record = save.MilestoneRewards[i];
                if ((record.LegacyDelivered || record.Journal?.State == RewardJournalState.Completed) &&
                    ValidateRecord(record) == RewardDeliveryReason.None) continue;
                results.Add(Deliver(i));
                if (RequiresReload) break;
            }
            return results.AsReadOnly();
        }

        private RewardDeliveryResult Deliver(int index)
        {
            var record = save.MilestoneRewards[index];
            var invalid = ValidateRecord(record);
            if (invalid != RewardDeliveryReason.None) return Remember(index, RewardDeliveryResult.Failed(invalid));
            var bundle = record.GetBundle();
            if (record.LegacyDelivered) return RewardDeliveryResult.AlreadyDelivered();
            if (record.RequiresLegacyReconciliation)
            {
                LegacyProgressionRewardStatus legacy;
                try { legacy = legacyMigration?.Reconcile($"progression:{save.CatalogId}:level:{record.MilestoneId}", bundle)
                    ?? LegacyProgressionRewardStatus.Unverifiable; }
                catch (Exception) { return Remember(index, RewardDeliveryResult.Pending(RewardDeliveryReason.DestinationUnavailable)); }
                if (legacy != LegacyProgressionRewardStatus.NotDelivered && legacy != LegacyProgressionRewardStatus.AlreadyDelivered)
                    return Remember(index, RewardDeliveryResult.Failed(RewardDeliveryReason.LegacyReceiptUnverifiable));
                var candidate = save.Clone();
                candidate.MilestoneRewards[index].RequiresLegacyReconciliation = false;
                candidate.MilestoneRewards[index].LegacyDelivered = legacy == LegacyProgressionRewardStatus.AlreadyDelivered;
                try { Commit(candidate); }
                catch (Exception) { return RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed); }
                if (legacy == LegacyProgressionRewardStatus.AlreadyDelivered)
                    return Remember(index, RewardDeliveryResult.AlreadyDelivered());
                record = save.MilestoneRewards[index];
            }
            var coordinator = new RewardDeliveryCoordinator(gateway, record.Journal);
            Action<RewardJournal> persist = journal =>
            {
                var candidate = save.Clone();
                candidate.MilestoneRewards[index].Journal = journal.Clone();
                candidate.MilestoneRewards[index].LastReason = RewardDeliveryReason.None;
                candidate.MilestoneRewards[index].Retryable = true;
                Commit(candidate);
                NotifyRewards();
            };
            if (record.Journal.State == RewardJournalState.IntentPersisted)
            {
                var resolved = coordinator.Resolve(bundle, persist);
                if (!resolved.IsSuccess) return Remember(index, resolved);
            }
            var delivered = coordinator.Deliver(persist);
            if (!delivered.IsSuccess) return Remember(index, delivered);
            return Remember(index, coordinator.Complete(persist));
        }

        private RewardDeliveryReason ValidateRecord(ProgressionMilestoneReward record)
        {
            if (record.Level < 2 || record.Level > save.CurrentLevelIndex + 1 ||
                save.Levels[record.Level - 1].Id != record.MilestoneId ||
                (record.LegacyDelivered && record.RequiresLegacyReconciliation))
                return RewardDeliveryReason.InvalidJournal;
            try
            {
                var bundle = record.GetBundle();
                if (record.Journal == null || !record.Journal.IsValid()) return RewardDeliveryReason.InvalidJournal;
                string id = RequestId(record.MilestoneId);
                if (record.Journal.RequestId != id || record.Journal.RequestFingerprint != Fingerprint(id, bundle) ||
                    (record.Journal.State != RewardJournalState.IntentPersisted && record.Journal.ResolvedFingerprint != bundle.Fingerprint))
                    return RewardDeliveryReason.PayloadMismatch;
            }
            catch (ArgumentException) { return RewardDeliveryReason.InvalidJournal; }
            return RewardDeliveryReason.None;
        }

        private RewardDeliveryResult Remember(int index, RewardDeliveryResult result)
        {
            // A destination may have durably saved before reporting persistence failure.
            // Stop the whole batch until the host reloads Progression AND affected owners.
            if (result.Reason == RewardDeliveryReason.PersistenceFailed) RequiresReload = true;
            if (RequiresReload) return RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed);
            var record = save.MilestoneRewards[index];
            if (record.LastReason != result.Reason || record.Retryable != result.Retryable)
            {
                var candidate = save.Clone();
                candidate.MilestoneRewards[index].LastReason = result.Reason;
                candidate.MilestoneRewards[index].Retryable = result.Retryable;
                try { Commit(candidate); }
                catch (Exception) { return RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed); }
            }
            NotifyRewards();
            return result;
        }
    }
}
