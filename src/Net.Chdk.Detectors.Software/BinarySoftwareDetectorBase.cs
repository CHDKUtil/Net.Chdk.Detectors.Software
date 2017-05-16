﻿using Microsoft.Extensions.Logging;
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
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    abstract class BinarySoftwareDetectorBase : IInnerSoftwareDetector, IBinarySoftwareDetector
    {
        private const string HashName = "sha256";

        protected ILogger Logger { get; }
        protected IBinaryDecoder BinaryDecoder { get; }
        protected IBootProvider BootProvider { get; }

        private IEnumerable<IInnerBinarySoftwareDetector> SoftwareDetectors { get; }
        private ICameraProvider CameraProvider { get; }
        private ISoftwareHashProvider HashProvider { get; }

        protected BinarySoftwareDetectorBase(IEnumerable<IInnerBinarySoftwareDetector> softwareDetectors, IBinaryDecoder binaryDecoder, IBootProvider bootProvider, ICameraProvider cameraProvider, ISoftwareHashProvider hashProvider, ILogger logger)
        {
            Logger = logger;
            SoftwareDetectors = softwareDetectors;
            BinaryDecoder = binaryDecoder;
            BootProvider = bootProvider;
            CameraProvider = cameraProvider;
            HashProvider = hashProvider;
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo, IProgress<double> progress, CancellationToken token)
        {
            return GetSoftware(cardInfo.GetRootPath(), progress, token);
        }

        public SoftwareInfo GetSoftware(string basePath, IProgress<double> progress, CancellationToken token)
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
            var software = GetSoftware(SoftwareDetectors, encBuffer, progress, token);
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

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, SoftwareEncodingInfo encoding,
            IProgress<double> progress = null, CancellationToken token = default(CancellationToken))
        {
            if (encoding == null)
                return GetSoftware(detectors, encBuffer, progress, token);
            var worker = new BinaryDetectorWorker(detectors, BinaryDecoder, encBuffer, encoding);
            return worker.GetSoftware(ProgressState.Empty, token);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, IProgress<double> progress, CancellationToken token)
        {
            var offsets = GetOffsets();
            var progressState = new ProgressState(offsets.Length, progress);
            return GetSoftware(detectors, encBuffer, offsets, progressState, token);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, uint?[] offsets, ProgressState progress, CancellationToken token)
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

            var software = GetSoftware(workers, offsets, progress, token);

            for (var i = 0; i < count; i++)
            {
                workers[i].Dispose();
            }

            watch.Stop();
            Logger.LogDebug("Detecting software completed in {0}", watch.Elapsed);

            progress.Reset();

            return software;
        }

        private SoftwareInfo GetSoftware(BinaryDetectorWorker[] workers, uint?[] offsets, ProgressState progress, CancellationToken token)
        {
            var count = workers.Length;
            if (count == 1)
            {
                Logger.LogDebug("Detecting software in a single thread from {0} offsets", offsets.Length);
                return workers[0].GetSoftware(progress, token);
            }

            Logger.LogDebug("Detecting software in {0} threads from {1} offsets", count, offsets.Length);
            return Enumerable.Range(0, count)
                .AsParallel()
                .Select(i => workers[i].GetSoftware(progress, token))
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

        protected abstract uint?[] GetOffsets();

        protected static uint? GetOffsets(IEnumerable<int> offsets)
        {
            var uOffsets = 0u;
            var index = 0;
            foreach (var value in offsets)
            {
                uOffsets += (uint)value << (index << 2);
                index++;
            }
            return uOffsets;
        }
    }
}