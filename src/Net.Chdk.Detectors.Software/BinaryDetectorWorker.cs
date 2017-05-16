using Net.Chdk.Encoders.Binary;
using Net.Chdk.Model.Software;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Net.Chdk.Detectors.Software
{
    sealed class BinaryDetectorWorker : IDisposable
    {
        private const string EncodingName = "dancingbits";

        private IEnumerable<IInnerBinarySoftwareDetector> Detectors { get; }
        private IBinaryDecoder BinaryDecoder { get; }

        private readonly int startIndex;
        private readonly int endIndex;
        private readonly byte[] encBuffer;
        private readonly byte[] decBuffer;
        private readonly ulong[] ulBuffer;
        private readonly uint?[] offsets;

        public BinaryDetectorWorker(IEnumerable<IInnerBinarySoftwareDetector> detectors, IBinaryDecoder binaryDecoder, byte[] encBuffer, int startIndex, int endIndex, uint?[] offsets)
        {
            Detectors = detectors;
            BinaryDecoder = binaryDecoder;

            this.encBuffer = encBuffer;
            this.decBuffer = new byte[encBuffer.Length];
            this.ulBuffer = new ulong[0x100];
            this.startIndex = startIndex;
            this.endIndex = endIndex;
            this.offsets = offsets;
        }

        public BinaryDetectorWorker(IEnumerable<IInnerBinarySoftwareDetector> detectors, IBinaryDecoder binaryDecoder, byte[] encBuffer, uint? offsets)
            : this(detectors, binaryDecoder, encBuffer, 0, 1, offsets != null ? new[] { offsets } : null)
        {
        }

        public void Dispose()
        {
        }

        public SoftwareInfo GetSoftware(ProgressState progress)
        {
            if (offsets == null)
                return PlainGetSoftware();

            for (var index = startIndex; index < endIndex; index++)
            {
                var software = GetSoftware(index);
                if (software != null)
                    return software;
                progress.Update();
            }

            return null;
        }

        private SoftwareInfo GetSoftware(int index)
        {
            var buffer = Decode(offsets[index]);
            if (buffer == null)
                return null;
            var software = GetSoftware(buffer);
            if (software != null)
                software.Encoding = GetEncoding(offsets[index]);
            return software;
        }

        private SoftwareInfo PlainGetSoftware()
        {
            var software = GetSoftware(encBuffer);
            if (software != null)
                software.Encoding = GetEncoding(null);
            return software;
        }

        private SoftwareInfo GetSoftware(byte[] buffer)
        {
            return Detectors
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

        private byte[] Decode(uint? offsets)
        {
            if (offsets == null)
                return encBuffer;
            if (BinaryDecoder.Decode(encBuffer, decBuffer, ulBuffer, offsets))
                return decBuffer;
            return null;
        }

        private static SoftwareEncodingInfo GetEncoding(uint? offsets)
        {
            return new SoftwareEncodingInfo
            {
                Name = offsets != null ? EncodingName : string.Empty,
                Data = offsets
            };
        }
    }
}
