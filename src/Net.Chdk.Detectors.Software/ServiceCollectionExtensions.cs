using Microsoft.Extensions.DependencyInjection;

namespace Net.Chdk.Detectors.Software
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<ISoftwareDetector, SoftwareDetector>();
        }

        public static IServiceCollection AddMetadataSoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<IInnerSoftwareDetector, MetadataSoftwareDetector>();
        }

        public static IServiceCollection AddBinarySoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<IInnerSoftwareDetector, BinarySoftwareDetector>();
        }

        public static IServiceCollection AddKnownBinarySoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<IBinarySoftwareDetector, KnownBinarySoftwareDetector>();
        }

        public static IServiceCollection AddUnkownBinarySoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<IBinarySoftwareDetector, UnknownBinarySoftwareDetector>();
        }

        public static IServiceCollection AddFileSystemSoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<IInnerSoftwareDetector, FileSystemSoftwareDetector>();
        }
    }
}
