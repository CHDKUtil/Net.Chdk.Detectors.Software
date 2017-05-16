using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.Binary;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using Net.Chdk.Providers.Boot;
using Net.Chdk.Providers.Software;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var worker = new BinaryDetectorWorker(detectors, BinaryDecoder, encBuffer, encoding.Data);
            return worker.GetSoftware(ProgressState.Empty);
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

            var watch = new Stopwatch();
            watch.Start();

            var workers = new BinaryDetectorWorker[count];
            for (var i = 0; i < count; i++)
            {
                workers[i] = new BinaryDetectorWorker(detectors, BinaryDecoder, encBuffer,
                    i * offsets.Length / count, (i + 1) * offsets.Length / count, offsets);
            }

            var software = GetSoftware(workers, offsets, progress);

            for (var i = 0; i < count; i++)
            {
                workers[i].Dispose();
            }

            watch.Stop();
            Logger.LogDebug("Detecting software completed in {0}", watch.Elapsed);

            progress.Reset();

            return software;
        }

        private SoftwareInfo GetSoftware(BinaryDetectorWorker[] workers, uint?[] offsets, ProgressState progress)
        {
            var count = workers.Length;
            if (count == 1)
            {
                Logger.LogDebug("Detecting software in a single thread from {0} offsets", offsets.Length);
                return workers[0].GetSoftware(progress);
            }

            Logger.LogDebug("Detecting software in {0} threads from {1} offsets", count, offsets.Length);
            return Enumerable.Range(0, count)
                .AsParallel()
                .Select(i => workers[i].GetSoftware(progress))
                .FirstOrDefault(s => s != null);
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
