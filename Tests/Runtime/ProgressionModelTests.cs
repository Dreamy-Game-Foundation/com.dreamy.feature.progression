using System.Collections.Generic;
using Dreamy.Datasave;
using Dreamy.Economy;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Dreamy.Progression.Tests
{
    public sealed class ProgressionModelTests
    {
        [Test]
        public void AdvanceNext_WhenSequential_AdvancesAndGrantsTheNextLevelReward()
        {
            FakeResourceWallet wallet = new();
            ProgressionModel model = CreateModel(SequentialJson, wallet, out _);

            ProgressionAdvanceResult result = model.AdvanceNext("stage-1-win");

            Assert.That(result.Status, Is.EqualTo(ProgressionAdvanceStatus.Advanced));
            Assert.That(result.State.CurrentLevel, Is.EqualTo(2));
            Assert.That(wallet.Requests, Has.Count.EqualTo(1));
            Assert.That(wallet.Requests[0].TransactionId, Is.EqualTo("progression:test-sequential:level:stage-2"));
        }

        [Test]
        public void AdvanceNext_WhenGrantIsRetried_DoesNotAdvanceOrRewardTwice()
        {
            FakeResourceWallet wallet = new();
            ProgressionModel model = CreateModel(SequentialJson, wallet, out _);
            model.AdvanceNext("stage-1-win");

            ProgressionAdvanceResult result = model.AdvanceNext("stage-1-win");

            Assert.That(result.Status, Is.EqualTo(ProgressionAdvanceStatus.AlreadyProcessed));
            Assert.That(result.State.CurrentLevel, Is.EqualTo(2));
            Assert.That(wallet.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void GrantExperience_WhenCrossingMultipleLevels_GrantsEachMilestoneOnce()
        {
            FakeResourceWallet wallet = new();
            ProgressionModel model = CreateModel(ExperienceJson, wallet, out _);

            ProgressionAdvanceResult result = model.GrantExperience("match-44", 300);

            Assert.That(result.Status, Is.EqualTo(ProgressionAdvanceStatus.ExperienceGranted));
            Assert.That(result.LevelsAdvanced, Is.EqualTo(2));
            Assert.That(result.State.CurrentLevel, Is.EqualTo(3));
            Assert.That(result.State.TotalExperience, Is.EqualTo(300));
            Assert.That(wallet.Requests, Has.Count.EqualTo(2));
            Assert.That(wallet.Requests[0].TransactionId, Is.EqualTo("progression:test-experience:level:rank-2"));
            Assert.That(wallet.Requests[1].TransactionId, Is.EqualTo("progression:test-experience:level:rank-3"));
        }

        [Test]
        public void GrantExperience_WhenRewardFails_DoesNotPersistExperienceOrProcessTheGrant()
        {
            FakeResourceWallet wallet = new { ShouldSucceed = false };
            ProgressionModel model = CreateModel(ExperienceJson, wallet, out InMemoryDatasaveService datasave);

            ProgressionAdvanceResult result = model.GrantExperience("match-44", 100);

            Assert.That(result.Status, Is.EqualTo(ProgressionAdvanceStatus.RewardGrantFailed));
            Assert.That(result.State.TotalExperience, Is.EqualTo(0));
            Assert.That(model.GetState().CurrentLevel, Is.EqualTo(1));
            Assert.That(datasave.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void AdvanceNext_WhenCalledForExperienceCatalog_ReturnsWrongMode()
        {
            ProgressionModel model = CreateModel(ExperienceJson, new FakeResourceWallet(), out _);

            ProgressionAdvanceResult result = model.AdvanceNext("match-44");

            Assert.That(result.Status, Is.EqualTo(ProgressionAdvanceStatus.WrongMode));
        }

        [Test]
        public void AdvanceNext_WhenAlreadyAtMaxLevel_DoesNotProcessTheGrant()
        {
            ProgressionModel model = CreateModel(SequentialJson, new FakeResourceWallet(), out _);
            model.AdvanceNext("stage-1-win");
            model.AdvanceNext("stage-2-win");

            ProgressionAdvanceResult result = model.AdvanceNext("stage-3-win");

            Assert.That(result.Status, Is.EqualTo(ProgressionAdvanceStatus.MaxLevel));
            Assert.That(result.State.CurrentLevel, Is.EqualTo(3));
        }

        [Test]
        public void Config_WhenExperienceThresholdsDoNotIncrease_Throws()
        {
            const string invalidJson = "{\"catalogId\":\"invalid\",\"mode\":\"Experience\",\"levels\":[{\"id\":\"rank-1\",\"level\":1,\"requiredExperience\":0},{\"id\":\"rank-2\",\"level\":2,\"requiredExperience\":0}]}";
            ProgressionCatalogConfig config = JsonConvert.DeserializeObject<ProgressionCatalogConfig>(invalidJson);

            Assert.That(() => config.Initialize("progressionCatalog"), Throws.TypeOf<DataConfigException>());
        }

        private static ProgressionModel CreateModel(
            string json,
            FakeResourceWallet wallet,
            out InMemoryDatasaveService datasave)
        {
            ProgressionCatalogConfig config = JsonConvert.DeserializeObject<ProgressionCatalogConfig>(json);
            config.Initialize("progressionCatalog");
            datasave = new InMemoryDatasaveService();
            return new ProgressionModel(config, datasave, wallet, "test-progression");
        }

        private const string SequentialJson =
            "{\"catalogId\":\"test-sequential\",\"mode\":\"Sequential\",\"levels\":[" +
            "{\"id\":\"stage-1\",\"level\":1}," +
            "{\"id\":\"stage-2\",\"level\":2,\"rewardResourceId\":\"currency.gold\",\"rewardAmount\":50}," +
            "{\"id\":\"stage-3\",\"level\":3}]}";

        private const string ExperienceJson =
            "{\"catalogId\":\"test-experience\",\"mode\":\"Experience\",\"levels\":[" +
            "{\"id\":\"rank-1\",\"level\":1,\"requiredExperience\":0}," +
            "{\"id\":\"rank-2\",\"level\":2,\"requiredExperience\":100,\"rewardResourceId\":\"currency.gold\",\"rewardAmount\":50}," +
            "{\"id\":\"rank-3\",\"level\":3,\"requiredExperience\":300,\"rewardResourceId\":\"currency.gems\",\"rewardAmount\":5}]}";

        private sealed class FakeResourceWallet : IResourceWallet
        {
            public List<ResourceGrantRequest> Requests { get; } = new();
            public bool ShouldSucceed { get; set; } = true;

            public bool TryGrant(ResourceGrantRequest request)
            {
                Requests.Add(request);
                return ShouldSucceed;
            }

            public bool TryExchange(ResourceExchangeRequest request) => false;
        }

        private sealed class InMemoryDatasaveService : IDatasaveService
        {
            private readonly Dictionary<string, SaveData> data = new();

            public int SaveCount { get; private set; }

            public T Load<T>(string key = null) where T : SaveData, new()
            {
                if (data.TryGetValue(key, out SaveData saved))
                {
                    return (T)saved;
                }

                T created = new();
                data[key] = created;
                return created;
            }

            public void Save<T>(T saved, string key = null) where T : SaveData
            {
                data[key] = saved;
                SaveCount++;
            }

            public void SaveAll()
            {
            }

            public bool Exists(string key) => data.ContainsKey(key);
            public void Delete(string key) => data.Remove(key);
            public void DeleteAll() => data.Clear();
        }
    }
}
