using System;
using Dreamy.Core;
using Dreamy.DataConfig;
using Dreamy.Datasave;
using Dreamy.Economy;

namespace Dreamy.Progression
{
    public static class ProgressionInstaller
    {
        public const string DefaultSaveKey = "progression";

        public static void RegisterConfig(IDataConfigService dataConfigService)
        {
            if (dataConfigService == null)
            {
                throw new ArgumentNullException(nameof(dataConfigService));
            }

            dataConfigService.Register<ProgressionCatalogConfig>("progressionCatalog");
        }

        public static IProgressionService Install(string saveKey = DefaultSaveKey)
        {
            IDataConfigService configService = ServiceLocator.Get<IDataConfigService>();
            IDatasaveService datasaveService = ServiceLocator.Get<IDatasaveService>();
            IResourceWallet resourceWallet = ServiceLocator.Get<IResourceWallet>();
            return Install(
                configService.GetTable<ProgressionCatalogConfig>(),
                datasaveService,
                resourceWallet,
                saveKey);
        }

        public static IProgressionService Install(
            ProgressionCatalogConfig catalog,
            IDatasaveService datasaveService,
            IResourceWallet resourceWallet,
            string saveKey = DefaultSaveKey)
        {
            ProgressionModel service = new(catalog, datasaveService, resourceWallet, saveKey);
            ServiceLocator.Register<IProgressionService>(service);
            return service;
        }
    }
}
