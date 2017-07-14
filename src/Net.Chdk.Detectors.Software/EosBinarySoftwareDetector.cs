using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.Binary;
using Net.Chdk.Providers.Boot;
using Net.Chdk.Providers.Camera;
using Net.Chdk.Providers.Software;
using System.Collections.Generic;

namespace Net.Chdk.Detectors.Software
{
    sealed class EosBinarySoftwareDetector : BinarySoftwareDetectorBase
    {
        public EosBinarySoftwareDetector(IEnumerable<IProductBinarySoftwareDetector> softwareDetectors, IBinaryDecoder binaryDecoder, IBootProvider bootProvider, ICameraProvider cameraProvider, ISoftwareHashProvider hashProvider, ILoggerFactory loggerFactory)
            : base(softwareDetectors, binaryDecoder, bootProvider, cameraProvider, hashProvider, loggerFactory.CreateLogger<EosBinarySoftwareDetector>())
        {
        }

        protected override uint?[] GetOffsets()
        {
            return new uint?[0];
        }

        protected override string CategoryName => "EOS";
    }
}
