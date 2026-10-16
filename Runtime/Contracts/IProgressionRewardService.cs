using System;
using System.Collections.Generic;
using Dreamy.Economy.Rewards;

namespace Dreamy.Progression
{
    public interface IProgressionRewardService
    {
        event Action RewardsChanged;
        IReadOnlyList<ProgressionRewardState> GetRewardStates();
        RewardDeliveryResult RetryReward(string milestoneId);
        IReadOnlyList<RewardDeliveryResult> RetryPendingRewards();
        // Reload durable state after an ambiguous save error; never delivers rewards.
        void Reload();
        bool RequiresMigration { get; }
        bool RequiresReload { get; }
    }

    public enum LegacyProgressionRewardStatus { Unverifiable, NotDelivered, AlreadyDelivered }

    // The game must have authoritative legacy evidence or an explicit migration policy.
    // Missing IDs in a truncated legacy history are NOT proof of NotDelivered.
    public interface IProgressionLegacyRewardMigration
    {
        LegacyProgressionRewardStatus Reconcile(string legacyTransactionId, ResolvedRewardBundle intendedReward);
    }
}
