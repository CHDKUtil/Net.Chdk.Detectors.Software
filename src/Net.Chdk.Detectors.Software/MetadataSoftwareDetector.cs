using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Net.Chdk.Detectors.Software
{
    sealed class MetadataSoftwareDetector : MetadataDetector<MetadataSoftwareDetector, SoftwareInfo>, IInnerSoftwareDetector
    {
        private static readonly string[] SecureHashes = new[] { "sha256", "sha384", "sha512" };

        public MetadataSoftwareDetector(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public SoftwareInfo GetSoftware(CardInfo cardInfo)
        {
            Logger.LogTrace("Detecting software from {0} metadata", cardInfo.DriveLetter);

            var software = GetValue(cardInfo);
            if (software == null)
                return null;

            if (!Validate(software.Version))
                return null;

            if (!Validate(software.Product))
                return null;

            if (!Validate(software.Camera))
                return null;

            if (!Validate(software.Build))
                return null;

            if (!Validate(software.Compiler))
                return null;

            if (!Validate(software.Source))
                return null;

            if (!Validate(software.Hash, cardInfo))
                return null;

            return software;
        }

        protected override string FileName => "SOFTWARE.JSN";

        private static bool Validate(Version version)
        {
            if (version == null)
                return false;

            if (version.Major < 1 || version.Minor < 0)
                return false;

            return true;
        }

        private static bool Validate(SoftwareProductInfo product)
        {
            if (product == null)
                return false;

            if (string.IsNullOrEmpty(product.Name))
                return false;

            if (product.Version == null)
                return false;

            if (product.Version.Major < 0 || product.Version.Minor < 0)
                return false;

            if (product.Version.MajorRevision < 0 || product.Version.MinorRevision < 0)
                return false;

            if (product.Created == null)
                return false;

            if (product.Created.Value < new DateTime(2000, 1, 1) || product.Created.Value > DateTime.UtcNow)
                return false;

            if (product.Language == null)
                return false;

            return true;
        }

        private static bool Validate(SoftwareCameraInfo camera)
        {
            if (camera == null)
                return false;

            if (string.IsNullOrEmpty(camera.Platform))
                return false;

            if (string.IsNullOrEmpty(camera.Revision))
                return false;

            return true;
        }

        private static bool Validate(SoftwareBuildInfo build)
        {
            if (build == null)
                return false;

            // Empty in update
            if (build.Name == null)
                return false;

            // Empty in final
            if (build.Status == null)
                return false;

            return true;
        }

        private static bool Validate(SoftwareCompilerInfo compiler)
        {
            // Unknown in download
            if (compiler == null)
                return true;

            if (string.IsNullOrEmpty(compiler.Name))
                return false;

            if (compiler.Version == null)
                return false;

            return true;
        }

        private static bool Validate(SoftwareSourceInfo source)
        {
            // Missing in manual build
            if (source == null)
                return true;

            if (string.IsNullOrEmpty(source.Name))
                return false;

            if (string.IsNullOrEmpty(source.Channel))
                return false;

            if (source.Url == null)
                return false;

            return true;
        }

        private static bool Validate(SoftwareHashInfo hash, CardInfo cardInfo)
        {
            if (hash == null)
                return false;

            if (string.IsNullOrEmpty(hash.Name))
                return false;

            if (!SecureHashes.Contains(hash.Name))
                return false;

            return Validate(hash.Values, hash.Name, cardInfo);
        }

        private static bool Validate(IDictionary<string, string> hashValues, string hashName, CardInfo cardInfo)
        {
            if (hashValues == null)
                return false;

            if (hashValues.Count == 0)
                return false;

            var rootPath = cardInfo.GetRootPath();
            foreach (var kvp in hashValues)
            {
                if (string.IsNullOrEmpty(kvp.Key))
                    return false;

                if (string.IsNullOrEmpty(kvp.Value))
                    return false;

                var fileName = kvp.Key.ToUpperInvariant();
                var filePath = Path.Combine(rootPath, fileName);
                if (!File.Exists(filePath))
                    return false;

                var hashString = GetHashString(filePath, hashName);
                if (!hashString.Equals(kvp.Value))
                    return false;
            }

            return true;
        }

        private static string GetHashString(string filePath, string hashName)
        {
            var hash = ComputeHash(filePath, hashName);
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }

        private static byte[] ComputeHash(string filePath, string hashName)
        {
            var hashAlgorithm = HashAlgorithm.Create(hashName);
            using (var stream = File.OpenRead(filePath))
            {
                return hashAlgorithm.ComputeHash(stream);
            }
        }
    }
}
