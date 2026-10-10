using System;
using Cysharp.Threading.Tasks;
using Dreamy.Progression;
using Dreamy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Feature.Progression.Integration
{
    /// <summary>Sample host actions; real games decide when to advance or grant XP.</summary>
    public sealed class ProgressionDemoPanel : UIPanel
    {
        [SerializeField] private ProgressionHud hud;
        [SerializeField] private Button advanceButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text statusText;
        private IProgressionService service;
        public override bool CanBack => true;

        public void Configure(IProgressionService progression)
        {
            Unbind();
            service = progression ?? throw new ArgumentNullException(nameof(progression));
            hud.Bind(service);
            if (isActiveAndEnabled) Bind();
            Render(service.GetState());
        }

        private void OnEnable()
        {
            advanceButton.onClick.AddListener(Advance);
            closeButton.onClick.AddListener(Close);
            Bind();
        }

        private void Bind()
        {
            if (service == null) return;
            service.ProgressChanged -= Render;
            service.ProgressChanged += Render;
            hud.Bind(service);
            Render(service.GetState());
        }

        private void Unbind()
        {
            if (service != null) service.ProgressChanged -= Render;
            if (hud != null) hud.Bind(null);
        }

        private void Advance()
        {
            if (service == null) return;
            string id = "progression-demo/" + Guid.NewGuid().ToString("N");
            var result = service.GetState().Mode == ProgressionMode.Experience
                ? service.GrantExperience(id, 100)
                : service.AdvanceNext(id);
            statusText.text = result.Status.ToString();
        }

        private void Render(ProgressionState state)
        {
            advanceButton.interactable = !state.IsMaxLevel;
            advanceButton.GetComponentInChildren<TMP_Text>().text = state.Mode == ProgressionMode.Experience
                ? "Grant 100 XP" : "Complete Stage";
            statusText.text = state.IsMaxLevel ? "Maximum level reached" : "Complete a stage to advance";
        }

        private void Close() => Hide().Forget();
        protected override void OnDisable()
        {
            advanceButton.onClick.RemoveListener(Advance);
            closeButton.onClick.RemoveListener(Close);
            Unbind();
            base.OnDisable();
        }
        protected override void OnDestroy() { Unbind(); base.OnDestroy(); }
    }
}
