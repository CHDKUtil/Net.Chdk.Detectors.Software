using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.Binary;
using Net.Chdk.Providers.Boot;
using Net.Chdk.Providers.Software;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Net.Chdk.Detectors.Software
{
    sealed class BinarySoftwareDetectorEx : BinarySoftwareDetectorBase
    {
        public BinarySoftwareDetectorEx(IEnumerable<IInnerBinarySoftwareDetector> softwareDetectors, IBinaryDecoder binaryDecoder, IBootProvider bootProvider, ICameraProvider cameraProvider, ISoftwareHashProvider hashProvider, ILoggerFactory loggerFactory)
            : base(softwareDetectors, binaryDecoder, bootProvider, cameraProvider, hashProvider, loggerFactory.CreateLogger<BinarySoftwareDetectorEx>())
        {
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
            return GetAllOffsets(new int[0])
                .Select(GetOffsets)
                .Cast<uint?>();
        }

        private static IEnumerable<int[]> GetAllOffsets(int[] prefix)
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
                        var prefix2 = prefix.Concat(new[] { i }).ToArray();
                        var offsets2 = GetAllOffsets(prefix2);
                        foreach (var offsets in offsets2)
                            yield return offsets;
                    }
                }
            }
        }
    }
}
