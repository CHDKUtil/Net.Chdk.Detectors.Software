using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using Net.Chdk.Providers.Boot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    sealed class FileSystemSoftwareDetector : IInnerSoftwareDetector
    {
        private static Version Version => new Version("1.0");

        private ILogger Logger { get; }
        private IEnumerable<IProductDetector> ProductDetectors { get; }
        private IBootProviderResolver BootProviderResolver { get; }

        public FileSystemSoftwareDetector(IEnumerable<IProductDetector> productDetectors, IBootProviderResolver bootProviderResolver, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<FileSystemSoftwareDetector>();
            ProductDetectors = productDetectors;
            BootProviderResolver = bootProviderResolver;
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo, string categoryName, IProgress<double> progress, CancellationToken token)
        {
            Logger.LogTrace("Detecting software from {0} file system", cardInfo.DriveLetter);

            var bootProvider = BootProviderResolver.GetBootProvider(categoryName);
            var rootPath = cardInfo.GetRootPath();
            var filePath = Path.Combine(rootPath, bootProvider.FileName);
            if (!File.Exists(filePath))
                return null;
            return new SoftwareInfo
            {
                Version = Version,
                Product = GetProduct(cardInfo, categoryName),
            };
        }

        private SoftwareProductInfo GetProduct(CardInfo cardInfo, string categoryName)
        {
            return ProductDetectors
                .Where(d => categoryName.Equals(d.CategoryName, StringComparison.InvariantCulture))
                .Select(d => d.GetProduct(cardInfo))
                .FirstOrDefault(p => p != null);
        }
    }
}
