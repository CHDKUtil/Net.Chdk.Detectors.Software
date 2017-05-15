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

        public SoftwareInfo GetSoftware(CardInfo cardInfo, IProgress<double> progress)
        {
            return GetSoftware(cardInfo.GetRootPath(), progress);
        }

        public SoftwareInfo GetSoftware(string basePath, IProgress<double> progress)
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
            var software = GetSoftware(SoftwareDetectors, encBuffer, progress);
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

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, SoftwareEncodingInfo encoding, IProgress<double> progress = null)
        {
            if (encoding == null)
                return GetSoftware(detectors, encBuffer, progress);
            if (encoding.Data == null)
                return PlainGetSoftware(detectors, encBuffer);
            var decBuffer = new byte[encBuffer.Length];
            var tmpBuffer1 = new byte[ChunkSize];
            var tmpBuffer2 = new byte[ChunkSize];
            using (var encStream = new MemoryStream(encBuffer))
            using (var decStream = new MemoryStream(decBuffer))
            {
                return GetSoftware(detectors, encBuffer, decBuffer, encStream, decStream, tmpBuffer1, tmpBuffer2, encoding.Data);
            }
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, IProgress<double> progress)
        {
            var offsets = GetOffsets();
            var software = GetSoftware(detectors, encBuffer, offsets, ProgressState.Empty);
            if (software != null)
                return software;

            var allOffsets = GetAllOffsetsExcept(offsets);
            var progressState = new ProgressState(allOffsets.Length, progress);
            return GetSoftware(detectors, encBuffer, allOffsets, progressState);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, uint?[] offsets, ProgressState progress)
        {
            var maxThreads = Properties.Settings.Default.MaxThreads;
            var processorCount = Environment.ProcessorCount;
            var count = maxThreads > 0 && maxThreads < processorCount
                ? maxThreads
                : processorCount;

            var encBuffers = new byte[count][];
            var decBuffers = new byte[count][];
            var tmpBuffers1 = new byte[count][];
            var tmpBuffers2 = new byte[count][];
            for (var i = 0; i < count; i++)
            {
                encBuffers[i] = new byte[encBuffer.Length];
                decBuffers[i] = new byte[encBuffer.Length];
                tmpBuffers1[i] = new byte[ChunkSize];
                tmpBuffers2[i] = new byte[ChunkSize];
            }

            var encStreams = new Stream[count];
            var decStreams = new Stream[count];
            for (var i = 0; i < count; i++)
            {
                encStreams[i] = new MemoryStream(encBuffers[i]);
                decStreams[i] = new MemoryStream(decBuffers[i]);
            }

            var versions = new int[count + 1];
            for (var i = 0; i <= count; i++)
                versions[i] = i * offsets.Length / count;

            var software = GetSoftware(detectors, offsets, count, encBuffers, decBuffers, tmpBuffers1, tmpBuffers2, encStreams, decStreams, versions, progress);

            for (var i = 0; i < count; i++)
            {
                encStreams[i].Dispose();
                decStreams[i].Dispose();
            }

            progress.Reset();

            return software;
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, uint?[] offsets, int count, byte[][] encBuffers, byte[][] decBuffers, byte[][] tmpBuffers1, byte[][] tmpBuffers2, Stream[] encStreams, Stream[] decStreams, int[] versions, ProgressState progress)
        {
            if (count == 1)
            {
                Logger.LogTrace("Detecting software in a single thread from {0} offsets", offsets.Length);
                return GetSoftware(detectors, encBuffers[0], decBuffers[0], encStreams[0], decStreams[0], tmpBuffers1[0], tmpBuffers2[0], versions[0], versions[1], offsets, progress);
            }

            Logger.LogTrace("Detecting software in {0} threads from {1} offsets", count, offsets.Length);
            return Enumerable.Range(0, count)
                .AsParallel()
                .Select(i => GetSoftware(detectors, encBuffers[i], decBuffers[i], encStreams[i], decStreams[i], tmpBuffers1[i], tmpBuffers2[i], versions[i], versions[i + 1], offsets, progress))
                .FirstOrDefault(s => s != null);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, byte[] decBuffer, Stream encStream, Stream decStream, byte[] tmpBuffer1, byte[] tmpBuffer2, int startIndex, int endIndex, uint?[] offsets, ProgressState progress)
        {
            for (var index = startIndex; index < endIndex; index++)
            {
                var software = GetSoftware(detectors, encBuffer, decBuffer, encStream, decStream, tmpBuffer1, tmpBuffer2, offsets[index]);
                if (software != null)
                    return software;
                progress.Update();
            }
            return null;
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, byte[] decBuffer, Stream encStream, Stream decStream, byte[] tmpBuffer1, byte[] tmpBuffer2, uint? offsets)
        {
            var buffer = Decode(encBuffer, decBuffer, encStream, decStream, tmpBuffer1, tmpBuffer2, offsets);
            if (buffer == null)
                return null;
            var software = DoGetSoftware(detectors, buffer);
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

        private static SoftwareEncodingInfo GetEncoding(uint? offsets)
        {
            return new SoftwareEncodingInfo
            {
                Name = offsets != null ? EncodingName : string.Empty,
                Data = offsets
            };
        }

        private byte[] Decode(byte[] encBuffer, byte[] decBuffer, Stream encStream, Stream decStream, byte[] tmpBuffer1, byte[] tmpBuffer2, uint? offsets)
        {
            if (offsets == null)
                return encBuffer;
            encStream.Seek(0, SeekOrigin.Begin);
            decStream.Seek(0, SeekOrigin.Begin);
            if (BinaryDecoder.Decode(encStream, decStream, tmpBuffer1, tmpBuffer2, offsets))
                return decBuffer;
            return null;
        }

        private uint?[] GetAllOffsets()
        {
            Logger.LogTrace("Building offsets");
            var result = GetAllOffsets(new int[0])
                .Select(GetOffsets)
                .Cast<uint?>()
                .ToArray();
            Logger.LogTrace("Building completed");
            return result;
        }

        private uint?[] GetAllOffsetsExcept(uint?[] offsets)
        {
            Logger.LogTrace("Building offsets");
            var result = GetAllOffsets(new int[0])
                .Select(GetOffsets)
                .Cast<uint?>()
                .Except(offsets)
                .ToArray();
            Logger.LogTrace("Building completed");
            return result;
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

        private uint?[] GetOffsets()
        {
            var offsets = new uint?[BinaryDecoder.MaxVersion + 1];
            for (var v = 0; v < BinaryDecoder.MaxVersion; v++)
                offsets[v + 1] = GetOffsets(v + 1);
            return offsets;
        }

        private uint GetOffsets(int version)
        {
            var offsets = BootProvider.Offsets[version - 1];
            return GetOffsets(offsets);
        }

        private static uint GetOffsets(int[] offsets)
        {
            var uOffsets = 0u;
            for (var index = 0; index < offsets.Length; index++)
                uOffsets += (uint)offsets[index] << (index << 2);
            return uOffsets;
        }
    }
}
