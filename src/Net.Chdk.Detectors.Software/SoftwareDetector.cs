using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Net.Chdk.Detectors.Software
{
    public sealed class SoftwareDetector : ISoftwareDetector
    {
        private ILogger Logger { get; }
        private IEnumerable<IInnerSoftwareDetector> SoftwareDetectors { get; }

        public SoftwareDetector(IEnumerable<IInnerSoftwareDetector> softwareDetectors, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<SoftwareDetector>();
            SoftwareDetectors = softwareDetectors;
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo, IProgress<double> progress)
        {
            Logger.LogTrace("Detecting software from {0}", cardInfo.DriveLetter);

            return SoftwareDetectors
                .Select(d => d.GetSoftware(cardInfo, progress))
                .FirstOrDefault(s => s != null);
        }
    }
}
