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
    sealed class FileSystemModulesDetector : IInnerModulesDetector, IFileSystemModulesDetector
    {
        private const string HashName = "sha256";

        private static Version Version => new Version("1.0");

        private ILogger Logger { get; }
        private IModuleProviderResolver ModuleProviderResolver { get; }
        private IEnumerable<IInnerModuleDetector> ModuleDetectors { get; }
        private IHashProvider HashProvider { get; }

        public FileSystemModulesDetector(IModuleProviderResolver moduleProviderResolver, IEnumerable<IInnerModuleDetector> moduleDetectors, IHashProvider hashProvider, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<FileSystemModulesDetector>();
            ModuleProviderResolver = moduleProviderResolver;
            ModuleDetectors = moduleDetectors;
            HashProvider = hashProvider;
        }

        public ModulesInfo GetModules(CardInfo card, CardInfo card2, SoftwareInfo software, IProgress<double> progress, CancellationToken token)
        {
            if (card2 == null)
                return null;
            var rootPath = card2.GetRootPath();
            return GetModules(software, rootPath, progress, token);
        }

        public ModulesInfo GetModules(SoftwareInfo software, string basePath, IProgress<double> progress, CancellationToken token)
        {
            var productName = software.Product.Name;
            Logger.LogTrace("Detecting {0} modules from {1} file system", productName, basePath);

            return new ModulesInfo
            {
                Version = new Version("1.0"),
                Product = new ModulesProductInfo
                {
                    Name = productName
                },
                Modules = DoGetModules(software, basePath, progress, token)
            };
        }

        private Dictionary<string, ModuleInfo> DoGetModules(SoftwareInfo software, string basePath, IProgress<double> progress, CancellationToken token)
        {
            var productName = software.Product.Name;
            var moduleProvider = ModuleProviderResolver.GetModuleProvider(productName);
            var modulesPath = moduleProvider?.Path;
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

            var buffer = File.ReadAllBytes(file);

            ModuleInfo moduleInfo;
            if (!modules.TryGetValue(moduleName, out moduleInfo))
            {
                moduleInfo = GetModule(software, buffer);
                modules.Add(moduleName, moduleInfo);
            }

            var hashString = HashProvider.GetHashString(buffer, HashName);
            moduleInfo.Hash.Values.Add(filePath, hashString);
        }

        private ModuleInfo GetModule(SoftwareInfo software, byte[] buffer)
        {
            return ModuleDetectors
                .Select(d => d.GetModule(software, buffer, HashName))
                .FirstOrDefault(m => m != null);
        }
    }
}
