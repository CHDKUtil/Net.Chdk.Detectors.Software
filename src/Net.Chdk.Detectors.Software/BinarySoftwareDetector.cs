﻿using Microsoft.Extensions.Logging;
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
            var version = GetEncodingVersion(software.Product, software.Camera, software.Encoding);

            var software2 = GetSoftware(detectors, encBuffer, version);
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

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, int? version)
        {
            if (version == null)
                return GetSoftware(detectors, encBuffer);
            if (version == 0)
                return PlainGetSoftware(detectors, encBuffer);
            var decBuffer = new byte[encBuffer.Length];
            return GetSoftware(detectors, encBuffer, decBuffer, version.Value);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer)
        {
            var software = PlainGetSoftware(detectors, encBuffer);
            if (software != null)
                return software;
            var maxVersion = BinaryDecoder.MaxVersion;
            var decBuffers = new byte[maxVersion][];
            for (var i = 0; i < maxVersion; i++)
                decBuffers[i] = new byte[encBuffer.Length];
            return Enumerable.Range(1, maxVersion)
                .AsParallel()
                .Select(v => GetSoftware(detectors, encBuffer, decBuffers[v - 1], v))
                .FirstOrDefault(s => s != null);
        }

        private SoftwareInfo GetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] encBuffer, byte[] decBuffer, int version)
        {
            if (!BinaryDecoder.Decode(encBuffer, decBuffer, version))
                return null;
            var software = DoGetSoftware(detectors, decBuffer);
            if (software != null)
                software.Encoding = GetEncodingInfo(version);
            return software;
        }

        private static SoftwareInfo PlainGetSoftware(IEnumerable<IInnerBinarySoftwareDetector> detectors, byte[] buffer)
        {
            var software = DoGetSoftware(detectors, buffer);
            if (software != null)
                software.Encoding = PlainGetEncodingInfo();
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
                if (Enumerable.Range(0, bytes.Length).All(j => buffer[i + j] == bytes[j]))
                    yield return i + bytes.Length;
        }

        private int? GetEncodingVersion(SoftwareProductInfo product, SoftwareCameraInfo camera, SoftwareEncodingInfo encoding)
        {
            if (encoding != null)
                return GetEncodingVersion(encoding);
            var cameraModel = CameraProvider.GetCamera(product, camera);
            return cameraModel?.EncodingVersion;
        }

        private IEnumerable<IInnerBinarySoftwareDetector> GetDetectors(SoftwareProductInfo product)
        {
            return product?.Name == null
                ? SoftwareDetectors
                : SoftwareDetectors.Where(d => d.ProductName.Equals(product.Name, StringComparison.InvariantCulture));
        }

        private static int? GetEncodingVersion(SoftwareEncodingInfo encoding)
        {
            if (encoding?.Name == null)
                return null;
            if (encoding.Name.Length == 0)
                return 0;
            if (!EncodingName.Equals(encoding.Name, StringComparison.InvariantCulture))
                return null;
            return (int?)encoding.Data;
        }

        private static SoftwareEncodingInfo GetEncodingInfo(int version)
        {
            return new SoftwareEncodingInfo
            {
                Name = EncodingName,
                Data = (ulong)version
            };
        }

        private static SoftwareEncodingInfo PlainGetEncodingInfo()
        {
            return new SoftwareEncodingInfo
            {
                Name = string.Empty
            };
        }
    }
}
