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
        private const string EncodingName = "dancingbits";
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
            return GetSoftware(cardInfo.GetRootPath());
        }

        public SoftwareInfo GetSoftware(string basePath)
        {
            var fileName = BootProvider.FileName;
            var diskbootPath = Path.Combine(basePath, fileName);

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
            var detectors = GetDetectors(software.Product);
            var encoding = GetEncoding(software.Product, software.Camera, software.Encoding);

            var software2 = GetSoftware(detectors, encBuffer, encoding);
            if (software2 != null)
            {
                if (software2.Product.Created != null)
                    software.Product.Created = software2.Product.Created;
                if (software.Encoding == null)
                    software.Encoding = software2.Encoding;
            }

            software.Hash = HashProvider.GetHash(encBuffer, BootProvider.FileName, HashName);
            return software;
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, SoftwareEncodingInfo encoding)
        {
            if (encoding == null)
                return GetSoftware(detectors, encBuffer);
            if (encoding.Data == null)
                return PlainGetSoftware(detectors, encBuffer);
            var decBuffer = new byte[encBuffer.Length];
            var ulBuffer = new ulong[0x100];
            return GetSoftware(detectors, encBuffer, decBuffer, ulBuffer, encoding.Data);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer)
        {
            var maxThreads = Properties.Settings.Default.MaxThreads;
            var processorCount = Environment.ProcessorCount;
            var count = maxThreads > 0 && maxThreads < processorCount
                ? maxThreads
                : processorCount;

            var decBuffers = new byte[count][];
            for (var i = 0; i < count; i++)
                decBuffers[i] = new byte[encBuffer.Length];

            var ulBuffers = new ulong[count][];
            for (var i = 0; i < count; i++)
                ulBuffers[i] = new ulong[0x100];

            var versions = new int[count + 1];
            for (var i = 0; i <= count; i++)
                versions[i] = i * (BinaryDecoder.MaxVersion + 1) / count;

            var offsets = new ulong?[BinaryDecoder.MaxVersion + 1];
            for (var v = 0; v < BinaryDecoder.MaxVersion; v++)
                offsets[v + 1] = GetOffsets(v + 1);

            if (count == 1)
            {
                Logger.LogTrace("Detecting software in a single thread");
                return GetSoftware(detectors, encBuffer, decBuffers[0], ulBuffers[0], versions[0], versions[1], offsets);
            }

            Logger.LogTrace("Detecting software in {0} threads", count);
            return Enumerable.Range(0, count)
                .AsParallel()
                .Select(i => GetSoftware(detectors, encBuffer, decBuffers[i], ulBuffers[i], versions[i], versions[i + 1], offsets))
                .FirstOrDefault(s => s != null);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, byte[] decBuffer, ulong[] ulBuffer, int startVersion, int endVersion, ulong?[] offsets)
        {
            return Enumerable.Range(startVersion, endVersion - startVersion)
                .Select(v => GetSoftware(detectors, encBuffer, decBuffer, ulBuffer, offsets[v]))
                .FirstOrDefault(s => s != null);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, byte[] decBuffer, ulong[] ulBuffer, ulong? offsets)
        {
            if (!BinaryDecoder.Decode(encBuffer, decBuffer, ulBuffer, offsets))
                return null;
            var software = DoGetSoftware(detectors, decBuffer);
            if (software != null)
                software.Encoding = GetEncodingInfo(offsets);
            return software;
        }

        private SoftwareInfo PlainGetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] buffer)
        {
            Logger.LogTrace("Detecting software from plaintext");
            var software = DoGetSoftware(detectors, buffer);
            if (software != null)
                software.Encoding = GetEncodingInfo(null);
            return software;
        }

        private static SoftwareInfo DoGetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] buffer)
        {
            return detectors
                .SelectMany(GetBytes)
                .Select(t => GetSoftware(buffer, t.Item1, t.Item2))
                .FirstOrDefault(s => s != null);
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
                if (Equals(buffer, bytes, i))
                    yield return i + bytes.Length;
        }

        private static bool Equals(byte[] buffer, byte[] bytes, int start)
        {
            for (var j = 0; j < bytes.Length; j++)
                if (buffer[start + j] != bytes[j])
                    return false;
            return true;
        }

        private SoftwareEncodingInfo GetEncoding(SoftwareProductInfo product, SoftwareCameraInfo camera, SoftwareEncodingInfo encoding)
        {
            if (encoding == null)
            {
                var cameraModel = CameraProvider.GetCamera(product, camera);
                return cameraModel?.Encoding;
            }
            return encoding;
        }

        private IEnumerable<IInnerBinarySoftwareDetector> GetDetectors(SoftwareProductInfo product)
        {
            return product?.Name == null
                ? SoftwareDetectors
                : SoftwareDetectors.Where(d => d.ProductName.Equals(product.Name, StringComparison.InvariantCulture));
        }

        private static SoftwareEncodingInfo GetEncodingInfo(ulong? offsets)
        {
            return new SoftwareEncodingInfo
            {
                Name = offsets != null ? EncodingName : string.Empty,
                Data = offsets
            };
        }

        private ulong GetOffsets(int version)
        {
            var offsets = BootProvider.Offsets[version - 1];
            var uOffsets = 0ul;
            for (var index = 0; index < offsets.Length; index++)
                uOffsets += (ulong)offsets[index] << (index << 3);
            return uOffsets;
        }
    }
}
