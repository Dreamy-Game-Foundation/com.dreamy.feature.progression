using System;
using System.Collections.Generic;
using Dreamy.Economy.Rewards;

namespace Dreamy.Progression
{
    [Serializable]
    public sealed class ProgressionMilestoneReward
    {
        public string MilestoneId;
        public int Level;
        public string ResolverVersion;
        public int BundleSchemaVersion;
        public List<RewardJournalEntry> Payload = new();
        public RewardJournal Journal;
        public bool RequiresLegacyReconciliation;
        public bool LegacyDelivered;
        public RewardDeliveryReason LastReason;
        public bool Retryable = true;

        public ResolvedRewardBundle GetBundle()
        {
            if (Payload == null) throw new ArgumentException("Missing milestone payload.");
            var entries = new List<ResolvedRewardEntry>();
            foreach (var entry in Payload)
            {
                if (entry == null) throw new ArgumentException("Null milestone entry.");
                entries.Add(new ResolvedRewardEntry(entry.EntryId, entry.KindId, entry.TargetId, entry.Amount));
            }
            return new ResolvedRewardBundle(BundleSchemaVersion, ResolverVersion, entries);
        }

        public ProgressionMilestoneReward Clone()
        {
            var result = (ProgressionMilestoneReward)MemberwiseClone();
            result.Journal = Journal?.Clone();
            result.Payload = Payload == null ? null : new List<RewardJournalEntry>();
            if (Payload != null) foreach (var entry in Payload) result.Payload.Add(entry?.Clone());
            return result;
        }
    }

    [Serializable]
    public sealed class ProgressionLevelSnapshot
    {
        public string Id;
        public int Level;
        public long RequiredExperience;
        public ProgressionLevelSnapshot Clone() => (ProgressionLevelSnapshot)MemberwiseClone();
    }
}
