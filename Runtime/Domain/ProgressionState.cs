using System;

namespace Dreamy.Progression
{
    public readonly struct ProgressionState
    {
        public ProgressionState(
            ProgressionMode mode,
            string currentLevelId,
            int currentLevel,
            int maxLevel,
            long totalExperience,
            long experienceIntoCurrentLevel,
            long experienceToNextLevel)
        {
            Mode = mode;
            CurrentLevelId = currentLevelId;
            CurrentLevel = currentLevel;
            MaxLevel = maxLevel;
            TotalExperience = totalExperience;
            ExperienceIntoCurrentLevel = experienceIntoCurrentLevel;
            ExperienceToNextLevel = experienceToNextLevel;
        }

        public ProgressionMode Mode { get; }
        public string CurrentLevelId { get; }
        public int CurrentLevel { get; }
        public int MaxLevel { get; }
        public long TotalExperience { get; }
        public long ExperienceIntoCurrentLevel { get; }
        public long ExperienceToNextLevel { get; }
        public bool IsMaxLevel => CurrentLevel >= MaxLevel;
    }
}
