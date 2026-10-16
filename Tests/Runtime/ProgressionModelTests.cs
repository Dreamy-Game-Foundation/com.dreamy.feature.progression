using System.Collections.Generic;
using System.Linq;
using Dreamy.Datasave;
using Dreamy.DataConfig;
using Dreamy.Economy;
using Dreamy.Economy.Rewards;
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
            Assert.That(wallet.Requests[0].TransactionId, Is.EqualTo(EntryTransaction("test-sequential", "stage-2")));
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
            Assert.That(wallet.Requests[0].TransactionId, Is.EqualTo(EntryTransaction("test-experience", "rank-2")));
            Assert.That(wallet.Requests[1].TransactionId, Is.EqualTo(EntryTransaction("test-experience", "rank-3")));
        }

        [Test]
        public void GrantExperience_WhenRewardFails_CommitsExperienceAndPendingReward()
        {
            FakeResourceWallet wallet = new() { ShouldSucceed = false };
            ProgressionModel model = CreateModel(ExperienceJson, wallet, out InMemoryDatasaveService datasave);

            ProgressionAdvanceResult result = model.GrantExperience("match-44", 100);

            Assert.That(result.Status, Is.EqualTo(ProgressionAdvanceStatus.ExperienceGranted));
            Assert.That(result.State.TotalExperience, Is.EqualTo(100));
            Assert.That(model.GetState().CurrentLevel, Is.EqualTo(2));
            Assert.That(model.GetRewardStates()[0].Status, Is.EqualTo(ProgressionRewardStatus.Pending));
            Assert.That(model.GrantExperience("match-44", 100).Status, Is.EqualTo(ProgressionAdvanceStatus.AlreadyProcessed));
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

        [TestCase(0L, 1)]
        [TestCase(99L, 1)]
        [TestCase(100L, 2)]
        [TestCase(101L, 2)]
        [TestCase(219L, 2)]
        [TestCase(220L, 3)]
        [TestCase(221L, 3)]
        [TestCase(360L, 4)]
        [TestCase(7920L, 25)]
        [TestCase(106919L, 99)]
        [TestCase(106920L, 100)]
        [TestCase(106921L, 100)]
        [TestCase(2147483647L, 100)]
        public void GrantExperience_WithIncreasingThresholds_ResolvesBoundariesWithoutMilestones(
            long experience, int expectedLevel)
        {
            var wallet = new FakeResourceWallet();
            var model = CreateModel(IncreasingThresholdJson(), wallet, out var datasave);
            if (experience > 0) model.GrantExperience("experience", experience);

            var config = JsonConvert.DeserializeObject<ProgressionCatalogConfig>(IncreasingThresholdJson());
            config.Initialize("progressionCatalog");
            var recreated = new ProgressionModel(config, datasave, wallet, "test-progression");

            Assert.That(recreated.GetState().CurrentLevel, Is.EqualTo(expectedLevel));
            Assert.That(recreated.GetState().TotalExperience, Is.EqualTo(experience));
            Assert.That(wallet.Requests, Is.Empty);
        }

        [Test]
        public void GrantExperience_WhenLongAdditionWouldOverflow_LeavesProgressAndSaveUntouched()
        {
            var wallet = new FakeResourceWallet();
            var model = CreateModel(IncreasingThresholdJson(), wallet, out var datasave);
            model.GrantExperience("near-limit", long.MaxValue - 1);
            int saves = datasave.SaveCount;

            var result = model.GrantExperience("overflow", 2);

            Assert.That(result.Status, Is.EqualTo(ProgressionAdvanceStatus.InvalidGrant));
            Assert.That(model.GetState().TotalExperience, Is.EqualTo(long.MaxValue - 1));
            Assert.That(datasave.SaveCount, Is.EqualTo(saves));
            Assert.That(wallet.Requests, Is.Empty);
        }

        private static string IncreasingThresholdJson() => JsonConvert.SerializeObject(new
        {
            catalogId = "increasing-thresholds",
            mode = "Experience",
            levels = Enumerable.Range(1, 100).Select(level => new
            {
                id = $"level-{level}",
                level,
                requiredExperience = 100L * (level - 1) + 10L * (level - 1) * (level - 2)
            })
        });

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

        private static string EntryTransaction(string catalog, string level) => RewardIdentity.EntryTransaction(
            RewardIdentity.Request("test-progression", "progression", catalog, "permanent", level), "legacy");

        private sealed class FakeResourceWallet : IResourceWallet, IRewardDestination
        {
            public List<ResourceGrantRequest> Requests { get; } = new();
            public bool ShouldSucceed { get; set; } = true;

            public bool TryGrant(ResourceGrantRequest request)
            {
                Requests.Add(request);
                return ShouldSucceed;
            }

            public bool TryExchange(ResourceExchangeRequest request) => false;
            public string KindId => "currency";
            public RewardDeliveryResult Validate(IReadOnlyList<RewardDeliveryRequest> requests) => RewardDeliveryResult.Delivered();
            public RewardDeliveryResult Deliver(RewardDeliveryRequest request) => TryGrant(new ResourceGrantRequest(
                request.TransactionId, new ResourceAmount(new ResourceId(request.Entry.TargetId), request.Entry.Amount)))
                ? RewardDeliveryResult.Delivered() : RewardDeliveryResult.Pending(RewardDeliveryReason.DestinationUnavailable);
        }

        private sealed class InMemoryDatasaveService : IDatasaveService
        {
            private readonly Dictionary<string, string> data = new();

            public int SaveCount { get; private set; }

            public T Load<T>(string key = null) where T : SaveData, new()
            {
                if (data.TryGetValue(key, out string saved))
                {
                    return JsonConvert.DeserializeObject<T>(saved);
                }

                T created = new();
                data[key] = JsonConvert.SerializeObject(created);
                return created;
            }

            public void Save<T>(T saved, string key = null) where T : SaveData
            {
                data[key] = JsonConvert.SerializeObject(saved);
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
