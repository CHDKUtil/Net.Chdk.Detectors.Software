using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Software;
using System.IO;

namespace Net.Chdk.Detectors.Software
{
    sealed class MetadataSoftwareDetector : ISoftwareDetector
    {
        public ILogger Logger { get; }

        public MetadataSoftwareDetector(ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<MetadataSoftwareDetector>();
        }

        public SoftwareInfo GetSoftware(string driveLetter)
        {
            var metadataPath = Path.Combine(driveLetter, "METADATA");
            var softwarePath = Path.Combine(metadataPath, "SOFTWARE.JSN");
            if (!File.Exists(softwarePath))
                return null;

            Logger.LogInformation("Reading {0}", softwarePath);

            using (var stream = File.OpenRead(softwarePath))
            {
                return JsonObject.Deserialize<SoftwareInfo>(stream);
            }
        }
    }
}
