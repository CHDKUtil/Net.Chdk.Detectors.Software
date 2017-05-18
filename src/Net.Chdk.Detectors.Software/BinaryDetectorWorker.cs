﻿using Net.Chdk.Encoders.Binary;
using Net.Chdk.Model.Software;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    sealed class BinaryDetectorWorker : IDisposable
    {
        private const string EncodingName = "dancingbits";
        private const int ChunkSize = 0x400;

        private IEnumerable<IProductBinarySoftwareDetector> Detectors { get; }

        private readonly byte[] encBuffer;
        private readonly byte[] decBuffer;
        private readonly byte[] tmpBuffer1;
        private readonly byte[] tmpBuffer2;
        private readonly MemoryStream encStream;
        private readonly MemoryStream decStream;
        private readonly Func<uint?, bool> decode;
        private readonly uint?[] offsets;

        public BinaryDetectorWorker(IEnumerable<IProductBinarySoftwareDetector> detectors, IBinaryDecoder binaryDecoder, byte[] encBuffer, int startIndex, int endIndex, uint?[] offsets)
        {
            Detectors = detectors;

            this.encBuffer = encBuffer;
            this.decBuffer = new byte[encBuffer.Length];
            this.tmpBuffer1 = new byte[ChunkSize];
            this.tmpBuffer2 = new byte[ChunkSize];
            this.encStream = new MemoryStream(this.encBuffer, false);
            this.decStream = new MemoryStream(this.decBuffer);
            this.decode = o => binaryDecoder.Decode(encStream, decStream, tmpBuffer1, tmpBuffer2, o);

            if (offsets != null)
            {
                this.offsets = new uint?[endIndex - startIndex];
                for (var i = 0; i < this.offsets.Length; i++)
                    this.offsets[i] = offsets[i + startIndex];
            }
        }

        public BinaryDetectorWorker(IEnumerable<IProductBinarySoftwareDetector> detectors, IBinaryDecoder binaryDecoder, byte[] encBuffer, SoftwareEncodingInfo encoding)
            : this(detectors, binaryDecoder, encBuffer, 0, 1, encoding != null ? new[] { encoding.Data } : null)
        {
        }

        public void Dispose()
        {
            encStream.Dispose();
            decStream.Dispose();
        }

        public SoftwareInfo GetSoftware(ProgressState progress, CancellationToken token)
        {
            if (offsets == null)
                return PlainGetSoftware();

            for (var index = 0; index < offsets.Length; index++)
            {
                if (progress.IsCompleted)
                    return null;
                token.ThrowIfCancellationRequested();
                var software = GetSoftware(offsets[index]);
                if (software != null)
                {
                    progress.SetCompleted();
                    return software;
                }
                progress.Update();
            }

            return null;
        }

        private SoftwareInfo GetSoftware(uint? offsets)
        {
            var buffer = Decode(offsets);
            if (buffer == null)
                return null;
            var software = GetSoftware(buffer);
            if (software != null)
                software.Encoding = GetEncoding(offsets);
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
            var tuples = Detectors
                .SelectMany(GetBytes)
                .ToArray();
            return GetSoftware(buffer, tuples);
        }

        private static SoftwareInfo GetSoftware(byte[] buffer, Tuple<Func<byte[], int, SoftwareInfo>, byte[]>[] tuples)
        {
            var maxLength = tuples.Max(t => t.Item2.Length);
            for (int i = 0; i < buffer.Length - maxLength; i++)
            {
                for (int j = 0; j < tuples.Length; j++)
                {
                    var bytes = tuples[j].Item2;
                    var getSoftware = tuples[j].Item1;
                    if (Equals(buffer, bytes, i))
                    {
                        var software = getSoftware(buffer, i + bytes.Length);
                        if (software != null)
                            return software;
                    }
                }
            }
            return null;
        }

        private static IEnumerable<Tuple<Func<byte[], int, SoftwareInfo>, byte[]>> GetBytes(IProductBinarySoftwareDetector d)
        {
            return d.Bytes.Select(b => Tuple.Create<Func<byte[], int, SoftwareInfo>, byte[]>(d.GetSoftware, b));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            encStream.Seek(0, SeekOrigin.Begin);
            decStream.Seek(0, SeekOrigin.Begin);
            if (decode(offsets))
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
