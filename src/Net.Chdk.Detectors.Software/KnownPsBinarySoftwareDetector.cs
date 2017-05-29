﻿using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.Binary;
using Net.Chdk.Providers.Boot;
using Net.Chdk.Providers.Software;
using System.Collections.Generic;

namespace Net.Chdk.Detectors.Software
{
    sealed class KnownPsBinarySoftwareDetector : PsBinarySoftwareDetector
    {
        public KnownPsBinarySoftwareDetector(IEnumerable<IProductBinarySoftwareDetector> softwareDetectors, IBinaryDecoder binaryDecoder, IBootProviderResolver bootProviderResolver, IEncodingProvider encodingProvider, ISoftwareHashProvider hashProvider, ILoggerFactory loggerFactory)
            : base(softwareDetectors, binaryDecoder, bootProviderResolver, encodingProvider, hashProvider, loggerFactory.CreateLogger<KnownPsBinarySoftwareDetector>())
        {
        }

        protected override uint?[] GetOffsets()
        {
            var offsets = new uint?[BootProvider.Offsets.Length + 1];
            for (var v = 0; v < BootProvider.Offsets.Length; v++)
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
