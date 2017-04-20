using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Software;

namespace Net.Chdk.Detectors.Software
{
    sealed class MetadataSoftwareDetector : MetadataDetector<MetadataSoftwareDetector, SoftwareInfo>, ISoftwareDetector
    {
        public MetadataSoftwareDetector(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public SoftwareInfo GetSoftware(string driveLetter)
        {
            return GetValue(driveLetter);
        }

        protected override string FileName => "SOFTWARE.JSN";
    }
}
