using System;
using System.Collections.Generic;
using Dreamy.Datasave;
using Dreamy.Economy;

namespace Dreamy.Progression
{
    public sealed class ProgressionModel : IProgressionService
    {
        private const int DefaultProcessedGrantHistoryLimit = 4096;

        private readonly ProgressionCatalogConfig catalog;
        private readonly IDatasaveService datasave;
        private readonly IResourceWallet resourceWallet;
        private readonly string saveKey;
        private readonly int processedGrantHistoryLimit;
        private readonly ProgressionSaveData save;
        private readonly HashSet<string> processedGrantIds = new(StringComparer.Ordinal);
        private readonly List<string> processedGrantHistory = new();

        public ProgressionModel(
            ProgressionCatalogConfig catalog,
            IDatasaveService datasave,
            IResourceWallet resourceWallet,
            string saveKey,
            int processedGrantHistoryLimit = DefaultProcessedGrantHistoryLimit)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.datasave = datasave ?? throw new ArgumentNullException(nameof(datasave));
            this.resourceWallet = resourceWallet ?? throw new ArgumentNullException(nameof(resourceWallet));
            this.saveKey = string.IsNullOrWhiteSpace(saveKey)
                ? throw new ArgumentException("Save key cannot be empty.", nameof(saveKey))
                : saveKey;
            if (processedGrantHistoryLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(processedGrantHistoryLimit));
            }

