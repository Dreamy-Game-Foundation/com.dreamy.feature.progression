using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreamy.DataConfig;
using Dreamy.Datasave;
using Dreamy.Economy;
using Dreamy.Economy.Rewards;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Dreamy.Progression.Tests
{
    public sealed class ProgressionRewardTests
    {
        internal static ProgressionCatalogConfig Catalog(int levels = 4)
        {
            var config = JsonConvert.DeserializeObject<ProgressionCatalogConfig>(JsonConvert.SerializeObject(new
            {
                catalogId = "ranks", mode = "Experience",
                levels = Enumerable.Range(1, levels).Select(i => new
                {
                    id = "rank-" + i, level = i, requiredExperience = (i - 1) * 100,
                    bundle = i == 1 ? null : new
                    {
                        schemaVersion = 1, resolverVersion = "static-v1",
                        entries = new[]
                        {
                            new { entryId = "coin", kindId = "currency", targetId = "currency.coin", amount = 100 },
                            new { entryId = "gem", kindId = "currency", targetId = "currency.gem", amount = 5 },
                            new { entryId = "card", kindId = "card", targetId = "card.knight", amount = 2 },
                            new { entryId = "chest", kindId = "chest", targetId = "chest.silver", amount = 1 },
                            new { entryId = "skin", kindId = "cosmetic", targetId = "skin.red", amount = 1 }
                        }
                    }
                })
            }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            config.Initialize("test");
            return config;
        }

        private sealed class Destination : IRewardDestination
        {
            public string KindId { get; }
            private readonly ProgressionRewardTestStore store;
            private Inventory data;
            public bool Fail;
            public Action OnDeliver;
            public Destination(string kind, ProgressionRewardTestStore store)
            { KindId = kind; this.store = store; data = store.Load<Inventory>(kind); }
            public long Amount => data.Amount;
            public RewardDeliveryResult Validate(IReadOnlyList<RewardDeliveryRequest> requests)
            {
                foreach (var request in requests)
                    if (data.Receipts.TryGetValue(request.TransactionId, out var fingerprint) && fingerprint != request.Fingerprint)
                        return RewardDeliveryResult.Failed(RewardDeliveryReason.PayloadMismatch);
                return RewardDeliveryResult.Delivered();
            }
            public RewardDeliveryResult Deliver(RewardDeliveryRequest request)
            {
                OnDeliver?.Invoke();
                if (Fail) return RewardDeliveryResult.Pending(RewardDeliveryReason.DestinationUnavailable);
                var validation = Validate(new[] { request });
                if (!validation.IsSuccess) return validation;
                if (data.Receipts.ContainsKey(request.TransactionId)) return RewardDeliveryResult.AlreadyDelivered();
                var candidate = JsonConvert.DeserializeObject<Inventory>(JsonConvert.SerializeObject(data));
                candidate.Amount = checked(candidate.Amount + request.Entry.Amount);
                candidate.Receipts.Add(request.TransactionId, request.Fingerprint);
                store.Save(candidate, KindId);
                data = candidate;
                return RewardDeliveryResult.Delivered();
            }
        }
        public sealed class Inventory : SaveData
        {
            public long Amount;
            public Dictionary<string, string> Receipts = new();
        }
        private sealed class Harness
        {
            public ProgressionRewardTestStore Store;
            public DatasaveResourceWallet Wallet;
            public Destination Card, Chest, Cosmetic;
            public ProgressionModel Model;
            public Harness(ProgressionRewardTestStore store = null, ProgressionCatalogConfig catalog = null,
                IProgressionLegacyRewardMigration migration = null)
            {
                Store = store ?? new ProgressionRewardTestStore();
                Wallet = new DatasaveResourceWallet(Store, "wallet");
                Card = new Destination("card", Store); Chest = new Destination("chest", Store); Cosmetic = new Destination("cosmetic", Store);
                Model = new ProgressionModel(catalog ?? Catalog(), Store,
                    new RewardDeliveryGateway(new IRewardDestination[] { Wallet, Card, Chest, Cosmetic }), "progress", migration);
            }
            public long Coins => Wallet.GetBalance(new ResourceId("currency.coin"));
        }

        [Test]
        public void MultiLevel_CommitsAllIntentsBeforeAnyDestination_AndDeliversAllKinds()
        {
            var h = new Harness();
            h.Card.OnDeliver = () =>
            {
                var saved = h.Store.Load<ProgressionSaveData>("progress");
                Assert.That(saved.TotalExperience, Is.EqualTo(300));
                Assert.That(saved.MilestoneRewards.Count, Is.EqualTo(3));
                Assert.That(saved.ProcessedGrantIds, Contains.Item("match"));
            };
            var result = h.Model.GrantExperience("match", 300);
            Assert.That(result.LevelsAdvanced, Is.EqualTo(3));
            Assert.That(h.Coins, Is.EqualTo(300));
            Assert.That(h.Wallet.GetBalance(new ResourceId("currency.gem")), Is.EqualTo(15));
            Assert.That(h.Card.Amount, Is.EqualTo(6));
            Assert.That(h.Chest.Amount, Is.EqualTo(3));
            Assert.That(h.Cosmetic.Amount, Is.EqualTo(3));
            Assert.That(h.Model.GetRewardStates().All(x => x.Status == ProgressionRewardStatus.Delivered), Is.True);
        }

        [Test]
        public void PartialBundle_RestartAndRetry_OnlyDeliversUnacknowledgedEntries()
        {
            var h = new Harness(); h.Card.Fail = true;
            Assert.That(h.Model.GrantExperience("match", 300).Succeeded, Is.True);
            Assert.That(h.Model.GetState().CurrentLevel, Is.EqualTo(4));
            Assert.That(h.Coins, Is.EqualTo(300));
            Assert.That(h.Model.GetRewardStates()[0].Entries.Select(x => x.Delivered), Is.EqualTo(new[] { true, true, false, false, false }));
            var restarted = new Harness(h.Store.Restart());
            Assert.That(restarted.Model.GrantExperience("match", 300).Status, Is.EqualTo(ProgressionAdvanceStatus.AlreadyProcessed));
            restarted.Model.RetryPendingRewards(); restarted.Model.RetryPendingRewards();
            Assert.That(restarted.Coins, Is.EqualTo(300));
            Assert.That(restarted.Card.Amount, Is.EqualTo(6));
            Assert.That(restarted.Model.GetRewardStates().All(x => x.Status == ProgressionRewardStatus.Delivered), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedXpSave_RequiresReload_AndNeverDeliversBeforeConfirmedCommit(bool afterWrite)
        {
            var h = new Harness();
            Action<string, SaveData> fail = (key, data) => { if (key == "progress") throw new IOException("disk"); };
            if (afterWrite) h.Store.AfterSave = fail; else h.Store.BeforeSave = fail;
            Assert.That(h.Model.GrantExperience("match", 300).Status, Is.EqualTo(ProgressionAdvanceStatus.PersistenceFailed));
            Assert.That(h.Coins, Is.Zero);
            Assert.That(h.Model.GetState().TotalExperience, Is.Zero);
            Assert.That(h.Model.GrantExperience("other", 100).Status, Is.EqualTo(ProgressionAdvanceStatus.RecoveryRequired));
            h.Store.BeforeSave = null; h.Store.AfterSave = null;
            h.Model.Reload();
            Assert.That(h.Model.GetState().TotalExperience, Is.EqualTo(afterWrite ? 300 : 0));
            var retry = h.Model.GrantExperience("match", 300);
            Assert.That(retry.Status, Is.EqualTo(afterWrite ? ProgressionAdvanceStatus.AlreadyProcessed : ProgressionAdvanceStatus.ExperienceGranted));
            h.Model.RetryPendingRewards();
            Assert.That(h.Coins, Is.EqualTo(300));
        }

        [TestCase(RewardJournalState.Resolved, false)]
        [TestCase(RewardJournalState.Resolved, true)]
        [TestCase(RewardJournalState.Delivering, false)]
        [TestCase(RewardJournalState.Delivering, true)]
        [TestCase(RewardJournalState.DeliveredAwaitingClaimCommit, false)]
        [TestCase(RewardJournalState.DeliveredAwaitingClaimCommit, true)]
        [TestCase(RewardJournalState.Completed, false)]
        [TestCase(RewardJournalState.Completed, true)]
        public void CrashAtEachJournalWrite_RecoversWithoutDuplicateEffects(RewardJournalState failState, bool afterWrite)
        {
            var h = new Harness();
            Action<string, SaveData> fail = (key, data) =>
            {
                if (key == "progress" && ((ProgressionSaveData)data).MilestoneRewards.Exists(r => r.Journal.State == failState))
                    throw new IOException("journal");
            };
            if (afterWrite) h.Store.AfterSave = fail; else h.Store.BeforeSave = fail;
            Assert.That(h.Model.GrantExperience("match", 100).Succeeded, Is.True);
            Assert.That(h.Model.RequiresReload, Is.True);
            var restart = new Harness(h.Store.Restart());
            restart.Model.RetryPendingRewards();
            Assert.That(restart.Coins, Is.EqualTo(100));
            Assert.That(restart.Card.Amount, Is.EqualTo(2));
            Assert.That(restart.Chest.Amount, Is.EqualTo(1));
            Assert.That(restart.Model.GetRewardStates()[0].Status, Is.EqualTo(ProgressionRewardStatus.Delivered));
            Assert.That(restart.Model.GetState().TotalExperience, Is.EqualTo(100));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DestinationPersistenceFailure_StopsWholeBatch_UntilAllOwnersRestart(bool afterWrite)
        {
            var h = new Harness();
            Action<string, SaveData> fail = (key, data) => { if (key == "wallet") throw new IOException("wallet disk"); };
            if (afterWrite) h.Store.AfterSave = fail; else h.Store.BeforeSave = fail;
            Assert.That(h.Model.GrantExperience("match", 300).Succeeded, Is.True);
            Assert.That(h.Model.RequiresReload, Is.True);
            Assert.That(h.Card.Amount, Is.Zero);
            Assert.That(h.Model.GetRewardStates()[0].Reason, Is.EqualTo(RewardDeliveryReason.PersistenceFailed));
            var restart = new Harness(h.Store.Restart()); restart.Model.RetryPendingRewards();
            Assert.That(restart.Coins, Is.EqualTo(300));
            Assert.That(restart.Card.Amount, Is.EqualTo(6));
        }

        [Test]
        public void BlockedMilestone_DoesNotPreventLaterMilestoneOrXp()
        {
            var config = JObject.Parse(JsonConvert.SerializeObject(Catalog()));
            config["levels"][1]["bundle"]["entries"][2]["kindId"] = "unknown";
            var h = new Harness(catalog: JsonConvert.DeserializeObject<ProgressionCatalogConfig>(config.ToString()));
            Assert.That(h.Model.GrantExperience("match", 200).Succeeded, Is.True);
            Assert.That(h.Model.GetRewardStates()[0].Reason, Is.EqualTo(RewardDeliveryReason.UnknownKind));
            Assert.That(h.Model.GetRewardStates()[1].Status, Is.EqualTo(ProgressionRewardStatus.Delivered));
            Assert.That(h.Model.GrantExperience("next", 100).Succeeded, Is.True);
        }

        [Test]
        public void ChangedRewardConfig_AfterRestart_DoesNotReplacePendingPayload()
        {
            var h = new Harness(); h.Card.Fail = true; h.Model.GrantExperience("match", 100);
            var config = JObject.Parse(JsonConvert.SerializeObject(Catalog()));
            config["levels"][1]["bundle"]["entries"][0]["amount"] = 999;
            var restart = new Harness(h.Store.Restart(), JsonConvert.DeserializeObject<ProgressionCatalogConfig>(config.ToString()));
            restart.Model.RetryPendingRewards();
            Assert.That(restart.Coins, Is.EqualTo(100));
            Assert.That(restart.Model.GetRewardStates()[0].Entries[0].Reward.Amount, Is.EqualTo(100));
        }

        [Test]
        public void ChangedThreshold_BlocksAdvance_ButAllowsFrozenRewardRecovery()
        {
            var h = new Harness(); h.Card.Fail = true; h.Model.GrantExperience("match", 100);
            var config = JObject.Parse(JsonConvert.SerializeObject(Catalog()));
            config["levels"][2]["requiredExperience"] = 250;
            var restart = new Harness(h.Store.Restart(), JsonConvert.DeserializeObject<ProgressionCatalogConfig>(config.ToString()));
            Assert.That(restart.Model.RequiresMigration, Is.True);
            Assert.That(restart.Model.GrantExperience("next", 100).Status, Is.EqualTo(ProgressionAdvanceStatus.MigrationRequired));
            restart.Model.RetryPendingRewards();
            Assert.That(restart.Model.GetRewardStates()[0].Status, Is.EqualTo(ProgressionRewardStatus.Delivered));
        }

        private sealed class LegacyPolicy : IProgressionLegacyRewardMigration
        {
            public LegacyProgressionRewardStatus Status;
            public string LastId;
            public LegacyProgressionRewardStatus Reconcile(string id, ResolvedRewardBundle reward) { LastId = id; return Status; }
        }

        [TestCase(LegacyProgressionRewardStatus.NotDelivered, 100)]
        [TestCase(LegacyProgressionRewardStatus.AlreadyDelivered, 0)]
        [TestCase(LegacyProgressionRewardStatus.Unverifiable, 0)]
        public void V1Migration_PreservesReachedLevels_AndReconcilesFutureLegacyEffects(LegacyProgressionRewardStatus status, long coins)
        {
            var store = new ProgressionRewardTestStore();
            store.Disk["progress"] = "{\"catalogId\":\"ranks\",\"currentLevelIndex\":1,\"totalExperience\":100,\"processedGrantIds\":[\"old\"]}";
            store.Versions["progress"] = 1;
            var policy = new LegacyPolicy { Status = status };
            var h = new Harness(store, migration: policy);
            Assert.That(h.Model.GetState().CurrentLevel, Is.EqualTo(2));
            Assert.That(h.Model.GetRewardStates(), Is.Empty);
            Assert.That(h.Model.GrantExperience("old", 100).Status, Is.EqualTo(ProgressionAdvanceStatus.AlreadyProcessed));
            Assert.That(h.Model.GrantExperience("next", 100).Succeeded, Is.True);
            Assert.That(policy.LastId, Is.EqualTo("progression:ranks:level:rank-3"));
            Assert.That(h.Coins, Is.EqualTo(coins));
            var restart = new Harness(store.Restart(), migration: policy);
            restart.Model.RetryPendingRewards();
            Assert.That(restart.Coins, Is.EqualTo(coins));
            Assert.That(restart.Model.GetRewardStates()[0].Status, Is.EqualTo(status == LegacyProgressionRewardStatus.Unverifiable
                ? ProgressionRewardStatus.Blocked : ProgressionRewardStatus.Delivered));
        }

        [Test]
        public void GrantHistory_BeyondOldLimit_RetainsReplayProtectionAfterRestart()
        {
            var h = new Harness();
            for (int i = 0; i < 4100; i++) Assert.That(h.Model.GrantExperience("xp-" + i, 1).Succeeded, Is.True);
            var restart = new Harness(h.Store.Restart());
            Assert.That(restart.Model.GrantExperience("xp-0", 1).Status, Is.EqualTo(ProgressionAdvanceStatus.AlreadyProcessed));
            Assert.That(restart.Model.GetState().TotalExperience, Is.EqualTo(4100));
        }

        [Test]
        public void ListenersAndReentry_CannotInvalidateDurableAdvance()
        {
            var h = new Harness();
            ProgressionAdvanceStatus nested = default;
            h.Model.ProgressChanged += _ => { nested = h.Model.GrantExperience("nested", 100).Status; throw new Exception("UI"); };
            h.Model.RewardsChanged += () => throw new Exception("reward UI");
            Assert.That(h.Model.GrantExperience("match", 100).Succeeded, Is.True);
            Assert.That(nested, Is.EqualTo(ProgressionAdvanceStatus.Busy));
            Assert.That(h.Model.GetRewardStates()[0].IsBusy, Is.False);
            Assert.That(h.Coins, Is.EqualTo(100));
        }

        [Test]
        public void DuplicateEntryAndMixedConfig_AreRejected()
        {
            var json = JObject.Parse(JsonConvert.SerializeObject(Catalog()));
            json["levels"][1]["bundle"]["entries"][1]["entryId"] = "coin";
            Assert.Throws<DataConfigException>(() => JsonConvert.DeserializeObject<ProgressionCatalogConfig>(json.ToString()).Initialize("test"));
            json = JObject.Parse(JsonConvert.SerializeObject(Catalog()));
            json["levels"][1]["rewardAmount"] = 0;
            Assert.Throws<DataConfigException>(() => JsonConvert.DeserializeObject<ProgressionCatalogConfig>(json.ToString()).Initialize("test"));
        }

        [TestCase("amount", "0")]
        [TestCase("kindId", "\"\"")]
        [TestCase("targetId", "\"\"")]
        public void InvalidBundleEntry_IsRejected(string field, string value)
        {
            var json = JObject.Parse(JsonConvert.SerializeObject(Catalog()));
            json["levels"][1]["bundle"]["entries"][0][field] = JToken.Parse(value);
            Assert.Throws<DataConfigException>(() => JsonConvert.DeserializeObject<ProgressionCatalogConfig>(json.ToString()).Initialize("test"));
        }

        [TestCase("\"rewardResourceId\":\"\",\"rewardAmount\":0")]
        [TestCase("\"rewardAmount\":0")]
        [TestCase("\"rewardResourceId\":null,\"rewardAmount\":0")]
        public void LegacyNoRewardDefaults_RemainCompatible(string fields)
        {
            string json = "{\"catalogId\":\"legacy\",\"mode\":\"Sequential\",\"levels\":[{\"id\":\"first\",\"level\":1," + fields + "}]}";
            var config = JsonConvert.DeserializeObject<ProgressionCatalogConfig>(json);
            Assert.DoesNotThrow(() => config.Initialize("test"));
            Assert.That(config.Levels[0].HasReward, Is.False);
        }

        [Test]
        public void EmptyBundleAndInitialLevelReward_AreRejected()
        {
            var json = JObject.Parse(JsonConvert.SerializeObject(Catalog()));
            json["levels"][1]["bundle"]["entries"] = new JArray();
            Assert.Throws<DataConfigException>(() => JsonConvert.DeserializeObject<ProgressionCatalogConfig>(json.ToString()).Initialize("test"));
            json = JObject.Parse(JsonConvert.SerializeObject(Catalog()));
            json["levels"][0]["bundle"] = json["levels"][1]["bundle"].DeepClone();
            Assert.Throws<DataConfigException>(() => JsonConvert.DeserializeObject<ProgressionCatalogConfig>(json.ToString()).Initialize("test"));
        }

        [Test]
        public void NoRewardMilestone_AdvancesWithoutCreatingEmptyBundle()
        {
            var json = JObject.Parse(JsonConvert.SerializeObject(Catalog()));
            ((JObject)json["levels"][1]).Remove("bundle");
            var h = new Harness(catalog: JsonConvert.DeserializeObject<ProgressionCatalogConfig>(json.ToString()));
            Assert.That(h.Model.GrantExperience("match", 100).Succeeded, Is.True);
            Assert.That(h.Model.GetRewardStates(), Is.Empty);
            Assert.That(h.Coins, Is.Zero);
        }

        [Test]
        public void DestinationReentry_IsBusyAndCannotAdvance()
        {
            var h = new Harness();
            RewardDeliveryReason reason = default;
            ProgressionAdvanceStatus status = default;
            h.Card.OnDeliver = () =>
            {
                reason = h.Model.RetryReward("rank-2").Reason;
                status = h.Model.GrantExperience("nested", 100).Status;
            };
            h.Model.GrantExperience("match", 100);
            Assert.That(reason, Is.EqualTo(RewardDeliveryReason.Busy));
            Assert.That(status, Is.EqualTo(ProgressionAdvanceStatus.Busy));
            Assert.That(h.Model.GetState().TotalExperience, Is.EqualTo(100));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CorruptJournalOrPayload_IsBlockedWithoutDelivery_AndUiCanRender(bool payload)
        {
            var h = new Harness(); h.Card.Fail = true; h.Model.GrantExperience("match", 100);
            var saved = h.Store.Load<ProgressionSaveData>("progress");
            if (payload) saved.MilestoneRewards[0].Payload[0].Amount = 999;
            else saved.MilestoneRewards[0].Journal.Entries[0].Acknowledged = false;
            h.Store.Save(saved, "progress");
            var restart = new Harness(h.Store.Restart());
            Assert.That(restart.Model.GetRewardStates()[0].Status, Is.EqualTo(ProgressionRewardStatus.Blocked));
            Assert.That(restart.Model.RetryReward("rank-2").IsSuccess, Is.False);
            Assert.That(restart.Coins, Is.EqualTo(100));
            Assert.That(restart.Card.Amount, Is.Zero);
        }

        [Test]
        public void SaveLoadSnapshots_DoNotAliasLiveState()
        {
            var h = new Harness(); h.Card.Fail = true; h.Model.GrantExperience("match", 100);
            var detached = h.Store.Load<ProgressionSaveData>("progress");
            detached.TotalExperience = 999;
            detached.MilestoneRewards[0].Payload[0].Amount = 1;
            Assert.That(h.Model.GetState().TotalExperience, Is.EqualTo(100));
            Assert.That(h.Model.GetRewardStates()[0].Entries[0].Reward.Amount, Is.EqualTo(100));
        }

        private sealed class BoolWallet : IResourceWallet
        {
            public int Calls;
            public bool TryGrant(ResourceGrantRequest request) { Calls++; return true; }
            public bool TryExchange(ResourceExchangeRequest request) => false;
        }

        [Test]
        public void BoolOnlyWallet_LeavesEntitlementPendingUntilDurableAdapterIsProvided()
        {
            var store = new ProgressionRewardTestStore(); var wallet = new BoolWallet();
            var model = new ProgressionModel(Catalog(), store, wallet, "progress");
            Assert.That(model.GrantExperience("match", 100).Succeeded, Is.True);
            Assert.That(wallet.Calls, Is.Zero);
            Assert.That(model.GetRewardStates()[0].Reason, Is.EqualTo(RewardDeliveryReason.DestinationUnavailable));
            var restart = new Harness(store.Restart()); restart.Model.RetryPendingRewards();
            Assert.That(restart.Coins, Is.EqualTo(100));
        }

        private sealed class RewardView : IProgressionRewardView
        {
            public int Renders;
            public void Render(IReadOnlyList<ProgressionRewardState> states) { Renders++; }
        }

        [Test]
        public void RewardPresenter_BindsOnce_Unsubscribes_AndQueriesPersistedState()
        {
            var h = new Harness(); var view = new RewardView();
            var presenter = new ProgressionRewardPresenter(h.Model, view);
            presenter.Show(); presenter.Show();
            h.Model.GrantExperience("match", 100);
            Assert.That(view.Renders, Is.GreaterThan(2));
            presenter.Dispose(); int rendered = view.Renders;
            h.Model.GrantExperience("next", 100);
            Assert.That(view.Renders, Is.EqualTo(rendered));
            presenter.Show(); Assert.That(view.Renders, Is.EqualTo(rendered + 1));
            presenter.Dispose();
        }

        [Test]
        public void FutureVersion_IsNotOverwritten_AndMissingV2StructureDoesNotReset()
        {
            var h = new Harness();
            string json = h.Store.Disk["progress"];
            h.Store.Versions["progress"] = 99;
            Assert.Throws<InvalidOperationException>(() => new Harness(h.Store.Restart()));
            Assert.That(h.Store.Disk["progress"], Is.EqualTo(json));
            h.Store.Versions["progress"] = 2;
            var saved = h.Store.Load<ProgressionSaveData>("progress"); saved.Levels.Clear(); h.Store.Save(saved, "progress");
            Assert.Throws<InvalidOperationException>(() => new Harness(h.Store.Restart()));
        }

        [Test]
        public void DiskEnvelope_V1MigrationAndV2Restart_PreserveProgressAndJournal()
        {
            string directory = Path.Combine(Path.GetTempPath(), "dreamy-progression-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "progress.json"), JsonConvert.SerializeObject(new
                {
                    FormatVersion = 1, DataType = typeof(ProgressionSaveData).AssemblyQualifiedName,
                    DataVersion = 1, SavedAtUtcTicks = DateTime.UtcNow.Ticks,
                    Payload = "{\"catalogId\":\"ranks\",\"currentLevelIndex\":1,\"totalExperience\":100,\"processedGrantIds\":[\"old\"]}"
                }));
                var service = new DatasaveService(new DatasaveOptions { DirectoryName = directory });
                var wallet = new DatasaveResourceWallet(service, "wallet");
                // Missing inventory destinations leave a durable intent; migration remains conservative.
                var model = new ProgressionModel(Catalog(), service, new RewardDeliveryGateway(new[] { wallet }), "progress");
                Assert.That(model.GetState().CurrentLevel, Is.EqualTo(2));
                model.GrantExperience("next", 100);
                var restartedSave = new DatasaveService(new DatasaveOptions { DirectoryName = directory });
                var restored = restartedSave.Load<ProgressionSaveData>("progress");
                Assert.That(restored.Version, Is.EqualTo(2));
                Assert.That(restored.TotalExperience, Is.EqualTo(200));
                Assert.That(restored.MilestoneRewards[0].Journal.IsValid(), Is.True);
                Assert.That(restored.LegacyHistory, Is.True);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
