﻿using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.Binary;
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
    abstract class BinarySoftwareDetectorBase : IInnerBinarySoftwareDetector
    {
        private const string HashName = "sha256";

        protected ILogger Logger { get; }
        protected IBinaryDecoder BinaryDecoder { get; }
        protected IBootProvider BootProvider { get; }

        private IEnumerable<IProductBinarySoftwareDetector> SoftwareDetectors { get; }
        private ICameraProvider CameraProvider { get; }
        private ISoftwareHashProvider HashProvider { get; }

        protected BinarySoftwareDetectorBase(IEnumerable<IProductBinarySoftwareDetector> softwareDetectors, IBinaryDecoder binaryDecoder, IBootProvider bootProvider, ICameraProvider cameraProvider, ISoftwareHashProvider hashProvider, ILogger logger)
        {
            Logger = logger;
            SoftwareDetectors = softwareDetectors;
            BinaryDecoder = binaryDecoder;
            BootProvider = bootProvider;
            CameraProvider = cameraProvider;
            HashProvider = hashProvider;
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

            var inBuffer = File.ReadAllBytes(diskbootPath);
            var software = GetSoftware(SoftwareDetectors, inBuffer, progress, token);
            if (software != null)
            {
                if (software.Product.Created == null)
                    software.Product.Created = File.GetCreationTimeUtc(diskbootPath);
                software.Hash = HashProvider.GetHash(inBuffer, fileName, HashName);
            }
            return software;
        }

        public bool UpdateSoftware(SoftwareInfo software, byte[] inBuffer)
        {
            var detectors = GetDetectors(software.Product);
            var encoding = GetEncoding(software.Product, software.Camera, software.Encoding);

            software.Hash = HashProvider.GetHash(inBuffer, BootProvider.FileName, HashName);

            var software2 = GetSoftware(detectors, inBuffer, encoding);
            if (software2 != null)
            {
                if (software2.Product.Created != null)
                    software.Product.Created = software2.Product.Created;
                if (software.Encoding == null)
                    software.Encoding = software2.Encoding;
                return true;
            }

            return false;
        }

        private SoftwareInfo GetSoftware(IEnumerable<IProductBinarySoftwareDetector> detectors, byte[] inBuffer, SoftwareEncodingInfo encoding,
            IProgress<double> progress = null, CancellationToken token = default(CancellationToken))
        {
            if (encoding == null)
                return GetSoftware(detectors, inBuffer, progress, token);
            return DoGetSoftware(detectors, inBuffer, encoding, token);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IProductBinarySoftwareDetector> detectors, byte[] inBuffer, IProgress<double> progress, CancellationToken token)
        {
            if (!BinaryDecoder.ValidatePrefix(inBuffer, inBuffer.Length))
                return PlainGetSoftware(detectors, inBuffer, token);
            return DoGetSoftware(detectors, inBuffer, progress, token);
        }

        protected SoftwareInfo PlainGetSoftware(IEnumerable<IProductBinarySoftwareDetector> detectors, byte[] inBuffer, CancellationToken token)
        {
            using (var worker = new BinaryDetectorWorker(detectors, BootProvider, BinaryDecoder, inBuffer, new SoftwareEncodingInfo()))
            {
                return worker.GetSoftware(ProgressState.Empty, token);
            }
        }

        private SoftwareInfo DoGetSoftware(IEnumerable<IProductBinarySoftwareDetector> detectors, byte[] inBuffer, SoftwareEncodingInfo encoding, CancellationToken token)
        {
            using (var worker = new BinaryDetectorWorker(detectors, BootProvider, BinaryDecoder, inBuffer, encoding))
            {
                return worker.GetSoftware(ProgressState.Empty, token);
            }
        }

        protected virtual SoftwareInfo DoGetSoftware(IEnumerable<IProductBinarySoftwareDetector> detectors, byte[] inBuffer, IProgress<double> progress, CancellationToken token)
        {
            var maxThreads = Properties.Settings.Default.MaxThreads;
            var processorCount = Environment.ProcessorCount;
            var count = maxThreads > 0 && maxThreads < processorCount
                ? maxThreads
                : processorCount;

            var offsets = GetOffsets();

            var watch = new Stopwatch();
            watch.Start();

            var workers = new BinaryDetectorWorker[count];
            for (var i = 0; i < count; i++)
            {
                workers[i] = new BinaryDetectorWorker(detectors, BootProvider, BinaryDecoder, inBuffer,
                    i * offsets.Length / count, (i + 1) * offsets.Length / count, offsets);
            }

            var progressState = new ProgressState(offsets.Length, progress);
            var software = GetSoftware(workers, offsets.Length, progressState, token);

            for (var i = 0; i < count; i++)
            {
                workers[i].Dispose();
            }

            watch.Stop();
            Logger.LogDebug("Detecting software completed in {0}", watch.Elapsed);

            progressState.Reset();

            return software;
        }

        private SoftwareInfo GetSoftware(BinaryDetectorWorker[] workers, int offsetCount, ProgressState progress, CancellationToken token)
        {
            var workerCount = workers.Length;
            if (workerCount == 1)
            {
                Logger.LogDebug("Detecting software in a single thread from {0} offsets", offsetCount);
                return workers[0].GetSoftware(progress, token);
            }

            Logger.LogDebug("Detecting software in {0} threads from {1} offsets", workerCount, offsetCount);

            var threads = new Thread[workerCount];
            var results = new SoftwareInfo[workerCount];
            for (var j = 0; j < threads.Length; j++)
            {
                var i = j;
                threads[i] = new Thread(() => results[i] = workers[i].GetSoftware(progress, token));
            }

            foreach (var thread in threads)
                thread.Start();

            foreach (var thread in threads)
                thread.Join();

            return results
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

        private IEnumerable<IProductBinarySoftwareDetector> GetDetectors(SoftwareProductInfo product)
        {
            return product?.Name == null
                ? SoftwareDetectors
                : SoftwareDetectors.Where(d => d.ProductName.Equals(product.Name, StringComparison.InvariantCulture));
        }

        protected abstract uint?[] GetOffsets();
    }
}
