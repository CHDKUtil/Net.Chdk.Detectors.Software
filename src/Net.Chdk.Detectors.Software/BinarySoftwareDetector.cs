using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.DancingBits;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using Net.Chdk.Providers.Boot;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Net.Chdk.Detectors.Software
{
    sealed class BinarySoftwareDetector : IInnerSoftwareDetector
    {
        private ILogger Logger { get; }
        private IEnumerable<IInnerBinarySoftwareDetector> SoftwareDetectors { get; }
        private IBootProvider BootProvider { get; }

        public BinarySoftwareDetector(IEnumerable<IInnerBinarySoftwareDetector> softwareDetectors, IBootProvider bootProvider, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<BinarySoftwareDetector>();
            SoftwareDetectors = softwareDetectors;
            BootProvider = bootProvider;
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo)
        {
            var rootPath = cardInfo.GetRootPath();
            var diskbootPath = Path.Combine(rootPath, BootProvider.FileName);

            Logger.LogTrace("Detecting software from {0}", diskbootPath);

            if (!File.Exists(diskbootPath))
            {
                Logger.LogTrace("{0} not found", diskbootPath);
                return null;
            }

            var inBuffer = File.ReadAllBytes(diskbootPath);
            var outBuffer = new byte[inBuffer.Length];
            using (var inStream = new MemoryStream(inBuffer))
            {
                for (var version = 0; version <= DancingBitsEncoder.MaxVersion; version++)
                {
                    inStream.Seek(0, SeekOrigin.Begin);
                    using (var outStream = new MemoryStream(outBuffer))
                    {
                        DancingBitsEncoder.Decode(inStream, outStream, version);
                        var software = GetSoftware(outBuffer);
                        if (software != null)
                            return software;
                    }
                }
            }
            return null;
        }

        private SoftwareInfo GetSoftware(byte[] buffer)
        {
            return SoftwareDetectors
                .Select(d => GetSoftware(d, buffer))
                .FirstOrDefault(s => s != null);
        }

        private SoftwareInfo GetSoftware(IInnerBinarySoftwareDetector softwareDetector, byte[] buffer)
        {
            var bytes = softwareDetector.Bytes;
            foreach (var bytesItem in bytes)
            {
                var index = SeekAfter(buffer, bytesItem);
                if (index >= 0)
                {
                    var software = softwareDetector.GetSoftware(buffer, index);
                    if (software != null)
                        return software;
                }
            }
            return null;
        }

        private static int SeekAfter(byte[] buffer, byte[] bytes)
        {
            for (var i = 0; i < buffer.Length - bytes.Length; i++)
                if (Enumerable.Range(0, bytes.Length).All(j => buffer[i + j] == bytes[j]))
                    return i + bytes.Length;
            return -1;
        }
    }
}
