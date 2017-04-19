using Net.Chdk.Model.Software;

namespace Net.Chdk.Detectors.Software
{
    public interface ISoftwareDetector
    {
        SoftwareInfo GetSoftware(string driveLetter);
    }
}
