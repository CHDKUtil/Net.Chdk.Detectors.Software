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

        public SoftwareInfo GetSoftware(string driveLetter)
        {
            var diskbootPath = Path.Combine(driveLetter, "DISKBOOT.BIN");
            if (!File.Exists(diskbootPath))
                return null;

            return new SoftwareInfo
            {
                Version = Version,
                Product = GetProduct(driveLetter),
            };
        }

        private ProductInfo GetProduct(string driveLetter)
        {
            return ProductDetectors
                .Select(d => d.GetProduct(driveLetter))
                .FirstOrDefault(p => p != null);
        }
    }
}
