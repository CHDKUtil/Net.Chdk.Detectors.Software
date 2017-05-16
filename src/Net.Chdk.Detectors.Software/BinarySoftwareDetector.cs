using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    sealed class BinarySoftwareDetector : IInnerSoftwareDetector
    {
        private IEnumerable<IBinarySoftwareDetector> SoftwareDetectors { get; }

        public BinarySoftwareDetector(IEnumerable<IBinarySoftwareDetector> softwareDetectors)
        {
            SoftwareDetectors = softwareDetectors;
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo, IProgress<double> progress, CancellationToken token)
        {
            var baseBath = cardInfo.GetRootPath();
            return GetSoftware(baseBath, progress, token);
        }

        private SoftwareInfo GetSoftware(string basePath, IProgress<double> progress, CancellationToken token)
        {
            return SoftwareDetectors
                .Select(d => d.GetSoftware(basePath, progress, token))
                .FirstOrDefault(s => s != null);
        }
    }
}
