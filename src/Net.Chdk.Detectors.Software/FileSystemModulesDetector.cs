using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using Net.Chdk.Providers.Crypto;
using Net.Chdk.Providers.Software;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    sealed class FileSystemModulesDetector : IInnerModulesDetector
    {
        private const string HashName = "sha256";

        private static Version Version => new Version("1.0");

        private ILogger Logger { get; }
        private IModuleProviderResolver ModuleProviderResolver { get; }
        private IHashProvider HashProvider { get; }

        public FileSystemModulesDetector(IModuleProviderResolver moduleProviderResolver, IHashProvider hashProvider, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<FileSystemModulesDetector>();
            ModuleProviderResolver = moduleProviderResolver;
            HashProvider = hashProvider;
        }

        public ModulesInfo GetModules(CardInfo card, SoftwareInfo software, IProgress<double> progress, CancellationToken token)
        {
            var productName = software.Product.Name;
            Logger.LogTrace("Detecting {0} modules from {1} file system", productName, card.DriveLetter);

            var rootPath = card.GetRootPath();
            return new ModulesInfo
            {
                Version = new Version("1.0"),
                ProductName = productName,
                Modules = GetModules(software, rootPath, progress, token)
            };
        }

        private Dictionary<string, ModuleInfo> GetModules(SoftwareInfo software, string basePath, IProgress<double> progress, CancellationToken token)
        {
            var productName = software.Product.Name;
            var moduleProvider = ModuleProviderResolver.GetModuleProvider(productName);
            var modulesPath = moduleProvider.Path;
            if (modulesPath == null)
                return null;

            var path = Path.Combine(basePath, modulesPath);
            if (!Directory.Exists(path))
                return null;

            token.ThrowIfCancellationRequested();

            var pattern = string.Format("*{0}", moduleProvider.Extension);
            var files = Directory.EnumerateFiles(path, pattern);
            var count = progress != null
                ? files.Count()
                : 0;
            var index = 0;
            var modules = new Dictionary<string, ModuleInfo>();
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();

                AddFile(moduleProvider, software, modulesPath, file, modules);
                if (progress != null)
                    progress.Report((double)(++index) / count);
            }
            return modules;
        }

        private void AddFile(IModuleProvider moduleProvider, SoftwareInfo software, string modulesPath, string file, Dictionary<string, ModuleInfo> modules)
        {
            var fileName = Path.GetFileName(file);
            var filePath = Path.Combine(modulesPath, fileName).ToLowerInvariant();

            var moduleName = moduleProvider.GetModuleName(filePath);
            if (moduleName == null)
            {
                Logger.LogError("Missing module for {0}", filePath);
                moduleName = fileName;
            }

            ModuleInfo moduleInfo;
            if (!modules.TryGetValue(moduleName, out moduleInfo))
            {
                moduleInfo = CreateModule(software);
                modules.Add(moduleName, moduleInfo);
            }

            var hashString = GetHashString(file);
            moduleInfo.Hash.Values.Add(filePath, hashString);

        }

        private static ModuleInfo CreateModule(SoftwareInfo software)
        {
            return new ModuleInfo
            {
                Created = software.Product.Created,
                Changeset = software.Build.Changeset,
                Hash = CreateHash(),
            };
        }

        private static SoftwareHashInfo CreateHash()
        {
            return new SoftwareHashInfo
            {
                Name = HashName,
                Values = new Dictionary<string, string>(),
            };
        }

        private string GetHashString(string file)
        {
            using (var stream = File.OpenRead(file))
            {
                return HashProvider.GetHashString(stream, HashName);
            }
        }
    }
}
