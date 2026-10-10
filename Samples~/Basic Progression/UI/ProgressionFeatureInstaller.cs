using System;
using Dreamy.DataConfig;
using Dreamy.Datasave;
using Dreamy.Economy;
using Dreamy.Progression;
using Dreamy.UI;
using UnityEngine;

namespace Dreamy.Feature.Progression.Integration
{
    public static class ProgressionFeatureInstaller
    {
        public static void RegisterConfig(IDataConfigService config) => ProgressionInstaller.RegisterConfig(config);

        public static IProgressionService Install(PanelPresenterFactory factory, ProgressionCatalogConfig config, IDatasaveService save, IResourceWallet wallet, string saveKey = ProgressionInstaller.DefaultSaveKey)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return Install(factory, ProgressionInstaller.Install(config, save, wallet, saveKey));
        }

        public static IProgressionService Install(PanelPresenterFactory factory, IProgressionService service)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (service == null) throw new ArgumentNullException(nameof(service));
            factory.Register<ProgressionHud>(view => new ProgressionPresenter(service, view));
            return service;
        }

        public static PresenterViewHost Attach(PanelPresenterFactory factory, ProgressionHud view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            var host = view.GetComponent<PresenterViewHost>();
            if (host == null) host = view.gameObject.AddComponent<PresenterViewHost>();
            host.Initialize(factory, view);
            return host;
        }
    }
}
