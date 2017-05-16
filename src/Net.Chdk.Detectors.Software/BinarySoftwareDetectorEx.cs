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

            var offsets = GetAllOffsets();
            var result = new uint?[] { null }
                .Concat(offsets)
                .ToArray();

            watch.Stop();
            Logger.LogDebug("Building offsets completed in {0}", watch.Elapsed);

            return result;
        }

        private static IEnumerable<uint?> GetAllOffsets()
        {
            return GetAllOffsets(Offsets.Empty)
                .Select(GetOffsets)
                .Cast<uint?>();
        }

        private static IEnumerable<Offsets> GetAllOffsets(Offsets prefix)
        {
            if (prefix.Count() == 8)
            {
                yield return prefix;
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    if (!prefix.Contains(i))
                    {
                        var prefix2 = new Offsets(prefix, i);
                        var offsets2 = GetAllOffsets(prefix2);
                        foreach (var offsets in offsets2)
                            yield return offsets;
                    }
                }
            }
        }
    }
}
