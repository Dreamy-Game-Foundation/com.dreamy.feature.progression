using System;
using System.Collections.Generic;
using Dreamy.Datasave;
using Newtonsoft.Json;

namespace Dreamy.Progression
{
    [Serializable]
    public sealed class ProgressionSaveData : SaveData
    {
        [JsonProperty("catalogId")]
        public string CatalogId { get; set; }

        [JsonProperty("currentLevelIndex")]
        public int CurrentLevelIndex { get; set; }

        [JsonProperty("totalExperience")]
        public long TotalExperience { get; set; }

        [JsonProperty("processedGrantIds")]
        public List<string> ProcessedGrantIds { get; set; } = new();

        public override int Version => 1;

        public override void OnAfterLoad()
        {
            ProcessedGrantIds ??= new List<string>();
        }
    }
}
