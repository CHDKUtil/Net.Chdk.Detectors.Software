using Microsoft.Extensions.DependencyInjection;

namespace Net.Chdk.Detectors.Software
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<ISoftwareDetector, SoftwareDetector>()
                .AddSingleton<IInnerSoftwareDetector, MetadataSoftwareDetector>()
                .AddSingleton<IInnerSoftwareDetector, BinarySoftwareDetector>()
                .AddSingleton<IInnerSoftwareDetector, FileSystemSoftwareDetector>();
        }
    }
}