            this.processedGrantHistoryLimit = processedGrantHistoryLimit;
            save = datasave.Load<ProgressionSaveData>(saveKey);
            InitializeForCatalog();
            LoadProcessedGrantIds();
        }

        public event Action<ProgressionState> ProgressChanged;

        public ProgressionState GetState() => CreateState();

        public ProgressionAdvanceResult AdvanceNext(string grantId)
        {
            ProgressionState state = CreateState();
            if (catalog.Mode != ProgressionMode.Sequential)
            {
                return ProgressionAdvanceResult.WrongMode(state);
            }

            if (!IsGrantIdValid(grantId))
            {
                return ProgressionAdvanceResult.InvalidGrant(state);
            }

            if (processedGrantIds.Contains(grantId))
            {
                return ProgressionAdvanceResult.AlreadyProcessed(state);
            }

            if (state.IsMaxLevel)
            {
                return ProgressionAdvanceResult.MaxLevel(state);
            }

            ProgressionLevelEntry nextLevel = catalog.Levels[save.CurrentLevelIndex + 1];
            if (!TryGrantReward(nextLevel))
            {
                return ProgressionAdvanceResult.RewardGrantFailed(state);
            }

            save.CurrentLevelIndex++;
            AddProcessedGrantId(grantId);
            Save();
            ProgressionState updatedState = CreateState();
            ProgressChanged?.Invoke(updatedState);
            return ProgressionAdvanceResult.Advanced(updatedState, 1);
        }

        public ProgressionAdvanceResult GrantExperience(string grantId, long amount)
        {
            ProgressionState state = CreateState();
            if (catalog.Mode != ProgressionMode.Experience)
            {
                return ProgressionAdvanceResult.WrongMode(state);
            }

            if (!IsGrantIdValid(grantId) || amount <= 0 || save.TotalExperience > long.MaxValue - amount)
            {
                return ProgressionAdvanceResult.InvalidGrant(state);
            }

            if (processedGrantIds.Contains(grantId))
            {
                return ProgressionAdvanceResult.AlreadyProcessed(state);
            }

            long updatedExperience = save.TotalExperience + amount;
            int targetLevelIndex = ResolveLevelIndex(updatedExperience);
            for (int index = save.CurrentLevelIndex + 1; index <= targetLevelIndex; index++)
            {
                if (!TryGrantReward(catalog.Levels[index]))
                {
                    return ProgressionAdvanceResult.RewardGrantFailed(state);
                }
            }

            int levelsAdvanced = targetLevelIndex - save.CurrentLevelIndex;
            save.TotalExperience = updatedExperience;
            save.CurrentLevelIndex = targetLevelIndex;
            AddProcessedGrantId(grantId);
            Save();
            ProgressionState updatedState = CreateState();
            ProgressChanged?.Invoke(updatedState);
            return ProgressionAdvanceResult.ExperienceGranted(updatedState, levelsAdvanced);
        }

        private void InitializeForCatalog()
        {
            if (!string.Equals(save.CatalogId, catalog.CatalogId, StringComparison.Ordinal))
            {
                save.CatalogId = catalog.CatalogId;
                save.CurrentLevelIndex = 0;
                save.TotalExperience = 0;
                save.ProcessedGrantIds.Clear();
                datasave.Save(save, saveKey);
            }

            int maxIndex = catalog.Levels.Count - 1;
            if (save.CurrentLevelIndex < 0 || save.CurrentLevelIndex > maxIndex || save.TotalExperience < 0)
            {
                save.CurrentLevelIndex = 0;
                save.TotalExperience = 0;
                save.ProcessedGrantIds.Clear();
                datasave.Save(save, saveKey);
            }

            if (catalog.Mode == ProgressionMode.Experience)
            {
                int resolvedIndex = ResolveLevelIndex(save.TotalExperience);
                if (save.CurrentLevelIndex != resolvedIndex)
                {
                    save.CurrentLevelIndex = resolvedIndex;
                    datasave.Save(save, saveKey);
                }
            }
        }

        private void LoadProcessedGrantIds()
        {
            foreach (string grantId in save.ProcessedGrantIds)
            {
                if (!string.IsNullOrWhiteSpace(grantId) && processedGrantIds.Add(grantId))
                {
                    processedGrantHistory.Add(grantId);
                }
            }
        }

        private int ResolveLevelIndex(long totalExperience)
        {
            int resolvedIndex = 0;
            for (int index = 1; index < catalog.Levels.Count; index++)
            {
                if (catalog.Levels[index].RequiredExperience > totalExperience)
                {
                    break;
                }

                resolvedIndex = index;
            }

            return resolvedIndex;
        }

        private bool TryGrantReward(ProgressionLevelEntry level)
        {
            if (!level.HasReward)
            {
                return true;
            }

            string transactionId = $"progression:{catalog.CatalogId}:level:{level.Id}";
            return resourceWallet.TryGrant(new ResourceGrantRequest(transactionId, level.Reward));
        }

        private void AddProcessedGrantId(string grantId)
        {
            processedGrantIds.Add(grantId);
            processedGrantHistory.Add(grantId);
        }

        private void Save()
        {
            int firstRetainedIndex = Math.Max(0, processedGrantHistory.Count - processedGrantHistoryLimit);
            save.ProcessedGrantIds.Clear();
            for (int index = firstRetainedIndex; index < processedGrantHistory.Count; index++)
            {
                save.ProcessedGrantIds.Add(processedGrantHistory[index]);
            }

            if (firstRetainedIndex > 0)
            {
                processedGrantIds.Clear();
                processedGrantHistory.Clear();
                LoadProcessedGrantIds();
            }

            datasave.Save(save, saveKey);
        }

        private ProgressionState CreateState()
        {
            ProgressionLevelEntry currentLevel = catalog.Levels[save.CurrentLevelIndex];
            long totalExperience = catalog.Mode == ProgressionMode.Experience
                ? save.TotalExperience
                : 0;
            long experienceIntoCurrentLevel = 0;
            long experienceToNextLevel = 0;
            if (catalog.Mode == ProgressionMode.Experience &&
                save.CurrentLevelIndex < catalog.Levels.Count - 1)
            {
                long currentThreshold = currentLevel.RequiredExperience;
                long nextThreshold = catalog.Levels[save.CurrentLevelIndex + 1].RequiredExperience;
                experienceIntoCurrentLevel = save.TotalExperience - currentThreshold;
                experienceToNextLevel = nextThreshold - currentThreshold;
            }

            return new ProgressionState(
                catalog.Mode,
                currentLevel.Id,
                currentLevel.Level,
                catalog.Levels[catalog.Levels.Count - 1].Level,
                totalExperience,
                experienceIntoCurrentLevel,
                experienceToNextLevel);
        }

        private static bool IsGrantIdValid(string grantId) => !string.IsNullOrWhiteSpace(grantId);
    }
}
