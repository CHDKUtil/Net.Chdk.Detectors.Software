using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using Net.Chdk.Validators;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    sealed class MetadataSoftwareDetector : MetadataDetector<MetadataSoftwareDetector, SoftwareInfo>, IInnerSoftwareDetector
    {
        public MetadataSoftwareDetector(IValidator<SoftwareInfo> validator, ILoggerFactory loggerFactory)
            : base(validator, loggerFactory)
        {
        }

        public IEnumerable<SoftwareInfo> GetSoftware(CardInfo cardInfo, IProgress<double> progress, CancellationToken token)
        {
            Logger.LogTrace("Detecting software from {0} metadata", cardInfo.DriveLetter);

            var software = GetValue(cardInfo);
            if (software == null)
                return null;

            return new[] { software };
        }

        protected override string FileName => Files.Metadata.Software;
    }
}
