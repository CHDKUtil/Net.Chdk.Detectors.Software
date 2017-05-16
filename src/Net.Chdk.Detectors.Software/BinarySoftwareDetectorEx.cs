using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.Binary;
using Net.Chdk.Model.Software;
using Net.Chdk.Providers.Boot;
using Net.Chdk.Providers.Software;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    sealed class BinarySoftwareDetectorEx : BinarySoftwareDetectorBase
    {
        private const int OffsetLength = 8;

        public BinarySoftwareDetectorEx(IEnumerable<IInnerBinarySoftwareDetector> softwareDetectors, IBinaryDecoder binaryDecoder, IBootProvider bootProvider, ICameraProvider cameraProvider, ISoftwareHashProvider hashProvider, ILoggerFactory loggerFactory)
            : base(softwareDetectors, binaryDecoder, bootProvider, cameraProvider, hashProvider, loggerFactory.CreateLogger<BinarySoftwareDetectorEx>())
        {
        }

        protected override SoftwareInfo DoGetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, IProgress<double> progress, CancellationToken token)
        {
            return DoGetSoftware(detectors, encBuffer, token)
                ?? base.DoGetSoftware(detectors, encBuffer, progress, token);
        }

        protected override uint?[] GetOffsets()
        {
            Logger.LogDebug("Building offsets");

            var watch = new Stopwatch();
            watch.Start();

            var offsetCount = GetOffsetCount(0);
            var offsets = new uint?[offsetCount];
            var index = 0;
            GetAllOffsets(Offsets.Empty, offsets, ref index, 0);

            watch.Stop();
            Logger.LogDebug("Building offsets completed in {0}", watch.Elapsed);

            return offsets;
        }

        private static void GetAllOffsets(Offsets prefix, uint?[] offsets, ref int index, int pos)
        {
            if (pos == OffsetLength)
                offsets[index++] = GetOffsets(prefix);
            else
                GetOffsets(prefix, offsets, ref index, pos);
        }

        private static void GetOffsets(Offsets prefix, uint?[] offsets, ref int index, int pos)
        {
            for (var i = 0; i < OffsetLength; i++)
            {
                if (!prefix.Contains(i))
                {
                    var prefix2 = new Offsets(prefix, i);
                    GetAllOffsets(prefix2, offsets, ref index, pos + 1);
                }
            }
        }

        private static int GetOffsetCount(int pos)
        {
            if (pos == OffsetLength)
                return 1;
            return (pos + 1) * GetOffsetCount(pos + 1);
        }
    }
}
