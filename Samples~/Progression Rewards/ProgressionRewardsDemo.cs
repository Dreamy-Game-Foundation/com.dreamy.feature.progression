using System;
using System.Linq;
using Dreamy.Datasave;
using Dreamy.Economy;
using Dreamy.Economy.Rewards;
using Newtonsoft.Json;
using UnityEngine;

namespace Dreamy.Progression.Samples
{
    // Attach to an empty GameObject in a sample scene. Uses its own save directory.
    public sealed class ProgressionRewardsDemo : MonoBehaviour
    {
        [SerializeField] private string saveDirectory = "DreamyProgressionRewardsDemo";
        private ProgressionModel model;
        private DatasaveResourceWallet wallet;
        private bool failCard;
        private FaultStore store;
        private string message;
        private Vector2 scroll;
        private void Start() => ReloadOwners();
        private void ReloadOwners()
        {
            var asset = Resources.Load<TextAsset>("progressionRewardsCatalog");
            if (asset == null) throw new InvalidOperationException("Import the complete Progression Rewards sample.");
            var config = JsonConvert.DeserializeObject<ProgressionCatalogConfig>(asset.text);
            config.Initialize("progressionRewardsCatalog");
            store = new FaultStore(new DatasaveService(new DatasaveOptions { DirectoryName = saveDirectory }));
            wallet = new DatasaveResourceWallet(store, "wallet", rewardDefinitions: new[]
            {
                new ResourceRewardDefinition("card", new ResourceId("card.knight")),
                new ResourceRewardDefinition("chest", new ResourceId("chest.silver")),
                new ResourceRewardDefinition("cosmetic", new ResourceId("skin.hero.red"), unique: true)
            });
            foreach (var pair in new[] { (kind: "card", target: "card.knight"), (kind: "chest", target: "chest.silver"), (kind: "cosmetic", target: "skin.hero.red") })
            {
                string key = "progression-rewards-" + pair.kind;
                if (!store.Exists(key)) continue;
                var legacy = store.Load<DemoInventorySave>(key);
                if (legacy.Count < 0 || legacy.Receipts == null) throw new InvalidOperationException("Invalid legacy inventory.");
                var amounts = legacy.Count > 0 ? new[] { new ResourceAmount(new ResourceId(pair.target), legacy.Count) } : Array.Empty<ResourceAmount>();
                var receipts = legacy.Receipts.Select(r => new RewardReceiptSaveEntry { TransactionId = r.Key, Fingerprint = r.Value }).ToArray();
                var result = wallet.ImportInventory("progression-demo-inventory-v1/" + pair.kind, amounts, receipts);
                if (!result.IsSuccess) throw new InvalidOperationException("Inventory import failed: " + result.Reason);
            }
            var gateway = new RewardDeliveryGateway(new[]
            {
                wallet.CreateRewardDestination("currency"), wallet.CreateRewardDestination("card"),
                wallet.CreateRewardDestination("chest"), wallet.CreateRewardDestination("cosmetic")
            });
            model = new ProgressionModel(config, store, new FaultGateway(this, gateway), "progression");
            model.RetryPendingRewards();
            message = "Loaded owners and recovered pending rewards.";
        }
        private void OnGUI()
        {
            if (model == null) return;
            // IMGUI uses top-left coordinates; Screen.safeArea uses bottom-left.
            Rect safe = Screen.safeArea;
            GUILayout.BeginArea(new Rect(safe.x + 12, Screen.height - safe.yMax + 12, safe.width - 24, safe.height - 24), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            var state = model.GetState();
            GUILayout.Label($"Level {state.CurrentLevel} | XP {state.TotalExperience} | Requires reload: {model.RequiresReload}");
            GUILayout.Label($"Coin {wallet.GetBalance(new ResourceId("currency.coin"))} / Gem {wallet.GetBalance(new ResourceId("currency.gem"))} / Card {wallet.GetBalance(new ResourceId("card.knight"))} / Chest {wallet.GetBalance(new ResourceId("chest.silver"))} / Cosmetic {wallet.GetBalance(new ResourceId("skin.hero.red"))}");
            if (GUILayout.Button("Grant 300 XP (cross several levels)"))
                message = model.GrantExperience(Guid.NewGuid().ToString("N"), 300).Status.ToString();
            if (GUILayout.Button("Fail next Card delivery")) { failCard = true; message = "Card failure armed."; }
            if (GUILayout.Button("Fail journal ack after destination saved")) { store.FailAck = true; message = "Ack failure armed."; }
            if (GUILayout.Button("Fail final reward completion save")) { store.FailComplete = true; message = "Completion failure armed."; }
            if (GUILayout.Button("Retry pending")) message = string.Join(", ", model.RetryPendingRewards().Select(r => r.Status + ":" + r.Reason));
            if (GUILayout.Button("Reload owners and recover (also works after app restart)")) ReloadOwners();
            GUILayout.Label(message ?? "");
            foreach (var reward in model.GetRewardStates())
            {
                GUILayout.Label($"{reward.MilestoneId}: {reward.Status} ({reward.Reason})");
                foreach (var entry in reward.Entries)
                    GUILayout.Label($"  {entry.Reward.TargetId} x{entry.Reward.Amount}: {(entry.Delivered ? "Acknowledged" : "Pending")}");
                GUI.enabled = reward.CanRetry;
                if (GUILayout.Button("Retry " + reward.MilestoneId)) message = model.RetryReward(reward.MilestoneId).Status.ToString();
                GUI.enabled = true;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private sealed class FaultGateway : IRewardDeliveryGateway
        {
            private readonly ProgressionRewardsDemo demo;
            private readonly IRewardDeliveryGateway inner;
            public FaultGateway(ProgressionRewardsDemo demo, IRewardDeliveryGateway inner) { this.demo = demo; this.inner = inner; }
            public RewardDeliveryResult Validate(string id, string fingerprint, ResolvedRewardBundle bundle) => inner.Validate(id, fingerprint, bundle);
            public RewardDeliveryResult Deliver(RewardDeliveryRequest request)
            {
                if (demo.failCard && request.Entry.KindId == "card")
                { demo.failCard = false; return RewardDeliveryResult.Pending(RewardDeliveryReason.DestinationUnavailable); }
                return inner.Deliver(request);
            }
        }

        private sealed class FaultStore : IDatasaveService
        {
            private readonly IDatasaveService inner;
            public bool FailAck, FailComplete;
            private ProgressionSaveData last;
            public FaultStore(IDatasaveService inner) => this.inner = inner;
            public T Load<T>(string key = null) where T : SaveData, new()
            {
                T loaded = inner.Load<T>(key);
                if (loaded is ProgressionSaveData progression) last = progression.Clone();
                return loaded;
            }
            public void Save<T>(T data, string key = null) where T : SaveData
            {
                if (data is ProgressionSaveData progression)
                {
                    if (FailAck && progression.MilestoneRewards.Any(r => r.Journal.Entries.Count(e => e.Acknowledged) >
                        (last?.MilestoneRewards.FirstOrDefault(old => old.MilestoneId == r.MilestoneId)?.Journal.Entries.Count(e => e.Acknowledged) ?? 0)))
                    { FailAck = false; throw new System.IO.IOException("Demo: failed journal ack after destination save."); }
                    if (FailComplete && progression.MilestoneRewards.Any(r => r.Journal.State == RewardJournalState.Completed &&
                        last?.MilestoneRewards.FirstOrDefault(old => old.MilestoneId == r.MilestoneId)?.Journal.State != RewardJournalState.Completed))
                    { FailComplete = false; throw new System.IO.IOException("Demo: failed completion save."); }
                }
                inner.Save(data, key);
                if (data is ProgressionSaveData committed) last = committed.Clone();
            }
            public void SaveAll() => inner.SaveAll();
            public bool Exists(string key) => inner.Exists(key);
            public void Delete(string key) => inner.Delete(key);
            public void DeleteAll() => inner.DeleteAll();
        }
    }
}
