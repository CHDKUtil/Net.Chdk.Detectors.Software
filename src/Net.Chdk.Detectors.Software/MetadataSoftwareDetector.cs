using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;

namespace Net.Chdk.Detectors.Software
{
    sealed class MetadataSoftwareDetector : MetadataDetector<MetadataSoftwareDetector, SoftwareInfo>, IInnerSoftwareDetector
    {
        public MetadataSoftwareDetector(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo)
        {
            Logger.LogTrace("Detecting software from {0} metadata", cardInfo.DriveLetter);

            return GetValue(cardInfo);
        }

        protected override string FileName => "SOFTWARE.JSN";
    }
}
