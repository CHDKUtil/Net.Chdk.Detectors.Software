﻿using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using Net.Chdk.Providers.Category;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    sealed class SoftwareDetector : ISoftwareDetector
    {
        private ILogger Logger { get; }
        private ICategoryProvider CategoryProvider { get; }
        private IEnumerable<IInnerSoftwareDetector> SoftwareDetectors { get; }

        public SoftwareDetector(ICategoryProvider categoryProvider, IEnumerable<IInnerSoftwareDetector> softwareDetectors, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<SoftwareDetector>();
            CategoryProvider = categoryProvider;
            SoftwareDetectors = softwareDetectors;
        }

        public IEnumerable<SoftwareInfo> GetSoftware(CardInfo cardInfo, IProgress<double> progress, CancellationToken token)
        {
            Logger.LogTrace("Detecting software from {0}", cardInfo.DriveLetter);

            return CategoryProvider.GetCategories()
                .Select(c => GetSoftware(cardInfo, progress, c, token))
                .Where(s => s != null)
                .ToArray();
        }

        private SoftwareInfo GetSoftware(CardInfo cardInfo, IProgress<double> progress, string categoryName, CancellationToken token)
        {
            return SoftwareDetectors
                .Select(d => d.GetSoftware(cardInfo, categoryName, progress, token))
                .FirstOrDefault(s => s != null);
        }
    }
}
