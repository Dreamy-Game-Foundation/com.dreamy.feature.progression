using System;
using Dreamy.UI;

namespace Dreamy.Progression
{
    public sealed class ProgressionPresenter : IPanelPresenter
    {
        private readonly IProgressionService service;
        private readonly IProgressionView view;
        private bool bound;
        public ProgressionPresenter(IProgressionService service, IProgressionView view)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }
        public void Show()
        {
            if (!bound) { service.ProgressChanged += Render; bound = true; }
            Render(service.GetState());
        }
        public void Dispose()
        {
            if (!bound) return;
            bound = false;
            service.ProgressChanged -= Render;
        }
        private void Render(ProgressionState state) { if (bound) view.Render(state); }
    }
}
