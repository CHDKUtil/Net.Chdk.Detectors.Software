﻿using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using System;
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

        public ModulesInfo GetModules(CardInfo card, SoftwareInfo software, IProgress<double> progress)
        {
            var productName = software.Product.Name;
            Logger.LogTrace("Detecting {0} modules from {1}", productName, card.DriveLetter);

            return ModulesDetectors
                .Select(d => d.GetModules(card, software, progress))
                .FirstOrDefault(m => m != null);
        }
    }
}
