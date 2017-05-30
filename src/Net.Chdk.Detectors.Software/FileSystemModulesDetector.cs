using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using Net.Chdk.Providers.Software;
using System;
using System.Collections.Generic;
using System.IO;

namespace Net.Chdk.Detectors.Software
{
    sealed class FileSystemModulesDetector : IInnerModulesDetector
    {
        private const string HashName = "sha256";

        private static Version Version => new Version("1.0");

        private ILogger Logger { get; }
        private IModuleProviderResolver ModuleProviderResolver { get; }
        private ISoftwareHashProvider HashProvider { get; }

        public FileSystemModulesDetector(IModuleProviderResolver moduleProviderResolver, ISoftwareHashProvider hashProvider, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<FileSystemModulesDetector>();
            ModuleProviderResolver = moduleProviderResolver;
            HashProvider = hashProvider;
        }

        public ModulesInfo GetModules(CardInfo card, SoftwareInfo software)
        {
            Logger.LogTrace("Detecting modules from {0} file system", card.DriveLetter);

            var rootPath = card.GetRootPath();
            return new ModulesInfo
            {
                Version = new Version("1.0"),
                ProductName = software.Product.Name,
                Modules = GetModules(software, rootPath)
            };
        }

        private Dictionary<string, ModuleInfo> GetModules(SoftwareInfo software, string basePath)
        {
            var productName = software.Product.Name;
            var moduleProvider = ModuleProviderResolver.GetModuleProvider(productName);
            var modulesPath = moduleProvider.Path;
            var path = Path.Combine(basePath, modulesPath);
            if (!Directory.Exists(path))
                return null;

            var pattern = string.Format("*{0}", moduleProvider.Extension);
            var files = Directory.EnumerateFiles(path, pattern);
            var modules = new Dictionary<string, ModuleInfo>();
            foreach (var file in files)
                AddFile(moduleProvider, software, modulesPath, file, modules);
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

            var moduleInfo = new ModuleInfo
            {
                Created = software.Product.Created,
                Changeset = software.Build.Changeset,
                Hash = GetHash(file, filePath),
            };
            modules.Add(moduleName, moduleInfo);
        }

        private SoftwareHashInfo GetHash(string file, string filePath)
        {
            using (var stream = File.OpenRead(file))
            {
                return HashProvider.GetHash(stream, filePath, HashName);
            }
        }
    }
}
