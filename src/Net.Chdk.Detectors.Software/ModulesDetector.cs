using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using System.Collections.Generic;
using System.Linq;

namespace Net.Chdk.Detectors.Software
{
    sealed class ModulesDetector : IModulesDetector
    {
        private ILogger Logger { get; }
        private IEnumerable<IInnerModulesDetector> ModulesDetectors { get; }

        public ModulesDetector(IEnumerable<IInnerModulesDetector> modulesDetectors, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<ModulesDetector>();
            ModulesDetectors = modulesDetectors;
        }

        public ModulesInfo GetModules(CardInfo card, SoftwareInfo software)
        {
            Logger.LogTrace("Detecting modules from {0}", card.DriveLetter);

            return ModulesDetectors
                .Select(d => d.GetModules(card, software))
                .FirstOrDefault(m => m != null);
        }
    }
}
