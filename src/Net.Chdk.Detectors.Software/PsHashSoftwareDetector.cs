using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.Binary;
using Net.Chdk.Json;
using Net.Chdk.Model.Software;
using Net.Chdk.Providers.Boot;
using Net.Chdk.Providers.Software;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    sealed class BytesComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] x, byte[] y)
        {
            return StructuralComparisons.StructuralEqualityComparer.Equals(x, y);
        }

        public int GetHashCode(byte[] obj)
        {
            return StructuralComparisons.StructuralEqualityComparer.GetHashCode(obj);
        }
    }

    sealed class PsHashSoftwareDetector : PsBinarySoftwareDetector
    {
        public PsHashSoftwareDetector(IEnumerable<IProductBinarySoftwareDetector> softwareDetectors, IBinaryDecoder binaryDecoder, IBootProviderResolver bootProviderResolver, IEncodingProvider encodingProvider, ISoftwareHashProvider hashProvider, ILoggerFactory loggerFactory)
            : base(softwareDetectors, binaryDecoder, bootProviderResolver, encodingProvider, hashProvider, loggerFactory.CreateLogger<PsHashSoftwareDetector>())
        {
            _hash2software = new Lazy<IDictionary<byte[], SoftwareInfo>>(GetHash2Software);
        }

        public override SoftwareInfo GetSoftware(byte[] inBuffer, IProgress<double> progress, CancellationToken token)
        {
            var fileName = BootProvider.FileName;
            var hash = HashProvider.GetHash(inBuffer, fileName, HashName);
            var hashStr = hash.Values[fileName.ToLowerInvariant()];
            var hashBytes = GetHashBytes(hashStr);
            SoftwareInfo software;
            Hash2Software.TryGetValue(hashBytes, out software);
            return software;
        }

        private readonly Lazy<IDictionary<byte[], SoftwareInfo>> _hash2software;

        private IDictionary<byte[], SoftwareInfo> Hash2Software => _hash2software.Value;

        private IDictionary<byte[], SoftwareInfo> GetHash2Software()
        {
            var path = Path.Combine(Directories.Data, Directories.Category, CategoryName, "hash2sw.json");
            IDictionary<string, SoftwareInfo> hash2sw;
            using (var stream = File.OpenRead(path))
            {
                hash2sw = JsonObject.Deserialize<IDictionary<string, SoftwareInfo>>(stream);
            }
            var result = new Dictionary<byte[], SoftwareInfo>(new BytesComparer());
            foreach (var kvp in hash2sw)
            {
                var bytes = GetHashBytes(kvp.Key);
                result.Add(bytes, kvp.Value);
            }
            return result;
        }

        private byte[] GetHashBytes(string hashStr)
        {
            var bytes = new byte[hashStr.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hashStr.Substring(i * 2, 2), 16);
            return bytes;
        }

        protected override uint?[] GetOffsets()
        {
            throw new NotImplementedException();
        }
    }
}
