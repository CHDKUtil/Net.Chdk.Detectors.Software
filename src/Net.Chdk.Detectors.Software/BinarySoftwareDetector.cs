using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.Binary;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using Net.Chdk.Providers.Boot;
using Net.Chdk.Providers.Software;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Net.Chdk.Detectors.Software
{
    sealed class BinarySoftwareDetector : IInnerSoftwareDetector, IBinarySoftwareDetector
    {
        private const string HashName = "sha256";

        private ILogger Logger { get; }
        private IEnumerable<IInnerBinarySoftwareDetector> SoftwareDetectors { get; }
        private IBinaryDecoder BinaryDecoder { get; }
        private IBootProvider BootProvider { get; }
        private ICameraProvider CameraProvider { get; }
        private ISoftwareHashProvider HashProvider { get; }

        public BinarySoftwareDetector(IEnumerable<IInnerBinarySoftwareDetector> softwareDetectors, IBinaryDecoder binaryDecoder, IBootProvider bootProvider, ICameraProvider cameraProvider, ISoftwareHashProvider hashProvider, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<BinarySoftwareDetector>();
            SoftwareDetectors = softwareDetectors;
            BinaryDecoder = binaryDecoder;
            BootProvider = bootProvider;
            CameraProvider = cameraProvider;
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
            var software = GetSoftware(SoftwareDetectors, encBuffer);
            if (software != null)
                software.Hash = HashProvider.GetHash(encBuffer, fileName, HashName);
            return software;
        }

        public SoftwareInfo UpdateSoftware(SoftwareInfo software, byte[] encBuffer)
        {
            var product = software.Product;
            var camera = software.Camera;
            var detectors = SoftwareDetectors;
            if (product?.Name != null)
                detectors = detectors.Where(d => d.ProductName.Equals(product.Name, StringComparison.InvariantCulture));

            var software2 = GetSoftware(product, camera, encBuffer, detectors);
            if (software2?.Product.Created != null)
                software.Product.Created = software2.Product.Created;

            software.Hash = HashProvider.GetHash(encBuffer, BootProvider.FileName, HashName);
            return software;
        }

        private SoftwareInfo GetSoftware(SoftwareProductInfo product, SoftwareCameraInfo camera, byte[] encBuffer, IEnumerable<IInnerBinarySoftwareDetector> detectors)
        {
            var cameraModel = CameraProvider.GetCamera(product, camera);
            var version = cameraModel?.EncodingVersion;
            if (version.HasValue)
            {
                var decBuffer = new byte[encBuffer.Length];
                return GetSoftware(detectors, encBuffer, decBuffer, version.Value);
            }
            return GetSoftware(detectors, encBuffer);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer)
        {
            var decBuffer = new byte[encBuffer.Length];
            for (var version = 0; version <= BinaryDecoder.MaxVersion; version++)
            {
                var software = GetSoftware(detectors, encBuffer, decBuffer, version);
                if (software != null)
                    return software;
            }
            return null;
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, byte[] decBuffer, int version)
        {
            return BinaryDecoder.Decode(encBuffer, decBuffer, version)
                ? detectors
                    .SelectMany(GetBytes)
                    .AsParallel()
                    .Select(t => GetSoftware(decBuffer, t.Item1, t.Item2))
                    .FirstOrDefault(s => s != null)
                : null;
        }

        private static IEnumerable<Tuple<IInnerBinarySoftwareDetector, byte[]>> GetBytes(IInnerBinarySoftwareDetector d)
        {
            return d.Bytes.Select(b => Tuple.Create(d, b));
        }

        private static SoftwareInfo GetSoftware(byte[] buffer, IInnerBinarySoftwareDetector softwareDetector, byte[] bytes)
        {
            return SeekAfterMany(buffer, bytes)
                .Select(i => softwareDetector.GetSoftware(buffer, i))
                .FirstOrDefault(s => s != null);
        }

        private static IEnumerable<int> SeekAfterMany(byte[] buffer, byte[] bytes)
        {
            for (var i = 0; i < buffer.Length - bytes.Length; i++)
                if (Enumerable.Range(0, bytes.Length).All(j => buffer[i + j] == bytes[j]))
                    yield return i + bytes.Length;
        }
    }
}
