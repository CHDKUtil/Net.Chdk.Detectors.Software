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

        public SoftwareInfo GetSoftware(CardInfo cardInfo, IProgress<double> progress, CancellationToken token)
        {
            var baseBath = cardInfo.GetRootPath();
            return GetSoftware(baseBath, progress, token);
        }

        public SoftwareInfo GetSoftware(string basePath, IProgress<double> progress, CancellationToken token)
        {
            return SoftwareDetectors
                .Select(d => d.GetSoftware(basePath, progress, token))
                .FirstOrDefault(s => s != null);
        }

        public SoftwareInfo UpdateSoftware(SoftwareInfo software, byte[] buffer)
        {
            foreach (var detector in SoftwareDetectors)
            {
                if (detector.UpdateSoftware(ref software, buffer))
                    return software;
            }
            return software;
        }
    }
}
