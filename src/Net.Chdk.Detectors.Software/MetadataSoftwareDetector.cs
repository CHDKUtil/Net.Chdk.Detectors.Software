using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using Net.Chdk.Validators;
using System;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    sealed class MetadataSoftwareDetector : MetadataDetector<MetadataSoftwareDetector, SoftwareInfo>, IInnerSoftwareDetector
    {
        public MetadataSoftwareDetector(IValidator<SoftwareInfo> validator, ILoggerFactory loggerFactory)
            : base(validator, loggerFactory)
        {
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo, string categoryName, IProgress<double> progress, CancellationToken token)
        {
            Logger.LogTrace("Detecting software from {0} metadata", cardInfo.DriveLetter);

            return GetValue(cardInfo, categoryName, progress);
        }

        protected override string FileName => Files.Metadata.Software;
    }
}
