using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.DancingBits;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using Net.Chdk.Providers.Boot;
using Net.Chdk.Providers.Crypto;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Net.Chdk.Detectors.Software
{
    sealed class BinarySoftwareDetector : IInnerSoftwareDetector
    {
        private const string HashName = "sha256";

        private ILogger Logger { get; }
        private IEnumerable<IInnerBinarySoftwareDetector> SoftwareDetectors { get; }
        private IBootProvider BootProvider { get; }
        private IHashProvider HashProvider { get; }

        public BinarySoftwareDetector(IEnumerable<IInnerBinarySoftwareDetector> softwareDetectors, IBootProvider bootProvider, IHashProvider hashProvider, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<BinarySoftwareDetector>();
            SoftwareDetectors = softwareDetectors;
            BootProvider = bootProvider;
            HashProvider = hashProvider;
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo)
        {
            var rootPath = cardInfo.GetRootPath();
            var fileName = BootProvider.FileName;
            var diskbootPath = Path.Combine(rootPath, fileName);

            Logger.LogTrace("Detecting software from {0}", diskbootPath);

            if (!File.Exists(diskbootPath))
            {
                Logger.LogTrace("{0} not found", diskbootPath);
                return null;
            }

            var encBuffer = File.ReadAllBytes(diskbootPath);
            var decBuffer = new byte[encBuffer.Length];
            using (var encStream = new MemoryStream(encBuffer))
            {
                var hash = GetHash(encStream, fileName, HashName);
                for (var version = 0; version <= DancingBitsEncoder.MaxVersion; version++)
                {
                    encStream.Seek(0, SeekOrigin.Begin);
                    using (var decStream = new MemoryStream(decBuffer))
                    {
                        DancingBitsEncoder.Decode(encStream, decStream, version);
                        var software = GetSoftware(decBuffer);
                        if (software != null)
                        {
                            software.Hash = hash;
                            return software;
                        }
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
            return softwareDetector.Bytes
                .SelectMany(b => SeekAfterMany(buffer, b))
                .Select(i => softwareDetector.GetSoftware(buffer, i))
                .FirstOrDefault(s => s != null);
        }

        private SoftwareHashInfo GetHash(Stream stream, string fileName, string hashName)
        {
            return new SoftwareHashInfo
            {
                Name = HashName,
                Values = GetHashValues(stream, fileName, hashName)
            };
        }

        private Dictionary<string, string> GetHashValues(Stream stream, string fileName, string hashName)
        {
            var key = fileName.ToLowerInvariant();
            var value = HashProvider.GetHashString(stream, HashName);
            return new Dictionary<string, string>
            {
                { key, value }
            };
        }

        private static IEnumerable<int> SeekAfterMany(byte[] buffer, byte[] bytes)
        {
            for (var i = 0; i < buffer.Length - bytes.Length; i++)
                if (Enumerable.Range(0, bytes.Length).All(j => buffer[i + j] == bytes[j]))
                    yield return i + bytes.Length;
        }
    }
}
