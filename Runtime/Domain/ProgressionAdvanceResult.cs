namespace Dreamy.Progression
{
    public readonly struct ProgressionAdvanceResult
    {
        private ProgressionAdvanceResult(ProgressionAdvanceStatus status, ProgressionState state, int levelsAdvanced)
        {
            Status = status;
            State = state;
            LevelsAdvanced = levelsAdvanced;
        }

        public ProgressionAdvanceStatus Status { get; }
        public ProgressionState State { get; }
        public int LevelsAdvanced { get; }
        public bool Succeeded => Status == ProgressionAdvanceStatus.Advanced ||
                                 Status == ProgressionAdvanceStatus.ExperienceGranted;

        internal static ProgressionAdvanceResult Advanced(ProgressionState state, int levelsAdvanced) =>
            new(ProgressionAdvanceStatus.Advanced, state, levelsAdvanced);

        internal static ProgressionAdvanceResult ExperienceGranted(ProgressionState state, int levelsAdvanced) =>
            new(ProgressionAdvanceStatus.ExperienceGranted, state, levelsAdvanced);

        internal static ProgressionAdvanceResult AlreadyProcessed(ProgressionState state) =>
            new(ProgressionAdvanceStatus.AlreadyProcessed, state, 0);

        internal static ProgressionAdvanceResult MaxLevel(ProgressionState state) =>
            new(ProgressionAdvanceStatus.MaxLevel, state, 0);

        internal static ProgressionAdvanceResult WrongMode(ProgressionState state) =>
            new(ProgressionAdvanceStatus.WrongMode, state, 0);

        internal static ProgressionAdvanceResult InvalidGrant(ProgressionState state) =>
            new(ProgressionAdvanceStatus.InvalidGrant, state, 0);

        internal static ProgressionAdvanceResult RewardGrantFailed(ProgressionState state) =>
            new(ProgressionAdvanceStatus.RewardGrantFailed, state, 0);
    }

    public enum ProgressionAdvanceStatus
    {
        Advanced,
        ExperienceGranted,
        AlreadyProcessed,
        MaxLevel,
        WrongMode,
        InvalidGrant,
        RewardGrantFailed
    }
}
