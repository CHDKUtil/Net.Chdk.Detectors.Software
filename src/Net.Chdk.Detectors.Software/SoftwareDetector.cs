using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using System.Collections.Generic;
using System.Linq;

namespace Net.Chdk.Detectors.Software
{
    public sealed class SoftwareDetector : ISoftwareDetector
    {
        private IEnumerable<ISoftwareDetector> SoftwareDetectors { get; }

        public SoftwareDetector(IEnumerable<IProductDetector> productDetectors, ILoggerFactory loggerFactory)
        {
            SoftwareDetectors = new ISoftwareDetector[]
            {
                new MetadataSoftwareDetector(loggerFactory),
                new FileSystemSoftwareDetector(productDetectors)
            };
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo)
        {
            return SoftwareDetectors
                .Select(d => d.GetSoftware(cardInfo))
                .FirstOrDefault(s => s != null);
        }
    }
}
