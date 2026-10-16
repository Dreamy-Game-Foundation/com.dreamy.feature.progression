using System.Collections.Generic;
using Dreamy.Economy.Rewards;

namespace Dreamy.Progression
{
    public enum ProgressionRewardStatus { Pending, Blocked, Delivered }

    public readonly struct ProgressionRewardEntryState
    {
        public ProgressionRewardEntryState(ResolvedRewardEntry reward, bool delivered)
        { Reward = reward; Delivered = delivered; }
        public ResolvedRewardEntry Reward { get; }
        public bool Delivered { get; }
    }

    public sealed class ProgressionRewardState
    {
        internal ProgressionRewardState(ProgressionMilestoneReward record, bool busy, bool requiresReload, RewardDeliveryReason invalid)
        {
            MilestoneId = record.MilestoneId;
            Level = record.Level;
            Reason = invalid != RewardDeliveryReason.None ? invalid : requiresReload
                ? RewardDeliveryReason.PersistenceFailed : record.LastReason;
            IsBusy = busy;
            Status = invalid != RewardDeliveryReason.None ? ProgressionRewardStatus.Blocked :
                record.LegacyDelivered || record.Journal?.State == RewardJournalState.Completed
                ? ProgressionRewardStatus.Delivered
                : record.Retryable ? ProgressionRewardStatus.Pending : ProgressionRewardStatus.Blocked;
            CanRetry = !busy && !requiresReload && Status != ProgressionRewardStatus.Delivered;
            Retryable = invalid == RewardDeliveryReason.None && record.Retryable;
            var entries = new List<ProgressionRewardEntryState>();
            if (invalid == RewardDeliveryReason.None && record.Payload != null)
                for (int i = 0; i < record.Payload.Count; i++)
                {
                    var e = record.Payload[i];
                    if (e == null) continue;
                    bool acknowledged = record.LegacyDelivered || (record.Journal?.Entries != null &&
                        i < record.Journal.Entries.Count && record.Journal.Entries[i].Acknowledged);
                    entries.Add(new ProgressionRewardEntryState(
                        new ResolvedRewardEntry(e.EntryId, e.KindId, e.TargetId, e.Amount), acknowledged));
                }
            Entries = entries.AsReadOnly();
        }
        public string MilestoneId { get; }
        public int Level { get; }
        public ProgressionRewardStatus Status { get; }
        public IReadOnlyList<ProgressionRewardEntryState> Entries { get; }
        public RewardDeliveryReason Reason { get; }
        public bool IsBusy { get; }
        public bool CanRetry { get; }
        public bool Retryable { get; }
    }
}
