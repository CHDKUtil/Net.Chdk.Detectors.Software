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
        private const int ChunkSize = 0x400;

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
            var tmpBuffer1 = new byte[ChunkSize];
            var tmpBuffer2 = new byte[ChunkSize];
            return GetSoftware(detectors, encBuffer, decBuffer, tmpBuffer1, tmpBuffer2, encoding.Data);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer)
        {
            var offsets = GetOffsets();
            var software = GetSoftware(detectors, encBuffer, offsets);
            if (software != null)
                return software;

            var allOffsets = GetAllOffsets();
            return GetSoftware(detectors, encBuffer, allOffsets);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, ulong?[] offsets)
        {
            var maxThreads = Properties.Settings.Default.MaxThreads;
            var processorCount = Environment.ProcessorCount;
            var count = maxThreads > 0 && maxThreads < processorCount
                ? maxThreads
                : processorCount;

            var decBuffers = new byte[count][];
            var tmpBuffers1 = new byte[count][];
            var tmpBuffers2 = new byte[count][];
            for (var i = 0; i < count; i++)
            {
                decBuffers[i] = new byte[encBuffer.Length];
                tmpBuffers1[i] = new byte[ChunkSize];
                tmpBuffers2[i] = new byte[ChunkSize];
            }

            var versions = new int[count + 1];
            for (var i = 0; i <= count; i++)
                versions[i] = i * offsets.Length / count;

            if (count == 1)
            {
                Logger.LogTrace("Detecting software in a single thread from {0} offsets", offsets.Length);
                return GetSoftware(detectors, encBuffer, decBuffers[0], tmpBuffers1[0], tmpBuffers2[0], versions[0], versions[1], offsets);
            }

            Logger.LogTrace("Detecting software in {0} threads from {1} offsets", count, offsets.Length);
            return Enumerable.Range(0, count)
                .AsParallel()
                .Select(i => GetSoftware(detectors, encBuffer, decBuffers[i], tmpBuffers1[i], tmpBuffers2[i], versions[i], versions[i + 1], offsets))
                .FirstOrDefault(s => s != null);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, byte[] decBuffer, byte[] tmpBuffer1, byte[] tmpBuffer2, int startVersion, int endVersion, ulong?[] offsets)
        {
            return Enumerable.Range(startVersion, endVersion - startVersion)
                .Select(v => GetSoftware(detectors, encBuffer, decBuffer, tmpBuffer1, tmpBuffer2, offsets[v]))
                .FirstOrDefault(s => s != null);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, byte[] decBuffer, byte[] tmpBuffer1, byte[] tmpBuffer2, ulong? offsets)
        {
            decBuffer = Decode(encBuffer, decBuffer, tmpBuffer1, tmpBuffer2, offsets);
            if (decBuffer == null)
                return null;
            var software = DoGetSoftware(detectors, decBuffer);
            if (software != null)
                software.Encoding = GetEncoding(offsets);
            return software;
        }

        private SoftwareInfo PlainGetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] buffer)
        {
            Logger.LogTrace("Detecting software from plaintext");
            var software = DoGetSoftware(detectors, buffer);
            if (software != null)
                software.Encoding = GetEncoding(null);
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

        private static SoftwareEncodingInfo GetEncoding(ulong? offsets)
        {
            return new SoftwareEncodingInfo
            {
                Name = offsets != null ? EncodingName : string.Empty,
                Data = offsets
            };
        }

        private byte[] Decode(byte[] encBuffer, byte[] decBuffer, byte[] tmpBuffer1, byte[] tmpBuffer2, ulong? offsets)
        {
            if (offsets == null)
                return encBuffer;
            using (var encStream = new MemoryStream(encBuffer))
            using (var decStream = new MemoryStream(decBuffer))
            {
                if (BinaryDecoder.Decode(encStream, decStream, tmpBuffer1, tmpBuffer2, offsets))
                    return decBuffer;
                return null;
            }
        }

        private ulong?[] GetAllOffsets()
        {
            Logger.LogTrace("Building offsets");
            var result = GetAllOfsets(new int[0])
                .Select(GetOffsets)
                .Cast<ulong?>()
                .ToArray();
            Logger.LogTrace("Building completed");
            return result;
        }

        private static IEnumerable<int[]> GetAllOfsets(int[] prefix)
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
                        var offsets2 = GetAllOfsets(prefix2);
                        foreach (var offsets in offsets2)
                            yield return offsets;
                    }
                }
            }
        }

        private ulong?[] GetOffsets()
        {
            var offsets = new ulong?[BinaryDecoder.MaxVersion + 1];
            for (var v = 0; v < BinaryDecoder.MaxVersion; v++)
                offsets[v + 1] = GetOffsets(v + 1);
            return offsets;
        }

        private ulong GetOffsets(int version)
        {
            var offsets = BootProvider.Offsets[version - 1];
            return GetOffsets(offsets);
        }

        private static ulong GetOffsets(int[] offsets)
        {
            var uOffsets = 0ul;
            for (var index = 0; index < offsets.Length; index++)
                uOffsets += (ulong)offsets[index] << (index << 3);
            return uOffsets;
        }
    }
}
