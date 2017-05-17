using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.Binary;
using Net.Chdk.Model.Software;
using Net.Chdk.Providers.Boot;
using Net.Chdk.Providers.Software;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    sealed class UnknownBinarySoftwareDetector : BinarySoftwareDetectorBase
    {
        public UnknownBinarySoftwareDetector(IEnumerable<IProductBinarySoftwareDetector> softwareDetectors, IBinaryDecoder binaryDecoder, IBootProvider bootProvider, ICameraProvider cameraProvider, ISoftwareHashProvider hashProvider, ILoggerFactory loggerFactory)
            : base(softwareDetectors, binaryDecoder, bootProvider, cameraProvider, hashProvider, loggerFactory.CreateLogger<UnknownBinarySoftwareDetector>())
        {
        }

        protected override SoftwareInfo DoGetSoftware(IEnumerable<IProductBinarySoftwareDetector> detectors, byte[] encBuffer, IProgress<double> progress, CancellationToken token)
        {
            return DoGetSoftware(detectors, encBuffer, token)
                ?? base.DoGetSoftware(detectors, encBuffer, progress, token);
        }

        protected override uint?[] GetOffsets()
        {
            var offsetCount = Offsets.GetOffsetCount();
            var offsets = new uint?[offsetCount];
            var index = 0;
            Offsets.Empty.GetAllOffsets(offsets, ref index);
            return offsets;
        }
    }
}
