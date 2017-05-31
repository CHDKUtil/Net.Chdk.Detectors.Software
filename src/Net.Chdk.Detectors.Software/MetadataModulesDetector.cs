using Microsoft.Extensions.Logging;
using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using Net.Chdk.Validators;
using System;

namespace Net.Chdk.Detectors.Software
{
    sealed class MetadataModulesDetector : MetadataDetector<MetadataModulesDetector, ModulesInfo>, IInnerModulesDetector
    {
        public MetadataModulesDetector(IValidator<ModulesInfo> validator, ILoggerFactory loggerFactory)
            : base(validator, loggerFactory)
        {
        }

        public ModulesInfo GetModules(CardInfo card, SoftwareInfo software, IProgress<double> progress)
        {
            Logger.LogTrace("Detecting modules from {0} metadata", card.DriveLetter);

            var modules = GetValue(card, software.Product.Category, progress);
            if (!software.Product.Name.Equals(modules?.ProductName, StringComparison.InvariantCulture))
                return null;
            return modules;
        }

        protected override string FileName => Files.Metadata.Modules;
    }
}
