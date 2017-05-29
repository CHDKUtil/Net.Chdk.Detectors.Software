using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using Net.Chdk.Providers.Software;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Net.Chdk.Detectors.Software
{
    sealed class FileSystemModulesDetector : IInnerModulesDetector
    {
        private const string HashName = "sha256";

        private static Version Version => new Version("1.0");

        private ILogger Logger { get; }
        private IModulesProviderResolver ModulesProviderResolver { get; }
        private ISoftwareHashProvider HashProvider { get; }

        public FileSystemModulesDetector(IModulesProviderResolver modulesProviderResolver, ISoftwareHashProvider hashProvider, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<FileSystemModulesDetector>();
            ModulesProviderResolver = modulesProviderResolver;
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
            var modulesProvider = ModulesProviderResolver.GetModulesProvider(productName);
            var modulesPath = modulesProvider.Path;
            var path = Path.Combine(basePath, modulesPath);
            var pattern = string.Format("*{0}", modulesProvider.Extension);
            var files = Directory.EnumerateFiles(path, pattern);

            var flatModules = Flatten(modulesProvider);
            var modules = new Dictionary<string, ModuleInfo>();
            foreach (var file in files)
                AddFile(software, modulesPath, modules, file, flatModules);
            return modules;
        }

        private void AddFile(SoftwareInfo software, string modulesPath, Dictionary<string, ModuleInfo> modules, string file, Dictionary<string, string[]> flatModules)
        {
            var fileName = Path.GetFileName(file);
            var filePath = Path.Combine(modulesPath, fileName).ToLowerInvariant();

            var moduleName = GetModuleName(flatModules, filePath);
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

        private string GetModuleName(Dictionary<string, string[]> flatModules, string filePath)
        {
            foreach (var kvp in flatModules)
            {
                if (kvp.Value.Contains(filePath, StringComparer.InvariantCultureIgnoreCase))
                    return kvp.Key;
            }
            return null;
        }

        private static Dictionary<string, string[]> Flatten(IModulesProvider modulesProvider)
        {
            var flat = new Dictionary<string, string[]>();
            Flatten(modulesProvider.Children, flat);
            return flat;
        }

        private static void Flatten(IDictionary<string, ModuleData> modules, Dictionary<string, string[]> flat)
        {
            if (modules != null)
            {
                foreach (var kvp in modules)
                {
                    var files = kvp.Value.Files;
                    if (files != null)
                        flat.Add(kvp.Key, files);
                    Flatten(kvp.Value.Children, flat);
                }
            }
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
