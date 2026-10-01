using System;
using System.Collections.Generic;
using Dreamy.DataConfig;
using Dreamy.Economy;
using Newtonsoft.Json;

namespace Dreamy.Progression
{
    public sealed class ProgressionCatalogConfig : ConfigBase
    {
        [JsonProperty("catalogId", Required = Required.Always)]
        private string catalogId;

        [JsonProperty("mode", Required = Required.Always)]
        private ProgressionMode mode;

        [JsonProperty("levels", Required = Required.Always)]
        private List<ProgressionLevelEntry> levels = new();

        [JsonIgnore]
        public string CatalogId => catalogId;

        [JsonIgnore]
        public ProgressionMode Mode => mode;

        [JsonIgnore]
        public IReadOnlyList<ProgressionLevelEntry> Levels => levels;

        public override void Initialize(string documentName)
        {
            if (string.IsNullOrWhiteSpace(catalogId))
            {
                throw new DataConfigException(documentName, "catalogId cannot be empty.");
            }

            if (!Enum.IsDefined(typeof(ProgressionMode), mode))
            {
                throw new DataConfigException(documentName, "mode is invalid.");
            }

            if (levels == null || levels.Count == 0)
            {
                throw new DataConfigException(documentName, "levels must contain at least one entry.");
            }

            HashSet<string> ids = new(StringComparer.Ordinal);
            long previousRequiredExperience = -1;
            for (int index = 0; index < levels.Count; index++)
            {
                ProgressionLevelEntry entry = levels[index];
                if (entry == null)
                {
                    throw new DataConfigException(documentName, $"Level at index {index} is null.");
                }

                entry.Validate(documentName, index, ids, mode, previousRequiredExperience);
                if (index == 0 && entry.HasReward)
                {
                    throw new DataConfigException(documentName, "The initial level cannot grant a milestone reward.");
                }
                previousRequiredExperience = entry.RequiredExperience;
            }

            if (mode == ProgressionMode.Experience && levels[0].RequiredExperience != 0)
            {
                throw new DataConfigException(documentName, "The first experience level must require 0 experience.");
            }
        }
    }

    public enum ProgressionMode
    {
        Sequential,
        Experience
    }

    [Serializable]
    public sealed class ProgressionLevelEntry
    {
        [JsonProperty("id", Required = Required.Always)]
        private string id;

        [JsonProperty("level", Required = Required.Always)]
        private int level;

        [JsonProperty("requiredExperience")]
        private long requiredExperience;

        [JsonProperty("rewardResourceId")]
        private string rewardResourceId;

        [JsonProperty("rewardAmount")]
        private long rewardAmount;

        [JsonIgnore]
        public string Id => id;

        [JsonIgnore]
        public int Level => level;

        [JsonIgnore]
        public long RequiredExperience => requiredExperience;

        [JsonIgnore]
        public bool HasReward => !string.IsNullOrWhiteSpace(rewardResourceId);

        [JsonIgnore]
        public ResourceAmount Reward => new(new ResourceId(rewardResourceId), rewardAmount);

        internal void Validate(
            string documentName,
            int index,
            ISet<string> ids,
            ProgressionMode mode,
            long previousRequiredExperience)
        {
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
            {
                throw new DataConfigException(documentName, $"Level at index {index} has a duplicate or empty id.");
            }

            if (level != index + 1)
            {
                throw new DataConfigException(documentName, "Levels must start at 1 and be consecutive.");
            }

            if (requiredExperience < 0)
            {
                throw new DataConfigException(documentName, $"Level '{id}' has negative requiredExperience.");
            }

            if (mode == ProgressionMode.Sequential && requiredExperience != 0)
            {
                throw new DataConfigException(documentName, "Sequential progression levels must use requiredExperience 0.");
            }

            if (mode == ProgressionMode.Experience &&
                index > 0 &&
                requiredExperience <= previousRequiredExperience)
            {
                throw new DataConfigException(documentName, "Experience thresholds must strictly increase.");
            }

            if (HasReward)
            {
                if (!ResourceId.TryParse(rewardResourceId, out _) || rewardAmount <= 0)
                {
                    throw new DataConfigException(documentName, $"Level '{id}' has invalid reward data.");
                }
            }
            else if (rewardAmount != 0)
            {
                throw new DataConfigException(documentName, $"Level '{id}' has rewardAmount without rewardResourceId.");
            }
        }
    }
}
