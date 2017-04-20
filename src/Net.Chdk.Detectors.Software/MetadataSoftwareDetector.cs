using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;

namespace Net.Chdk.Detectors.Software
{
    sealed class MetadataSoftwareDetector : MetadataDetector<MetadataSoftwareDetector, SoftwareInfo>, ISoftwareDetector
    {
        public MetadataSoftwareDetector(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo)
        {
            return GetValue(cardInfo.DriveLetter);
        }

        protected override string FileName => "SOFTWARE.JSN";
    }
}
