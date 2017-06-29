using Microsoft.Extensions.Logging;
using Net.Chdk.Encoders.Binary;
using Net.Chdk.Json;
using Net.Chdk.Model.Software;
using Net.Chdk.Providers.Boot;
using Net.Chdk.Providers.Software;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    sealed class PsHashSoftwareDetector : PsBinarySoftwareDetector
    {
        public PsHashSoftwareDetector(IEnumerable<IProductBinarySoftwareDetector> softwareDetectors, IBinaryDecoder binaryDecoder, IBootProviderResolver bootProviderResolver, IEncodingProvider encodingProvider, ISoftwareHashProvider hashProvider, ILoggerFactory loggerFactory)
            : base(softwareDetectors, binaryDecoder, bootProviderResolver, encodingProvider, hashProvider, loggerFactory.CreateLogger<PsHashSoftwareDetector>())
        {
            _hash2software = new Lazy<IDictionary<string, SoftwareInfo>>(GetHash2Software);
        }

        public override SoftwareInfo GetSoftware(byte[] inBuffer, IProgress<double> progress, CancellationToken token)
        {
            var fileName = BootProvider.FileName;
            var hash = HashProvider.GetHash(inBuffer, fileName, HashName);
            var hashValue = hash.Values[fileName.ToLowerInvariant()];
            SoftwareInfo software;
            Hash2Software.TryGetValue(hashValue, out software);
            return software;
        }

        private readonly Lazy<IDictionary<string, SoftwareInfo>> _hash2software;

        private IDictionary<string, SoftwareInfo> Hash2Software => _hash2software.Value;

        private IDictionary<string, SoftwareInfo> GetHash2Software()
        {
            var path = Path.Combine(Directories.Data, Directories.Category, CategoryName, "hash2sw.json");
            using (var stream = File.OpenRead(path))
            {
                return JsonObject.Deserialize<IDictionary<string, SoftwareInfo>>(stream);
            }
        }

        protected override uint?[] GetOffsets()
        {
            throw new NotImplementedException();
        }
    }
}
