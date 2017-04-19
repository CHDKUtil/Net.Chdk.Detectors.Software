using Net.Chdk.Model.Software;
using System.IO;

namespace Net.Chdk.Detectors.Software
{
    sealed class MetadataSoftwareDetector : ISoftwareDetector
    {
        public SoftwareInfo GetSoftware(string driveLetter)
        {
            var metadataPath = Path.Combine(driveLetter, "METADATA");
            var softwarePath = Path.Combine(metadataPath, "SOFTWARE.JSN");
            if (!File.Exists(softwarePath))
                return null;

            using (var stream = File.OpenRead(softwarePath))
            {
                return JsonObject.Deserialize<SoftwareInfo>(stream);
            }
        }
    }
}
