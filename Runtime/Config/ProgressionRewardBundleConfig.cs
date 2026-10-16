using System;
using System.Collections.Generic;
using Dreamy.Economy.Rewards;
using Newtonsoft.Json;

namespace Dreamy.Progression
{
    [Serializable]
    public sealed class ProgressionRewardBundleConfig
    {
        [JsonProperty("schemaVersion", Required = Required.Always)]
        private int schemaVersion;
        [JsonProperty("resolverVersion", Required = Required.Always)]
        private string resolverVersion;
        [JsonProperty("entries", Required = Required.Always)]
        private List<ProgressionRewardEntryConfig> entries;

        public ResolvedRewardBundle Resolve()
        {
            if (entries == null) throw new ArgumentException("Bundle entries are required.");
            var resolved = new List<ResolvedRewardEntry>();
            foreach (var entry in entries)
            {
                if (entry == null) throw new ArgumentException("Null reward entry.");
                resolved.Add(entry.Resolve());
            }
            return new ResolvedRewardBundle(schemaVersion, resolverVersion, resolved);
        }
    }

    [Serializable]
    public sealed class ProgressionRewardEntryConfig
    {
        [JsonProperty("entryId", Required = Required.Always)] private string entryId;
        [JsonProperty("kindId", Required = Required.Always)] private string kindId;
        [JsonProperty("targetId", Required = Required.Always)] private string targetId;
        [JsonProperty("amount", Required = Required.Always)] private long amount;
        internal ResolvedRewardEntry Resolve() => new(entryId, kindId, targetId, amount);
    }
}
