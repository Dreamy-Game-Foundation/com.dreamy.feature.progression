using System;
using System.Collections.Generic;
using Dreamy.Datasave;
using Newtonsoft.Json;

namespace Dreamy.Progression
{
    [Serializable]
    public sealed class ProgressionSaveData : SaveData
    {
        [JsonProperty("catalogId")] public string CatalogId { get; set; }
        [JsonProperty("currentLevelIndex")] public int CurrentLevelIndex { get; set; }
        [JsonProperty("totalExperience")] public long TotalExperience { get; set; }
        [JsonProperty("processedGrantIds")] public List<string> ProcessedGrantIds { get; set; } = new();
        public string OccurrenceId = "permanent";
        public ProgressionMode Mode;
        public List<ProgressionLevelSnapshot> Levels = new();
        public List<ProgressionMilestoneReward> MilestoneRewards = new();
        // v1 may have delivered future-level rewards before saving its level.
        public bool LegacyHistory;
        public int LegacyReachedLevelIndex;
        public override int Version => 2;

        public override void Migrate(int fromVersion)
        {
            if (fromVersion != 1) throw new InvalidOperationException("Unsupported Progression save version.");
            LegacyHistory = true;
            LegacyReachedLevelIndex = CurrentLevelIndex;
        }

        public override void OnAfterLoad()
        {
            ProcessedGrantIds ??= new List<string>();
            Levels ??= new List<ProgressionLevelSnapshot>();
            MilestoneRewards ??= new List<ProgressionMilestoneReward>();
        }

        public ProgressionSaveData Clone()
        {
            var copy = (ProgressionSaveData)MemberwiseClone();
            copy.ProcessedGrantIds = new List<string>(ProcessedGrantIds);
            copy.Levels = new List<ProgressionLevelSnapshot>();
            foreach (var level in Levels) copy.Levels.Add(level?.Clone());
            copy.MilestoneRewards = new List<ProgressionMilestoneReward>();
            foreach (var reward in MilestoneRewards) copy.MilestoneRewards.Add(reward?.Clone());
            return copy;
        }
    }
}
