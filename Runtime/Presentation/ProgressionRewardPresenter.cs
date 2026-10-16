using System;
using Dreamy.UI;

namespace Dreamy.Progression
{
    public sealed class ProgressionRewardPresenter : IPanelPresenter
    {
        private readonly IProgressionRewardService service;
        private readonly IProgressionRewardView view;
        private bool bound;
        public ProgressionRewardPresenter(IProgressionRewardService service, IProgressionRewardView view)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }
        public void Show()
        {
            if (!bound) { service.RewardsChanged += Render; bound = true; }
            Render();
        }
        public void Dispose()
        {
            if (!bound) return;
            service.RewardsChanged -= Render;
            bound = false;
        }
        private void Render() { if (bound) view.Render(service.GetRewardStates()); }
    }
}
