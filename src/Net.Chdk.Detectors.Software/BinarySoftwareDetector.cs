using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    sealed class BinarySoftwareDetector : IInnerSoftwareDetector, IBinarySoftwareDetector
    {
        private IEnumerable<IInnerBinarySoftwareDetector> SoftwareDetectors { get; }

        public BinarySoftwareDetector(IEnumerable<IInnerBinarySoftwareDetector> softwareDetectors)
        {
            SoftwareDetectors = softwareDetectors;
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo, string categoryName, IProgress<double> progress, CancellationToken token)
        {
            var baseBath = cardInfo.GetRootPath();
            return GetSoftware(baseBath, categoryName, progress, token);
        }

        public SoftwareInfo GetSoftware(string basePath, string categoryName, IProgress<double> progress, CancellationToken token)
        {
            return SoftwareDetectors
                .Select(d => d.GetSoftware(basePath, categoryName, progress, token))
                .FirstOrDefault(s => s != null);
        }

        public bool UpdateSoftware(SoftwareInfo software, byte[] buffer)
        {
            return SoftwareDetectors
                .Any(d => d.UpdateSoftware(software, buffer));
        }
    }
}
