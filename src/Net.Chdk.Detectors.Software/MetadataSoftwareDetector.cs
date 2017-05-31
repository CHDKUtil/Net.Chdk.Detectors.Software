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
            Logger.LogTrace("Detecting {0} software from {1} metadata", categoryName, cardInfo.DriveLetter);

            return GetValue(cardInfo, categoryName, progress, token);
        }

        protected override string FileName => Files.Metadata.Software;
    }
}
