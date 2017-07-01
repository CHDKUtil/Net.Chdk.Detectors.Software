using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.Binary;
using Net.Chdk.Providers.Boot;
using Net.Chdk.Providers.Software;
using System.Collections.Generic;

namespace Net.Chdk.Detectors.Software
{
    sealed class PsHashSoftwareDetector : HashSoftwareDetector
    {
        public PsHashSoftwareDetector(IEnumerable<IProductBinarySoftwareDetector> softwareDetectors, IBinaryDecoder binaryDecoder, IBootProviderResolver bootProviderResolver, IEncodingProvider encodingProvider, ISoftwareHashProvider hashProvider, ILoggerFactory loggerFactory)
            : base(softwareDetectors, binaryDecoder, bootProviderResolver, encodingProvider, hashProvider, loggerFactory.CreateLogger<PsHashSoftwareDetector>())
        {
        }

        protected override string CategoryName => "PS";
    }
}
