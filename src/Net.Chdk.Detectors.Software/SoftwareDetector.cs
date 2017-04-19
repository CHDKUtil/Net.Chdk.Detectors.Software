using Net.Chdk.Model.Software;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Net.Chdk.Detectors.Software
{
    public sealed class SoftwareDetector
    {
        private IEnumerable<IProductDetector> ProductDetectors { get; }

        public SoftwareDetector(IEnumerable<IProductDetector> productDetectors)
        {
            ProductDetectors = productDetectors;
        }

        public SoftwareInfo GetSoftware(string driveLetter)
        {
            return GetSoftwareFromMetadata(driveLetter)
                ?? GetSoftwareFromFileSystem(driveLetter);
        }

        private SoftwareInfo GetSoftwareFromMetadata(string driveLetter)
        {
            var metadataPath = Path.Combine(driveLetter, "METADATA");
            var softwarePath = Path.Combine(metadataPath, "SOFTWARE.JSN");
            if (!File.Exists(softwarePath))
                return null;

            using (var reader = File.OpenRead(softwarePath))
            {
                return JsonObject.Deserialize<SoftwareInfo>(reader);
            }
        }

        private SoftwareInfo GetSoftwareFromFileSystem(string driveLetter)
        {
            var diskbootPath = Path.Combine(driveLetter, "DISKBOOT.BIN");
            if (!File.Exists(diskbootPath))
                return null;

            return new SoftwareInfo
            {
                Version = GetVersion(),
                Product = GetProduct(driveLetter),
            };
        }

        private static string GetVersion()
        {
            return "1.0";
        }

        private ProductInfo GetProduct(string driveLetter)
        {
            return ProductDetectors
                .Select(d => d.GetProduct(driveLetter))
                .FirstOrDefault(p => p != null);
        }
    }
}
