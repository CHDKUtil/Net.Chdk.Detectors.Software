using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Net.Chdk.Detectors.Software
{
    sealed class FileSystemSoftwareDetector : IInnerSoftwareDetector
    {
        private static Version Version => new Version("1.0");

        private ILogger Logger { get; }
        private IEnumerable<IProductDetector> ProductDetectors { get; }

        public FileSystemSoftwareDetector(IEnumerable<IProductDetector> productDetectors, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<FileSystemSoftwareDetector>();
            ProductDetectors = productDetectors;
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo)
        {
            Logger.LogTrace("Detecting software from {0} file system", cardInfo.DriveLetter);

            string diskbootPath = cardInfo.GetDiskbootPath();
            if (!File.Exists(diskbootPath))
                return null;

            return new SoftwareInfo
            {
                Version = Version,
                Product = GetProduct(cardInfo),
            };
        }

        private SoftwareProductInfo GetProduct(CardInfo cardInfo)
        {
            return ProductDetectors
                .Select(d => d.GetProduct(cardInfo))
                .FirstOrDefault(p => p != null);
        }
    }
}
