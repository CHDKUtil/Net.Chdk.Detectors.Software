using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Net.Chdk.Detectors.Software
{
    sealed class FileSystemSoftwareDetector : ISoftwareDetector
    {
        private static string Version => "1.0";

        private IEnumerable<IProductDetector> ProductDetectors { get; }

        public FileSystemSoftwareDetector(IEnumerable<IProductDetector> productDetectors)
        {
            ProductDetectors = productDetectors;
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo)
        {
            string diskbootPath = cardInfo.GetDiskbootPath();
            if (!File.Exists(diskbootPath))
                return null;

            return new SoftwareInfo
            {
                Version = Version,
                Product = GetProduct(cardInfo),
            };
        }

        private ProductInfo GetProduct(CardInfo cardInfo)
        {
            return ProductDetectors
                .Select(d => d.GetProduct(cardInfo))
                .FirstOrDefault(p => p != null);
        }
    }
}
