using Net.Chdk.Model.Software;
using System.Collections.Generic;
using System.Linq;

namespace Net.Chdk.Detectors.Software
{
    public sealed class SoftwareDetector : ISoftwareDetector
    {
        private IEnumerable<ISoftwareDetector> SoftwareDetectors { get; }

        public SoftwareDetector(IEnumerable<IProductDetector> productDetectors)
        {
            SoftwareDetectors = new ISoftwareDetector[]
            {
                new MetadataSoftwareDetector(),
                new FileSystemSoftwareDetector(productDetectors)
            };
        }

        public SoftwareInfo GetSoftware(string driveLetter)
        {
            return SoftwareDetectors
                .Select(d => d.GetSoftware(driveLetter))
                .FirstOrDefault(s => s != null);
        }
    }
}
