using System;
using System.Collections.Generic;
using Dreamy.Datasave;
using Newtonsoft.Json;

namespace Dreamy.Progression.Tests
{
    internal sealed class ProgressionRewardTestStore : IDatasaveService
    {
        public readonly Dictionary<string, string> Disk;
        public readonly Dictionary<string, int> Versions;
        public Action<string, SaveData> BeforeSave;
        public Action<string, SaveData> AfterSave;
        public ProgressionRewardTestStore(Dictionary<string, string> disk = null, Dictionary<string, int> versions = null)
        { Disk = disk ?? new Dictionary<string, string>(); Versions = versions ?? new Dictionary<string, int>(); }
        public T Load<T>(string key = null) where T : SaveData, new()
        {
            var data = Disk.TryGetValue(key, out var json) ? JsonConvert.DeserializeObject<T>(json) : new T();
            if (Versions.TryGetValue(key, out int version))
            {
                if (version > data.Version) throw new InvalidOperationException("Future save.");
                if (version < data.Version) data.Migrate(version);
            }
            data.OnAfterLoad();
            return data;
        }
        public void Save<T>(T data, string key = null) where T : SaveData
        {
            BeforeSave?.Invoke(key, data);
            Disk[key] = JsonConvert.SerializeObject(data);
            Versions[key] = data.Version;
            AfterSave?.Invoke(key, data);
        }
        public void SaveAll() { }
        public bool Exists(string key) => Disk.ContainsKey(key);
        public void Delete(string key) { Disk.Remove(key); Versions.Remove(key); }
        public void DeleteAll() { Disk.Clear(); Versions.Clear(); }
        public ProgressionRewardTestStore Restart() => new(Disk, Versions);
    }
}
