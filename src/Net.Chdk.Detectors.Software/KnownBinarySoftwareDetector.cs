using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.Binary;
using Net.Chdk.Providers.Boot;
using Net.Chdk.Providers.Software;
using System.Collections.Generic;

namespace Net.Chdk.Detectors.Software
{
    sealed class KnownBinarySoftwareDetector : BinarySoftwareDetectorBase
    {
        public KnownBinarySoftwareDetector(IEnumerable<IInnerBinarySoftwareDetector> softwareDetectors, IBinaryDecoder binaryDecoder, IBootProvider bootProvider, ICameraProvider cameraProvider, ISoftwareHashProvider hashProvider, ILoggerFactory loggerFactory)
            : base(softwareDetectors, binaryDecoder, bootProvider, cameraProvider, hashProvider, loggerFactory.CreateLogger<KnownBinarySoftwareDetector>())
        {
        }

        protected override uint?[] GetOffsets()
        {
            var offsets = new uint?[BinaryDecoder.MaxVersion + 1];
            for (var v = 0; v < BinaryDecoder.MaxVersion; v++)
                offsets[v + 1] = GetOffsets(v + 1);
            return offsets;
        }

        private uint? GetOffsets(int version)
        {
            var offsets = BootProvider.Offsets[version - 1];
            return GetOffsets(offsets);
        }

        private static uint? GetOffsets(int[] offsets)
        {
            var uOffsets = 0u;
            for (int index = 0; index < offsets.Length; index++)
                uOffsets += (uint)offsets[index] << (index << 2);
            return uOffsets;
        }
    }
}
